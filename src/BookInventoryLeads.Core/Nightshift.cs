using System.Text;
using System.Text.Json;

namespace BookInventoryLeads.Core;

/// <summary>Meterless Nightshift provider interface: the app's primary AI provider.</summary>
public interface INightshiftProvider
{
    string ProviderName { get; }
    Task<string> ExplainAsync(BookLead lead, CancellationToken ct = default);
}

/// <summary>
/// Real Nightshift client (OpenAI-compatible chat-completions endpoint).
/// Only used when METERLESS_NIGHTSHIFT_API_URL + KEY are set, with the model
/// taken from METERLESS_NIGHTSHIFT_MODEL (e.g. an NVIDIA Nemotron model).
/// </summary>
public class NightshiftProvider : INightshiftProvider
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly string _apiKey;
    private readonly string _model;
    public string ProviderName => $"Meterless Nightshift ({_model})";
    public NightshiftProvider(HttpClient http, string baseUrl, string apiKey, string model)
    { _http = http; _baseUrl = baseUrl.TrimEnd('/'); _apiKey = apiKey; _model = model; }

    public async Task<string> ExplainAsync(BookLead lead, CancellationToken ct = default)
    {
        var payload = new
        {
            model = _model,
            messages = new[]
            {
                new { role = "system", content = "You explain a book-reseller pricing decision in 2-3 sentences, citing competitor median, costs and demand. Do not invent prices." },
                new { role = "user", content = $"Book: {lead.Title}. Status: {lead.Status}. Recommended: {lead.RecommendedPrice}. Median: {lead.CompetitorMedian}. Demand: {lead.DemandScore}. Explanation draft: {lead.Explanation}" },
            },
        };
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/chat/completions");
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
        req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var res = await _http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? lead.Explanation;
    }
}

/// <summary>Clearly labelled local fallback. NOT Nightshift and NOT NVIDIA output.</summary>
public class LocalReasoningFallback : INightshiftProvider
{
    public string ProviderName => "Local sample fallback (not Nightshift / NVIDIA)";
    public Task<string> ExplainAsync(BookLead lead, CancellationToken ct = default) => Task.FromResult(lead.Explanation);
}

public static class NightshiftFactory
{
    public static INightshiftProvider Create(HttpClient http) =>
        AppConfig.NightshiftConfigured
            ? new NightshiftProvider(http, AppConfig.NightshiftUrl!, AppConfig.NightshiftKey!, AppConfig.NightshiftModel)
            : new LocalReasoningFallback();
}
