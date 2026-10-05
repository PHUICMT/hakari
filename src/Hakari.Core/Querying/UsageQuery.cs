using Hakari.Core.Currency;
using Hakari.Core.Indexing;
using Hakari.Core.Pricing;
using Hakari.Core.Usage;
using Microsoft.Data.Sqlite;

namespace Hakari.Core.Querying;

/// <param name="converter">Converts dollar costs per usage day; null keeps US dollars.</param>
public sealed class UsageQuery(
    IndexStore store,
    PricingTable pricing,
    CurrencyConverter? converter = null)
{
    private const long MillisecondsPerDay = 86_400_000;

    public string Currency => converter?.Currency ?? CurrencyCodes.Dollar;

    public DateOnly? FirstUsageDay()
    {
        using var command = store.Connection.CreateCommand();
        command.CommandText = "SELECT min(timestamp_ms) FROM usage_records";
        var firstTimestamp = command.ExecuteScalar();
        if (firstTimestamp is not long milliseconds)
        {
            return null;
        }

        var firstUsage = DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);
        return DateOnly.FromDateTime(firstUsage.UtcDateTime);
    }

    public IReadOnlyList<UsageSummary> Summarize(UsageFilter filter, GroupBy groupBy)
    {
        using var command = store.Connection.CreateCommand();
        var whereClause = UsageFilterSql.BuildWhereClause(filter, command);
        command.CommandText = BuildSummarySql(GroupByExpressions.For(groupBy), whereClause);

        var summariesByKey = new Dictionary<string, UsageSummary>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var summary = ReadPricedSummary(reader);
            summariesByKey[summary.Key] = summariesByKey.TryGetValue(summary.Key, out var existing)
                ? existing.Merge(summary)
                : summary;
        }

        return SortForDisplay(summariesByKey.Values, groupBy);
    }

    private static IReadOnlyList<UsageSummary> SortForDisplay(
        IEnumerable<UsageSummary> summaries,
        GroupBy groupBy)
    {
        var isTimeline = groupBy is GroupBy.Day or GroupBy.Hour;
        return isTimeline
            ? [.. summaries.OrderByDescending(summary => summary.Key, StringComparer.Ordinal)]
            : [.. summaries.OrderByDescending(summary => summary.Cost)];
    }

    public UsageSummary Total(UsageFilter filter) =>
        Summarize(filter, GroupBy.None).FirstOrDefault() ?? UsageSummary.Empty;

    public IReadOnlyList<string> FindUnpricedModels()
    {
        using var command = store.Connection.CreateCommand();
        command.CommandText = "SELECT DISTINCT model FROM usage_records";

        var unpricedModels = new List<string>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var model = reader.GetString(0);
            if (!pricing.IsPriced(model))
            {
                unpricedModels.Add(model);
            }
        }

        return unpricedModels;
    }

    /// <remarks>
    /// Rows are split by UTC day as well as by every pricing input, so each day is priced with
    /// the price that was in effect on that day.
    /// </remarks>
    private static string BuildSummarySql(string groupExpression, string whereClause) => $"""
        SELECT
            {groupExpression} AS group_key,
            model,
            speed,
            inference_geography,
            timestamp_ms / {MillisecondsPerDay} AS utc_day_number,
            count(*),
            sum(input_tokens),
            sum(output_tokens),
            sum(cache_write_five_minutes),
            sum(cache_write_one_hour),
            sum(cache_read_tokens),
            sum(web_search_requests),
            min(timestamp_ms),
            max(timestamp_ms)
        FROM usage_records
        {whereClause}
        GROUP BY group_key, model, speed, inference_geography, utc_day_number
        """;

    private UsageSummary ReadPricedSummary(SqliteDataReader reader)
    {
        var tokens = new TokenCounts(
            Input: reader.GetInt64(6),
            Output: reader.GetInt64(7),
            CacheWriteFiveMinutes: reader.GetInt64(8),
            CacheWriteOneHour: reader.GetInt64(9),
            CacheRead: reader.GetInt64(10));
        var webSearchRequests = reader.GetInt64(11);

        var utcDay = ToUtcDay(reader.GetInt64(4));
        var pricedUsage = new PricedUsage(
            Model: reader.GetString(1),
            Speed: reader.GetString(2),
            InferenceGeography: reader.IsDBNull(3) ? null : reader.GetString(3),
            UtcDay: utcDay,
            Tokens: tokens,
            WebSearchRequests: webSearchRequests);
        var dollarCost = pricing.Cost(pricedUsage);
        var cost = dollarCost is { } dollars && converter is not null
            ? converter.Convert(dollars, utcDay)
            : dollarCost;

        return new UsageSummary(
            Key: reader.GetString(0),
            Messages: reader.GetInt64(5),
            Tokens: tokens,
            WebSearchRequests: webSearchRequests,
            Cost: cost ?? 0,
            HasUnpricedModels: cost is null,
            FirstSeen: DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(12)),
            LastSeen: DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(13)));
    }

    private static DateOnly ToUtcDay(long dayNumber) =>
        DateOnly.FromDateTime(DateTime.UnixEpoch.AddDays(dayNumber));
}
