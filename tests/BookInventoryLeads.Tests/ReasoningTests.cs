using BookInventoryLeads.Core;
using Xunit;

namespace BookInventoryLeads.Tests;

public class ReasoningTests
{
    [Fact]
    public void Median_OddAndEven()
    {
        Assert.Equal(20m, Reasoning.Median(new List<decimal> { 10, 20, 30 }));
        Assert.Equal(25m, Reasoning.Median(new List<decimal> { 10, 20, 30, 40 }));
        Assert.Null(Reasoning.Median(new List<decimal>()));
    }

    [Fact]
    public void Recommend_WhenCompetitiveCoversFloor()
    {
        var book = new BookCandidate { Title = "T", Authors = new() { "A" }, Rating = 4.5, RatingsCount = 5000,
            CompetitorPrices = new() { new(20m, "USD", "Google Books listPrice", false) } };
        var lead = Reasoning.Recommend(book, new CostModel { PurchaseCost = 8m, ShippingCost = 3m, FeePercent = 0.12m, TargetMarginPercent = 0.20m }, new PriceTrend(0, 1, 20m, 20m), "test");
        Assert.Equal("Recommend", lead.Status);
        Assert.Equal(19.60m, lead.RecommendedPrice); // 20 * 0.98
    }

    [Fact]
    public void DoNotStock_WhenFloorExceedsMarketAndDemandLow()
    {
        var book = new BookCandidate { Title = "T", Authors = new() { "A" }, Rating = 2.5, RatingsCount = 2,
            CompetitorPrices = new() { new(10m, "USD", "Google Books listPrice", false) } };
        var lead = Reasoning.Recommend(book, new CostModel { PurchaseCost = 9m, ShippingCost = 4m, FeePercent = 0.12m, TargetMarginPercent = 0.20m }, new PriceTrend(0, 1, 10m, 10m), "test");
        Assert.Equal("DoNotStock", lead.Status);
        Assert.NotNull(lead.BreakEvenPurchaseCost);
    }

    [Fact]
    public void InsufficientData_WhenNoPrice()
    {
        var book = new BookCandidate { Title = "T", Authors = new() { "A" } };
        var lead = Reasoning.Recommend(book, new CostModel(), new PriceTrend(0, 0, null, null), "test");
        Assert.Equal("InsufficientData", lead.Status);
        Assert.Null(lead.RecommendedPrice);
    }

    [Fact]
    public void Dedupe_MergesEditions()
    {
        var books = new List<BookCandidate>
        {
            new() { Title = "Dune", Authors = new() { "Frank Herbert" }, CompetitorPrices = new() { new(20m, "USD", "A", false) } },
            new() { Title = "Dune", Authors = new() { "Frank Herbert" }, CompetitorPrices = new() { new(18m, "USD", "B", false) } },
        };
        var merged = Reasoning.Dedupe(books);
        Assert.Single(merged);
        Assert.Equal(2, merged[0].CompetitorPrices.Count);
    }
}
