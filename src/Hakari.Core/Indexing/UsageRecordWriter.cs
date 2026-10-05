using Hakari.Core.Usage;
using Microsoft.Data.Sqlite;

namespace Hakari.Core.Indexing;

internal sealed class UsageRecordWriter : IDisposable
{
    /// <remarks>
    /// One response is often logged on several lines, and an early line can carry incomplete
    /// counts, so a repeated key keeps the largest value of each counted column.
    /// </remarks>
    private const string UpsertSql = """
        INSERT INTO usage_records (
            deduplication_key, source_id, timestamp_ms, model, session_id, project,
            git_branch, is_sidechain, speed, inference_geography, input_tokens, output_tokens,
            cache_write_five_minutes, cache_write_one_hour, cache_read_tokens,
            web_search_requests)
        VALUES (
            $deduplicationKey, $sourceId, $timestampMs, $model, $sessionId, $project,
            $gitBranch, $isSidechain, $speed, $inferenceGeography, $inputTokens, $outputTokens,
            $cacheWriteFiveMinutes, $cacheWriteOneHour, $cacheReadTokens,
            $webSearchRequests)
        ON CONFLICT (deduplication_key) DO UPDATE SET
            input_tokens = max(input_tokens, excluded.input_tokens),
            output_tokens = max(output_tokens, excluded.output_tokens),
            cache_write_five_minutes =
                max(cache_write_five_minutes, excluded.cache_write_five_minutes),
            cache_write_one_hour = max(cache_write_one_hour, excluded.cache_write_one_hour),
            cache_read_tokens = max(cache_read_tokens, excluded.cache_read_tokens),
            web_search_requests = max(web_search_requests, excluded.web_search_requests)
        WHERE excluded.input_tokens > input_tokens
            OR excluded.output_tokens > output_tokens
            OR excluded.cache_write_five_minutes > cache_write_five_minutes
            OR excluded.cache_write_one_hour > cache_write_one_hour
            OR excluded.cache_read_tokens > cache_read_tokens
            OR excluded.web_search_requests > web_search_requests
        """;

    private static readonly string[] ParameterNames =
    [
        "$deduplicationKey", "$sourceId", "$timestampMs", "$model", "$sessionId",
        "$project", "$gitBranch", "$isSidechain", "$speed", "$inferenceGeography",
        "$inputTokens", "$outputTokens", "$cacheWriteFiveMinutes", "$cacheWriteOneHour",
        "$cacheReadTokens", "$webSearchRequests",
    ];

    private readonly SqliteCommand command;

    public UsageRecordWriter(SqliteConnection connection, SqliteTransaction transaction)
    {
        command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = UpsertSql;
        foreach (var parameterName in ParameterNames)
        {
            command.Parameters.Add(new SqliteParameter(parameterName, DBNull.Value));
        }

        command.Prepare();
    }

    public bool TryUpsert(string sourceId, UsageRecord record)
    {
        SetValue("$deduplicationKey", record.DeduplicationKey);
        SetValue("$sourceId", sourceId);
        SetValue("$timestampMs", record.Timestamp.ToUnixTimeMilliseconds());
        SetValue("$model", record.Model);
        SetValue("$sessionId", record.SessionId);
        SetValue("$project", record.WorkingDirectory);
        SetValue("$gitBranch", record.GitBranch);
        SetValue("$isSidechain", record.IsSidechain);
        SetValue("$speed", record.Speed);
        SetValue("$inferenceGeography", record.InferenceGeography);
        SetValue("$inputTokens", record.Tokens.Input);
        SetValue("$outputTokens", record.Tokens.Output);
        SetValue("$cacheWriteFiveMinutes", record.Tokens.CacheWriteFiveMinutes);
        SetValue("$cacheWriteOneHour", record.Tokens.CacheWriteOneHour);
        SetValue("$cacheReadTokens", record.Tokens.CacheRead);
        SetValue("$webSearchRequests", record.WebSearchRequests);

        var changedRowCount = command.ExecuteNonQuery();
        return changedRowCount > 0;
    }

    public void Dispose() => command.Dispose();

    private void SetValue(string parameterName, object? value) =>
        command.Parameters[parameterName].Value = value ?? DBNull.Value;
}
