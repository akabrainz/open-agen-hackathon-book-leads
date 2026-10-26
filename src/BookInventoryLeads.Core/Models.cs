namespace BookInventoryLeads.Core;

/// <summary>One price observed from one source. Never invent a source label.</summary>
public record CompetitorPrice(decimal Amount, string Currency, string Source, bool IsSample);

public record BookCandidate
{
    public string Title { get; init; } = "";
    public List<string> Authors { get; init; } = new();
    public string Description { get; init; } = "";
    public string? Isbn { get; init; }
    public List<string> Genres { get; init; } = new();
    public double? Rating { get; init; }
    public int RatingsCount { get; init; }
    public List<CompetitorPrice> CompetitorPrices { get; init; } = new();
    public List<string> Sources { get; init; } = new();
    public bool IsSample { get; init; }
}

public record CostModel
{
    /// <summary>Per-unit purchase cost. Null = engine estimates 55% of competitor median and flags it as an assumption.</summary>
    public decimal? PurchaseCost { get; init; }
    public decimal ShippingCost { get; init; } = 3.99m;
    public decimal FeePercent { get; init; } = 0.12m;       // marketplace / payment fee, share of sale price
    public decimal TargetMarginPercent { get; init; } = 0.20m; // target margin, share of sale price
}

public record LeadRequest
{
    public string Query { get; init; } = "bestselling fiction";
    public int TopN { get; init; } = 10;
    public int Days { get; init; } = 1;          // duration window for price-history trend
    public bool SampleMode { get; init; }        // explicit request flag; env SAMPLE_MODE also forces it
    public CostModel Cost { get; init; } = new();
}

public record BookLead
{
    public string Title { get; init; } = "";
    public List<string> Authors { get; init; } = new();
    public string Description { get; init; } = "";
    public List<string> Genres { get; init; } = new();
    public double? Rating { get; init; }
    public int RatingsCount { get; init; }
    public decimal? CompetitorMedian { get; init; }
    public List<CompetitorPrice> CompetitorPrices { get; init; } = new();
    public double DemandScore { get; init; }
    public double TrendPercent { get; init; }
    public int TrendDataPoints { get; init; }
    public decimal PurchaseCostUsed { get; init; }
    public bool PurchaseCostWasEstimated { get; init; }
    public decimal ShippingCost { get; init; }
    public decimal FeePercent { get; init; }
    public decimal TargetMarginPercent { get; init; }
    public decimal FloorPrice { get; init; }      // price needed to hit target margin after fees
    public decimal? CompetitivePrice { get; init; } // competitor median * 0.98
    public decimal? RecommendedPrice { get; init; }
    public decimal? BreakEvenPurchaseCost { get; init; }
    public string Status { get; init; } = "";    // Recommend | RecommendWithCaution | DoNotStock | InsufficientData
    public int Confidence { get; init; }
    public string Explanation { get; init; } = "";
    public string WhatWouldChangeIt { get; init; } = "";
    public List<string> Trace { get; init; } = new();
    public bool IsSample { get; init; }
    public string ReasoningProvider { get; init; } = "";
}

public record LeadsResponse
{
    public string Query { get; init; } = "";
    public int TopN { get; init; }
    public int Days { get; init; }
    public bool SampleMode { get; init; }
    public bool SampleFallbackUsed { get; init; }
    public string DataBackend { get; init; } = "";      // "Zetaris" or "SQLite fallback (not Zetaris)"
    public string ReasoningProvider { get; init; } = "";
    public List<BookLead> Leads { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
}

public record PriceTrend(double TrendPercent, int DataPoints, decimal? AverageMedian, decimal? LatestMedian);

public static class AppConfig
{
    public static bool SampleModeFromEnv() =>
        string.Equals(Environment.GetEnvironmentVariable("SAMPLE_MODE"), "true", StringComparison.OrdinalIgnoreCase);
    public static string? ZetarisUrl => Environment.GetEnvironmentVariable("ZETARIS_API_URL");
    public static string? ZetarisKey => Environment.GetEnvironmentVariable("ZETARIS_API_KEY");
    public static bool ZetarisConfigured => !string.IsNullOrWhiteSpace(ZetarisUrl) && !string.IsNullOrWhiteSpace(ZetarisKey);
    public static string? NightshiftUrl => Environment.GetEnvironmentVariable("METERLESS_NIGHTSHIFT_API_URL");
    public static string? NightshiftKey => Environment.GetEnvironmentVariable("METERLESS_NIGHTSHIFT_API_KEY");
    public static string NightshiftModel => Environment.GetEnvironmentVariable("METERLESS_NIGHTSHIFT_MODEL") ?? "nvidia/nemotron";
    public static bool NightshiftConfigured => !string.IsNullOrWhiteSpace(NightshiftUrl) && !string.IsNullOrWhiteSpace(NightshiftKey);
    public static string SqlitePath =>
        Environment.GetEnvironmentVariable("SQLITE_PATH")
        ?? Path.Combine(AppContext.BaseDirectory, "data", "book_inventory.db");
}
