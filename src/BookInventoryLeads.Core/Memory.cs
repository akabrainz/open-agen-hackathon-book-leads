using Microsoft.Data.Sqlite;

namespace BookInventoryLeads.Core;

/// <summary>
/// H-MEM-like price-history memory. Stores one competitor-median observation
/// per book per day, then derives a trend from history (not just today).
/// Backed by the same SQLite file as the local data layer.
/// </summary>
public class PriceHistoryStore
{
    private readonly string _connectionString;
    public PriceHistoryStore(string dbPath)
    {
        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS price_history (
                book_key TEXT NOT NULL,
                observed_date TEXT NOT NULL,
                competitor_median REAL NOT NULL,
                demand_score REAL NOT NULL,
                PRIMARY KEY (book_key, observed_date)
            );
            """;
        cmd.ExecuteNonQuery();
    }

    public void Record(string bookKey, DateTime date, decimal competitorMedian, double demandScore)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT OR REPLACE INTO price_history VALUES ($k, $d, $m, $s)";
        cmd.Parameters.AddWithValue("$k", bookKey);
        cmd.Parameters.AddWithValue("$d", date.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("$m", (double)competitorMedian);
        cmd.Parameters.AddWithValue("$s", demandScore);
        cmd.ExecuteNonQuery();
    }

    public PriceTrend GetTrend(string bookKey, int days)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT competitor_median FROM price_history
            WHERE book_key = $k AND observed_date >= $from
            ORDER BY observed_date ASC
            """;
        cmd.Parameters.AddWithValue("$k", bookKey);
        cmd.Parameters.AddWithValue("$from", DateTime.UtcNow.Date.AddDays(-Math.Max(0, days - 1)).ToString("yyyy-MM-dd"));
        var values = new List<decimal>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) values.Add((decimal)reader.GetDouble(0));
        if (values.Count < 2) return new PriceTrend(0, values.Count, values.Count == 1 ? values[0] : null, values.Count == 1 ? values[0] : null);
        var first = values.First(); var last = values.Last();
        double trend = first == 0 ? 0 : (double)((last - first) / first * 100);
        return new PriceTrend(Math.Round(trend, 2), values.Count, Math.Round(values.Average(), 2), last);
    }

    public bool HasAnyHistory(string bookKey) => GetTrend(bookKey, 365).DataPoints > 0;
}
