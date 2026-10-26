using BookInventoryLeads.Core;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(new HttpClient { Timeout = TimeSpan.FromSeconds(20) });
builder.Services.AddSingleton(sp => new PriceHistoryStore(AppConfig.SqlitePath));
builder.Services.AddSingleton(sp => DataLayerFactory.Create(sp.GetRequiredService<HttpClient>()));
builder.Services.AddSingleton(sp => NightshiftFactory.Create(sp.GetRequiredService<HttpClient>()));
builder.Services.AddSingleton(sp => new BookDiscovery(sp.GetRequiredService<HttpClient>()));
builder.Services.AddSingleton(sp => new LeadPipeline(
    sp.GetRequiredService<BookDiscovery>(),
    sp.GetRequiredService<PriceHistoryStore>(),
    sp.GetRequiredService<IDataLayer>(),
    sp.GetRequiredService<INightshiftProvider>()));

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => new { status = "ok", time = DateTime.UtcNow });
app.MapGet("/api/status", (IDataLayer dl, INightshiftProvider ns) => new
{
    dataBackend = dl.BackendName,
    reasoningProvider = ns.ProviderName,
    sampleModeEnv = AppConfig.SampleModeFromEnv(),
    zetarisConfigured = AppConfig.ZetarisConfigured,
    nightshiftConfigured = AppConfig.NightshiftConfigured,
    note = "SQLite fallback is NOT Zetaris; local fallback is NOT Nightshift/NVIDIA. See README.",
});

app.MapPost("/api/leads", async (LeadRequest req, LeadPipeline pipeline, CancellationToken ct) =>
    await pipeline.GenerateAsync(req, ct));

app.MapGet("/api/leads", async (string? query, int? topN, int? days, bool? sample, LeadPipeline pipeline, CancellationToken ct) =>
    await pipeline.GenerateAsync(new LeadRequest
    {
        Query = query ?? "bestselling fiction",
        TopN = topN ?? 10, Days = days ?? 7, SampleMode = sample ?? false,
    }, ct));

app.MapGet("/api/samples", () => new
{
    inputs = new[]
    {
        new { name = "Sample 1 - daily top 10", query = "bestselling fiction", topN = 10, days = 1, sampleMode = true, purchaseCost = (decimal?)null, shippingCost = 3.99m, feePercent = 0.12m, targetMarginPercent = 0.20m },
        new { name = "Sample 2 - science fiction, 5 over 7 days", query = "science fiction", topN = 5, days = 7, sampleMode = true, purchaseCost = (decimal?)null, shippingCost = 3.99m, feePercent = 0.12m, targetMarginPercent = 0.20m },
        new { name = "Sample 3 - with explicit costs", query = "bestselling fiction", topN = 10, days = 7, sampleMode = true, purchaseCost = (decimal?)9.00m, shippingCost = 3.99m, feePercent = 0.12m, targetMarginPercent = 0.20m },
    },
    note = "These inputs use SAMPLE_MODE=true, so outputs are illustrative sample data. See samples/ folder for saved outputs.",
});

app.Run();
