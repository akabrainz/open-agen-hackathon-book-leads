using System.Text.RegularExpressions;

namespace BookInventoryLeads.Core;

/// <summary>
/// Price reasoning engine. This is a decision engine, not a fixed list:
/// it normalises/dedupes editions, uses a competitor median, scores demand,
/// applies a cost model, and rejects/flags books that cannot meet the margin.
/// </summary>
public static class Reasoning
{
    public static string NormalizeKey(string title, string? author)
    {
        var t = Regex.Replace(title.ToLowerInvariant(), @"[^a-z0-9 ]", " ").Trim();
        t = Regex.Replace(t, @"\s+", " ");
        if (t.Contains(" (sample)")) t = t.Replace(" (sample)", "");
        var a = (author ?? "").ToLowerInvariant().Trim();
        return $"{t}|{a}";
    }

    public static decimal? Median(List<decimal> values)
    {
        if (values.Count == 0) return null;
        var sorted = values.OrderBy(v => v).ToList();
        int n = sorted.Count;
        return n % 2 == 1 ? sorted[n / 2] : (sorted[n / 2 - 1] + sorted[n / 2]) / 2;
    }

    public static double DemandScore(double? rating, int ratingsCount, double trendPercent)
    {
        double ratingPart = (rating ?? 3.0) / 5.0 * 40.0;
        double countPart = Math.Min(40.0, Math.Log10(ratingsCount + 1) * 20.0);
        double trendPart = Math.Clamp(trendPercent / 2.0, -10.0, 10.0);
        return Math.Clamp(ratingPart + countPart + trendPart, 0, 100);
    }

    /// <summary>Dedupe editions: same normalised title+first-author merged, competitor prices unioned.</summary>
    public static List<BookCandidate> Dedupe(IEnumerable<BookCandidate> books)
    {
        var merged = new Dictionary<string, BookCandidate>();
        foreach (var b in books)
        {
            var key = NormalizeKey(b.Title, b.Authors.FirstOrDefault());
            if (!merged.TryGetValue(key, out var existing))
            {
                merged[key] = b;
                continue;
            }
            var prices = existing.CompetitorPrices
                .Concat(b.CompetitorPrices)
                .GroupBy(p => (p.Amount, p.Source))
                .Select(g => g.First()).ToList();
            merged[key] = existing with
            {
                CompetitorPrices = prices,
                Genres = existing.Genres.Concat(b.Genres).Distinct().ToList(),
                Sources = existing.Sources.Concat(b.Sources).Distinct().ToList(),
                Rating = new[] { existing.Rating, b.Rating }.Where(r => r.HasValue).Max(),
                RatingsCount = Math.Max(existing.RatingsCount, b.RatingsCount),
                Description = string.IsNullOrWhiteSpace(existing.Description) ? b.Description : existing.Description,
                IsSample = existing.IsSample && b.IsSample,
            };
        }
        return merged.Values.ToList();
    }

    public static BookLead Recommend(BookCandidate book, CostModel cost, PriceTrend trend, string providerName)
    {
        var trace = new List<string>
        {
            $"Sources: {string.Join(", ", book.Sources.Distinct())}",
            $"Competitor prices considered: {string.Join(", ", book.CompetitorPrices.Select(p => $"{p.Amount:0.00} {p.Currency} ({p.Source}{(p.IsSample ? ", sample" : "")})"))}",
        };
        var median = Median(book.CompetitorPrices.Select(p => p.Amount).ToList());
        var demand = DemandScore(book.Rating, book.RatingsCount, trend.TrendPercent);
        trace.Add($"Demand score {demand:0.0}: rating {(book.Rating?.ToString("0.0") ?? "unknown")} ({(book.Rating ?? 3.0) / 5 * 40:0.0} pts), " +
                  $"ratings count {book.RatingsCount} ({Math.Min(40, Math.Log10(book.RatingsCount + 1) * 20):0.0} pts), " +
                  $"trend {trend.TrendPercent:+0.0;-0.0}% over {trend.DataPoints} point(s) ({Math.Clamp(trend.TrendPercent / 2, -10, 10):+0.0;-0.0} pts).");

        if (median is null)
        {
            trace.Add("No competitor price available -> cannot compute a competitive price or margin floor.");
            return new BookLead
            {
                Title = book.Title, Authors = book.Authors, Description = book.Description, Genres = book.Genres,
                Rating = book.Rating, RatingsCount = book.RatingsCount, CompetitorPrices = book.CompetitorPrices,
                DemandScore = Math.Round(demand, 1), TrendPercent = Math.Round(trend.TrendPercent, 2),
                TrendDataPoints = trend.DataPoints, Status = "InsufficientData", Confidence = 20,
                Explanation = "Insufficient data: no competitor price was found, so no sales price can be recommended.",
                WhatWouldChangeIt = "A verifiable competitor price (or a supplied purchase cost) would allow a recommendation.",
                Trace = trace, IsSample = book.IsSample, ReasoningProvider = providerName,
                ShippingCost = cost.ShippingCost, FeePercent = cost.FeePercent, TargetMarginPercent = cost.TargetMarginPercent,
            };
        }

        bool estimated = !cost.PurchaseCost.HasValue;
        decimal purchase = cost.PurchaseCost ?? Math.Round(median.Value * 0.55m, 2);
        decimal floor = Math.Round((purchase + cost.ShippingCost) / (1 - cost.FeePercent - cost.TargetMarginPercent), 2);
        decimal competitive = Math.Round(median.Value * 0.98m, 2);
        decimal breakEven = Math.Round(competitive * (1 - cost.FeePercent - cost.TargetMarginPercent) - cost.ShippingCost, 2);
        trace.Add($"Cost breakdown: purchase {purchase:0.00} (estimated={estimated}), shipping {cost.ShippingCost:0.00}, " +
                  $"fee {cost.FeePercent:P0} of sale price, target margin {cost.TargetMarginPercent:P0} of sale price.");
        trace.Add($"Floor price (to hit target margin after fees) = ({purchase:0.00} + {cost.ShippingCost:0.00}) / (1 - {cost.FeePercent:0.00} - {cost.TargetMarginPercent:0.00}) = {floor:0.00}.");
        trace.Add($"Competitive price (median x 0.98) = {median:0.00} x 0.98 = {competitive:0.00}. Break-even purchase cost at competitive price = {breakEven:0.00}.");

        string status; decimal recommended; string explanation; string whatChanges;
        if (competitive >= floor)
        {
            status = "Recommend"; recommended = competitive;
            explanation = $"Recommend stocking at {recommended:0.00}: slightly under the competitor median ({median:0.00}) while still clearing the margin floor ({floor:0.00}).";
            whatChanges = $"If purchase cost rises above {breakEven:0.00}, the competitive price would no longer meet the target margin.";
        }
        else if (floor <= median.Value * 1.10m && demand >= 60)
        {
            status = "RecommendWithCaution"; recommended = floor;
            explanation = $"Recommend with caution at {recommended:0.00}: demand is strong ({demand:0.0}), so a price above the median may still sell, but it exceeds the competitive price ({competitive:0.00}).";
            whatChanges = $"If purchase cost falls to {breakEven:0.00} or below, the price could drop to the competitive {competitive:0.00} and still meet margin.";
        }
        else
        {
            status = "DoNotStock"; recommended = floor;
            explanation = $"Do not stock at a competitive price: the floor price ({floor:0.00}) exceeds what the market will bear (competitive {competitive:0.00}) and demand ({demand:0.0}) is not strong enough to justify it.";
            whatChanges = $"Stock only if purchase cost falls to {breakEven:0.00} or below, or if demand rises sharply.";
        }
        trace.Add($"Decision: {status} at {recommended:0.00}.");

        int confidence = book.CompetitorPrices.Count switch { 1 => 45, 2 => 65, _ => 80 };
        if (book.RatingsCount > 1000) confidence += 10;
        if (trend.DataPoints >= 3) confidence += 5;
        confidence = Math.Clamp(confidence, 0, 95);
        if (status == "InsufficientData") confidence = 20;

        return new BookLead
        {
            Title = book.Title, Authors = book.Authors, Description = book.Description, Genres = book.Genres,
            Rating = book.Rating, RatingsCount = book.RatingsCount, CompetitorMedian = median,
            CompetitorPrices = book.CompetitorPrices, DemandScore = Math.Round(demand, 1),
            TrendPercent = Math.Round(trend.TrendPercent, 2), TrendDataPoints = trend.DataPoints,
            PurchaseCostUsed = purchase, PurchaseCostWasEstimated = estimated, ShippingCost = cost.ShippingCost,
            FeePercent = cost.FeePercent, TargetMarginPercent = cost.TargetMarginPercent, FloorPrice = floor,
            CompetitivePrice = competitive, RecommendedPrice = recommended, BreakEvenPurchaseCost = breakEven,
            Status = status, Confidence = confidence, Explanation = explanation, WhatWouldChangeIt = whatChanges,
            Trace = trace, IsSample = book.IsSample, ReasoningProvider = providerName,
        };
    }
}
