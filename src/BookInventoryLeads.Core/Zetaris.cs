using System.Text;
using System.Text.Json;

namespace BookInventoryLeads.Core;

/// <summary>Data-layer interface. Zetaris is the intended production backend.</summary>
public interface IDataLayer
{
    string BackendName { get; }
    Task SaveLeadsAsync(IEnumerable<BookLead> leads, CancellationToken ct = default);
}

/// <summary>
/// Real Zetaris adapter (HTTP). Only used when ZETARIS_API_URL + ZETARIS_API_KEY
/// are set. The exact endpoint path is configurable via the base URL; this
/// adapter POSTs JSON documents and authenticates with a Bearer key.
/// Without keys it is never presented as live Zetaris.
/// </summary>
public class ZetarisAdapter : IDataLayer
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly string _apiKey;
    public string BackendName => "Zetaris";
    public ZetarisAdapter(HttpClient http, string baseUrl, string apiKey) { _http = http; _baseUrl = baseUrl.TrimEnd('/'); _apiKey = apiKey; }
    public async Task SaveLeadsAsync(IEnumerable<BookLead> leads, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/leads");
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
        req.Content = new StringContent(JsonSerializer.Serialize(leads), Encoding.UTF8, "application/json");
        var res = await _http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();
    }
}

/// <summary>
/// Local SQLite fallback. This is NOT Zetaris. It exists so the app runs,
/// demos and tests pass without sponsor keys, and it is labelled as such
/// in every API response (DataBackend field) and in the UI.
/// </summary>
public class SqliteDataLayer : IDataLayer
{
    private readonly string _dbPath;
    public string BackendName => "SQLite fallback (not Zetaris)";
    public SqliteDataLayer(string dbPath) { _dbPath = dbPath; }
    public Task SaveLeadsAsync(IEnumerable<BookLead> leads, CancellationToken ct = default)
    {
        // Persisting is handled by PriceHistoryStore for history; leads are
        // written to a simple JSON snapshot next to the DB for auditability.
        var dir = Path.GetDirectoryName(_dbPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var snapshot = Path.Combine(dir ?? ".", "latest_leads.json");
        File.WriteAllText(snapshot, JsonSerializer.Serialize(leads, new JsonSerializerOptions { WriteIndented = true }));
        return Task.CompletedTask;
    }
}

public static class DataLayerFactory
{
    public static IDataLayer Create(HttpClient http) =>
        AppConfig.ZetarisConfigured
            ? new ZetarisAdapter(http, AppConfig.ZetarisUrl!, AppConfig.ZetarisKey!)
            : new SqliteDataLayer(AppConfig.SqlitePath);
}
