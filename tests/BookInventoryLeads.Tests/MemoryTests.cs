using BookInventoryLeads.Core;
using Xunit;

namespace BookInventoryLeads.Tests;

/// <summary>Conformance check for the H-MEM-like price-history engine.</summary>
public class MemoryTests
{
    [Fact]
    public void Trend_RisesFromFirstToLast()
    {
        var db = Path.Combine(Path.GetTempPath(), $"memtest-{Guid.NewGuid()}.db");
        try
        {
            var store = new PriceHistoryStore(db);
            var today = DateTime.UtcNow.Date;
            store.Record("book|author", today.AddDays(-2), 10m, 50);
            store.Record("book|author", today.AddDays(-1), 11m, 55);
            store.Record("book|author", today, 12m, 60);
            var trend = store.GetTrend("book|author", 3);
            Assert.Equal(3, trend.DataPoints);
            Assert.Equal(20, trend.TrendPercent); // (12-10)/10*100
            Assert.Equal(12m, trend.LatestMedian);
        }
        finally { if (File.Exists(db)) File.Delete(db); }
    }

    [Fact]
    public void Trend_SinglePointIsZero()
    {
        var db = Path.Combine(Path.GetTempPath(), $"memtest-{Guid.NewGuid()}.db");
        try
        {
            var store = new PriceHistoryStore(db);
            store.Record("b|a", DateTime.UtcNow.Date, 15m, 50);
            var trend = store.GetTrend("b|a", 7);
            Assert.Equal(1, trend.DataPoints);
            Assert.Equal(0, trend.TrendPercent);
        }
        finally { if (File.Exists(db)) File.Delete(db); }
    }
}
