using Hakari.Core.Indexing;
using Hakari.Core.Pricing;
using Hakari.Core.Usage;
using Microsoft.Data.Sqlite;

namespace Hakari.Core.Querying;

public sealed class UsageQuery(IndexStore store, PricingTable pricing)
{
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

        return [.. summariesByKey.Values.OrderByDescending(summary => summary.Cost)];
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
            if (pricing.Find(model) is null)
            {
                unpricedModels.Add(model);
            }
        }

        return unpricedModels;
    }

    private static string BuildSummarySql(string groupExpression, string whereClause) => $"""
        SELECT
            {groupExpression} AS group_key,
            model,
            speed,
            count(*),
            sum(input_tokens),
            sum(output_tokens),
            sum(cache_write_five_minutes),
            sum(cache_write_one_hour),
            sum(cache_read_tokens),
            min(timestamp_ms),
            max(timestamp_ms)
        FROM usage_records
        {whereClause}
        GROUP BY group_key, model, speed
        """;

    private UsageSummary ReadPricedSummary(SqliteDataReader reader)
    {
        var model = reader.GetString(1);
        var speed = reader.GetString(2);
        var tokens = new TokenCounts(
            Input: reader.GetInt64(4),
            Output: reader.GetInt64(5),
            CacheWriteFiveMinutes: reader.GetInt64(6),
            CacheWriteOneHour: reader.GetInt64(7),
            CacheRead: reader.GetInt64(8));
        var cost = pricing.Cost(model, speed, tokens);

        return new UsageSummary(
            Key: reader.GetString(0),
            Messages: reader.GetInt64(3),
            Tokens: tokens,
            Cost: cost ?? 0,
            HasUnpricedModels: cost is null,
            FirstSeen: DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(9)),
            LastSeen: DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(10)));
    }
}
