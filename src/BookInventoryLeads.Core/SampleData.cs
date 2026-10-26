namespace BookInventoryLeads.Core;

/// <summary>
/// Clearly labelled SAMPLE data only. Prices here are illustrative, NOT real
/// competitor prices and NOT sold-copies data. Used when SAMPLE_MODE=true or
/// when live public APIs are unavailable, so the demo still runs.
/// Descriptions below are short original summaries written for this project.
/// </summary>
public static class SampleData
{
    public static List<BookCandidate> GetBooks() => new()
    {
        new BookCandidate
        {
            Title = "The Midnight Library (Sample)", Authors = new() { "Matt Haig" },
            Description = "Sample entry: a novel about parallel lives and second chances.",
            Genres = new() { "Fiction", "Fantasy" }, Rating = 4.3, RatingsCount = 18400, IsSample = true,
            Sources = new() { "Sample data (not a live API)" },
            CompetitorPrices = new()
            {
                new(18.99m, "USD", "Sample competitor A", true),
                new(17.49m, "USD", "Sample competitor B", true),
                new(19.99m, "USD", "Sample competitor C", true),
            },
        },
        new BookCandidate
        {
            Title = "Atomic Habits (Sample)", Authors = new() { "James Clear" },
            Description = "Sample entry: a practical guide to building good habits and breaking bad ones.",
            Genres = new() { "Self-help", "Non-fiction" }, Rating = 4.8, RatingsCount = 96200, IsSample = true,
            Sources = new() { "Sample data (not a live API)" },
            CompetitorPrices = new()
            {
                new(16.50m, "USD", "Sample competitor A", true),
                new(15.99m, "USD", "Sample competitor B", true),
                new(17.20m, "USD", "Sample competitor C", true),
            },
        },
        new BookCandidate
        {
            Title = "Project Hail Mary (Sample)", Authors = new() { "Andy Weir" },
            Description = "Sample entry: a lone astronaut wakes up with no memory and a mission to save humanity.",
            Genres = new() { "Science fiction" }, Rating = 4.7, RatingsCount = 54100, IsSample = true,
            Sources = new() { "Sample data (not a live API)" },
            CompetitorPrices = new()
            {
                new(21.99m, "USD", "Sample competitor A", true),
                new(20.49m, "USD", "Sample competitor B", true),
                new(22.50m, "USD", "Sample competitor C", true),
            },
        },
        new BookCandidate
        {
            Title = "The Silent Patient (Sample)", Authors = new() { "Alex Michaelides" },
            Description = "Sample entry: a thriller about a woman who stops speaking after a shocking event.",
            Genres = new() { "Thriller", "Mystery" }, Rating = 4.1, RatingsCount = 32800, IsSample = true,
            Sources = new() { "Sample data (not a live API)" },
            CompetitorPrices = new()
            {
                new(14.99m, "USD", "Sample competitor A", true),
                new(13.99m, "USD", "Sample competitor B", true),
                new(15.49m, "USD", "Sample competitor C", true),
            },
        },
        new BookCandidate
        {
            Title = "Sapiens (Sample)", Authors = new() { "Yuval Noah Harari" },
            Description = "Sample entry: a sweeping history of humankind, from early humans to the present.",
            Genres = new() { "History", "Non-fiction" }, Rating = 4.5, RatingsCount = 71200, IsSample = true,
            Sources = new() { "Sample data (not a live API)" },
            CompetitorPrices = new()
            {
                new(24.99m, "USD", "Sample competitor A", true),
                new(23.50m, "USD", "Sample competitor B", true),
                new(26.00m, "USD", "Sample competitor C", true),
            },
        },
        new BookCandidate
        {
            Title = "Dune (Sample)", Authors = new() { "Frank Herbert" },
            Description = "Sample entry: a desert-planet epic of politics, ecology and destiny.",
            Genres = new() { "Science fiction", "Fantasy" }, Rating = 4.6, RatingsCount = 88400, IsSample = true,
            Sources = new() { "Sample data (not a live API)" },
            CompetitorPrices = new()
            {
                new(19.99m, "USD", "Sample competitor A", true),
                new(18.75m, "USD", "Sample competitor B", true),
                new(20.99m, "USD", "Sample competitor C", true),
            },
        },
    };

    /// <summary>Seed price history, labelled sample, so trend reasoning can be demoed on day one.</summary>
    public static List<(string Key, DateTime Date, decimal Median, double Demand)> SeedHistory()
    {
        var seeds = new List<(string, DateTime, decimal, double)>();
        var today = DateTime.UtcNow.Date;
        foreach (var book in GetBooks())
        {
            var key = Reasoning.NormalizeKey(book.Title, book.Authors.FirstOrDefault());
            var median = Reasoning.Median(book.CompetitorPrices.Select(p => p.Amount).ToList()) ?? 15m;
            for (int d = 6; d >= 0; d--)
            {
                // Small illustrative drift only; labelled sample, not real history.
                var m = Math.Round(median * (1 + (6 - d) * 0.004m), 2);
                seeds.Add((key, today.AddDays(-d), m, 60 + d));
            }
        }
        return seeds;
    }
}
