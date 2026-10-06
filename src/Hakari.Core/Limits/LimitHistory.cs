using Hakari.Core.Indexing;

namespace Hakari.Core.Limits;

/// <summary>
/// Keeps how full each limit was over the last 90 days, so a chart can show where the week
/// went. A reading is kept when the percent changed, or once an hour while it stays the same.
/// </summary>
public sealed class LimitHistory(IndexStore store)
{
    public static readonly TimeSpan Keep = TimeSpan.FromDays(90);
    private static readonly TimeSpan SteadyGap = TimeSpan.FromHours(1);

    private const string LastSql = """
        SELECT taken_at_ms, percent FROM limit_history
        WHERE account_id = $accountId AND kind = $kind
        ORDER BY taken_at_ms DESC LIMIT 1
        """;

    private const string InsertSql = """
        INSERT OR REPLACE INTO limit_history (account_id, kind, taken_at_ms, percent)
        VALUES ($accountId, $kind, $takenAtMs, $percent)
        """;

    private const string LoadSql = """
        SELECT taken_at_ms, percent FROM limit_history
        WHERE account_id = $accountId AND kind = $kind AND taken_at_ms >= $sinceMs
        ORDER BY taken_at_ms
        """;

    private const string PruneSql = "DELETE FROM limit_history WHERE taken_at_ms < $beforeMs";

    /// <summary>Live readings only: an old one would be written at the wrong time.</summary>
    public void Record(string accountId, LimitSnapshot snapshot)
    {
        if (snapshot.Freshness != LimitFreshness.Live)
        {
            return;
        }

        foreach (var limit in snapshot.Limits)
        {
            Record(accountId, limit.Kind, snapshot.FetchedAt, limit.Percent);
        }
    }

    public IReadOnlyList<LimitReading> Load(string accountId, string kind, DateTimeOffset since)
    {
        using var command = store.Connection.CreateCommand();
        command.CommandText = LoadSql;
        command.Parameters.AddWithValue("$accountId", accountId);
        command.Parameters.AddWithValue("$kind", kind);
        command.Parameters.AddWithValue("$sinceMs", since.ToUnixTimeMilliseconds());
        using var reader = command.ExecuteReader();
        var readings = new List<LimitReading>();
        while (reader.Read())
        {
            readings.Add(new LimitReading(
                DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(0)),
                reader.GetDouble(1)));
        }

        return readings;
    }

    public void Prune(DateTimeOffset now)
    {
        using var command = store.Connection.CreateCommand();
        command.CommandText = PruneSql;
        command.Parameters.AddWithValue("$beforeMs", (now - Keep).ToUnixTimeMilliseconds());
        command.ExecuteNonQuery();
    }

    private void Record(string accountId, string kind, DateTimeOffset at, double percent)
    {
        if (Last(accountId, kind) is { } last
            && Math.Abs(last.Percent - percent) < double.Epsilon
            && at - last.At < SteadyGap)
        {
            return;
        }

        using var command = store.Connection.CreateCommand();
        command.CommandText = InsertSql;
        command.Parameters.AddWithValue("$accountId", accountId);
        command.Parameters.AddWithValue("$kind", kind);
        command.Parameters.AddWithValue("$takenAtMs", at.ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue("$percent", percent);
        command.ExecuteNonQuery();
    }

    private LimitReading? Last(string accountId, string kind)
    {
        using var command = store.Connection.CreateCommand();
        command.CommandText = LastSql;
        command.Parameters.AddWithValue("$accountId", accountId);
        command.Parameters.AddWithValue("$kind", kind);
        using var reader = command.ExecuteReader();
        return reader.Read()
            ? new LimitReading(
                DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(0)),
                reader.GetDouble(1))
            : null;
    }
}
