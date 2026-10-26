using System.Text.Json;

namespace BookInventoryLeads.Core;

/// <summary>
/// Discovery via public APIs only: Google Books API and Open Library API.
/// No scraping of sites that forbid it. No sold-copies data is claimed:
/// popularity is approximated transparently from rating + ratings count.
/// Google Books works without a key for basic queries; an optional
/// GOOGLE_BOOKS_API_KEY is honoured if set.
/// </summary>
public class BookDiscovery
{
    private readonly HttpClient _http;
    public BookDiscovery(HttpClient http) => _http = http;

    public async Task<List<BookCandidate>> SearchAsync(string query, int limit, CancellationToken ct = default)
    {
        var google = await SearchGoogleBooksAsync(query, limit, ct);
        var openLib = await SearchOpenLibraryAsync(query, limit, ct);
        // Merge by normalised key: Google supplies price/rating, Open Library supplies subjects.
        var merged = Reasoning.Dedupe(google.Concat(openLib));
        return merged;
    }

    private async Task<List<BookCandidate>> SearchGoogleBooksAsync(string query, int limit, CancellationToken ct)
    {
        try
        {
            var key = Environment.GetEnvironmentVariable("GOOGLE_BOOKS_API_KEY");
            var url = $"https://www.googleapis.com/books/v1/volumes?q={Uri.EscapeDataString(query)}&maxResults={Math.Clamp(limit, 1, 40)}" + (string.IsNullOrWhiteSpace(key) ? "" : $"&key={key}");
            using var res = await _http.GetAsync(url, ct);
            if (!res.IsSuccessStatusCode) return new();
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            var list = new List<BookCandidate>();
            if (!doc.RootElement.TryGetProperty("items", out var items)) return list;
            foreach (var item in items.EnumerateArray())
            {
                var info = item.GetProperty("volumeInfo");
                string Title() => info.TryGetProperty("title", out var v) ? v.GetString() ?? "" : "";
                if (string.IsNullOrWhiteSpace(Title())) continue;
                var authors = info.TryGetProperty("authors", out var a) ? a.EnumerateArray().Select(x => x.GetString() ?? "").Where(s => s != "").ToList() : new List<string>();
                var genres = info.TryGetProperty("categories", out var c) ? c.EnumerateArray().Select(x => x.GetString() ?? "").Where(s => s != "").ToList() : new List<string>();
                double? rating = info.TryGetProperty("averageRating", out var r) ? r.GetDouble() : null;
                int count = info.TryGetProperty("ratingsCount", out var rc) ? rc.GetInt32() : 0;
                var prices = new List<CompetitorPrice>();
                if (item.TryGetProperty("saleInfo", out var sale))
                {
                    foreach (var field in new[] { "listPrice", "retailPrice" })
                    {
                        if (sale.TryGetProperty(field, out var p) && p.TryGetProperty("amount", out var amt))
                        {
                            var cur = p.TryGetProperty("currencyCode", out var cu) ? cu.GetString() ?? "USD" : "USD";
                            prices.Add(new CompetitorPrice((decimal)amt.GetDouble(), cur, $"Google Books {field}", false));
                            break;
                        }
                    }
                }
                string? isbn = null;
                if (info.TryGetProperty("industryIdentifiers", out var ids))
                    foreach (var id in ids.EnumerateArray())
                        if (id.TryGetProperty("type", out var t) && t.GetString() == "ISBN_13" && id.TryGetProperty("identifier", out var v)) isbn = v.GetString();
                list.Add(new BookCandidate
                {
                    Title = Title(), Authors = authors,
                    Description = info.TryGetProperty("description", out var d) ? (d.GetString() ?? "") : "",
                    Isbn = isbn, Genres = genres, Rating = rating, RatingsCount = count,
                    CompetitorPrices = prices, Sources = new() { "Google Books API" },
                });
            }
            return list;
        }
        catch { return new(); } // network failure -> pipeline falls back to labelled sample mode
    }

    private async Task<List<BookCandidate>> SearchOpenLibraryAsync(string query, int limit, CancellationToken ct)
    {
        try
        {
            var url = $"https://openlibrary.org/search.json?q={Uri.EscapeDataString(query)}&limit={Math.Clamp(limit, 1, 40)}&fields=key,title,author_name,subject,subject_facet,ratings_average,ratings_count,first_publish_year";
            using var res = await _http.GetAsync(url, ct);
            if (!res.IsSuccessStatusCode) return new();
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            var list = new List<BookCandidate>();
            if (!doc.RootElement.TryGetProperty("docs", out var docs)) return list;
            foreach (var d in docs.EnumerateArray())
            {
                var title = d.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                if (string.IsNullOrWhiteSpace(title)) continue;
                var authors = d.TryGetProperty("author_name", out var a) ? a.EnumerateArray().Select(x => x.GetString() ?? "").Where(s => s != "").ToList() : new List<string>();
                var genres = d.TryGetProperty("subject", out var s) ? s.EnumerateArray().Take(5).Select(x => x.GetString() ?? "").Where(x => x != "").ToList() : new List<string>();
                if (genres.Count == 0 && d.TryGetProperty("subject_facet", out var sf))
                    genres = sf.EnumerateArray().Take(5).Select(x => x.GetString() ?? "").Where(x => x != "").ToList();
                double? rating = d.TryGetProperty("ratings_average", out var ra) ? ra.GetDouble() : null;
                int count = d.TryGetProperty("ratings_count", out var rc) ? rc.GetInt32() : 0;
                list.Add(new BookCandidate
                {
                    Title = title, Authors = authors, Genres = genres, Rating = rating, RatingsCount = count,
                    Sources = new() { "Open Library API" },
                });
            }
            return list;
        }
        catch { return new(); }
    }
}

public class LeadPipeline
{
    private readonly BookDiscovery _discovery;
    private readonly PriceHistoryStore _memory;
    private readonly IDataLayer _dataLayer;
    private readonly INightshiftProvider _nightshift;

    public LeadPipeline(BookDiscovery discovery, PriceHistoryStore memory, IDataLayer dataLayer, INightshiftProvider nightshift)
    { _discovery = discovery; _memory = memory; _dataLayer = dataLayer; _nightshift = nightshift; }

    public async Task<LeadsResponse> GenerateAsync(LeadRequest req, CancellationToken ct = default)
    {
        bool sampleMode = req.SampleMode || AppConfig.SampleModeFromEnv();
        var warnings = new List<string>();
        List<BookCandidate> candidates;
        bool fallbackUsed = false;

        if (sampleMode)
        {
            candidates = SampleData.GetBooks();
            warnings.Add("SAMPLE_MODE: prices and history below are illustrative sample data, not live market prices.");
        }
        else
        {
            candidates = await _discovery.SearchAsync(req.Query, Math.Max(req.TopN * 3, 10), ct);
            if (candidates.Count == 0)
            {
                candidates = SampleData.GetBooks();
                fallbackUsed = true;
                warnings.Add("Live APIs returned no results (or were unreachable); labelled sample data was used so the demo still runs. Do not present sample prices as real.");
            }
        }

        var deduped = Reasoning.Dedupe(candidates);

        // Seed sample history on first sample run so trend reasoning is visible.
        if ((sampleMode || fallbackUsed) && deduped.Any())
        {
            var firstKey = Reasoning.NormalizeKey(deduped[0].Title, deduped[0].Authors.FirstOrDefault());
            if (!_memory.HasAnyHistory(firstKey))
                foreach (var (key, date, median, demand) in SampleData.SeedHistory())
                    _memory.Record(key, date, median, demand);
        }

        var leads = new List<BookLead>();
        foreach (var book in deduped)
        {
            var key = Reasoning.NormalizeKey(book.Title, book.Authors.FirstOrDefault());
            var trend = _memory.GetTrend(key, Math.Max(1, req.Days));
            var lead = Reasoning.Recommend(book, req.Cost, trend, _nightshift.ProviderName);
            // Nightshift (real) enriches the explanation; the local fallback keeps the template.
            if (AppConfig.NightshiftConfigured)
            {
                try { lead = lead with { Explanation = await _nightshift.ExplainAsync(lead, ct) }; }
                catch (Exception ex) { warnings.Add($"Nightshift explanation failed ({ex.Message}); template explanation kept."); }
            }
            leads.Add(lead);
            var median = Reasoning.Median(book.CompetitorPrices.Select(p => p.Amount).ToList());
            if (median.HasValue) _memory.Record(key, DateTime.UtcNow.Date, median.Value, lead.DemandScore);
        }

        // Ranking: recommended first, then by demand x margin value.
        int Rank(string s) => s switch { "Recommend" => 0, "RecommendWithCaution" => 1, "DoNotStock" => 2, _ => 3 };
        var ranked = leads
            .OrderBy(l => Rank(l.Status))
            .ThenByDescending(l => l.DemandScore * (double)Math.Max(0, (l.RecommendedPrice ?? 0) - l.PurchaseCostUsed - l.ShippingCost))
            .Take(Math.Max(1, req.TopN))
            .ToList();

        try { await _dataLayer.SaveLeadsAsync(ranked, ct); }
        catch (Exception ex) { warnings.Add($"Data-layer save failed ({ex.Message}); leads are still returned in this response."); }

        return new LeadsResponse
        {
            Query = req.Query, TopN = req.TopN, Days = req.Days,
            SampleMode = sampleMode, SampleFallbackUsed = fallbackUsed,
            DataBackend = _dataLayer.BackendName, ReasoningProvider = _nightshift.ProviderName,
            Leads = ranked, Warnings = warnings,
        };
    }
}
