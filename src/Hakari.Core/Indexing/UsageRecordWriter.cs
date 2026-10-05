using Hakari.Core.Usage;
using Microsoft.Data.Sqlite;

namespace Hakari.Core.Indexing;

internal sealed class UsageRecordWriter : IDisposable
{
    /// <remarks>
    /// One response is often logged on several lines, and an early line can carry incomplete
    /// counts, so a repeated key keeps the largest value of each token column.
    /// </remarks>
    private const string UpsertSql = """
        INSERT INTO usage_records (
            deduplication_key, source_id, timestamp_ms, model, session_id, project,
            git_branch, is_sidechain, speed, input_tokens, output_tokens,
            cache_write_five_minutes, cache_write_one_hour, cache_read_tokens)
        VALUES (
            $deduplicationKey, $sourceId, $timestampMs, $model, $sessionId, $project,
            $gitBranch, $isSidechain, $speed, $inputTokens, $outputTokens,
            $cacheWriteFiveMinutes, $cacheWriteOneHour, $cacheReadTokens)
        ON CONFLICT (deduplication_key) DO UPDATE SET
            input_tokens = max(input_tokens, excluded.input_tokens),
            output_tokens = max(output_tokens, excluded.output_tokens),
            cache_write_five_minutes =
                max(cache_write_five_minutes, excluded.cache_write_five_minutes),
            cache_write_one_hour = max(cache_write_one_hour, excluded.cache_write_one_hour),
            cache_read_tokens = max(cache_read_tokens, excluded.cache_read_tokens)
        WHERE excluded.input_tokens > input_tokens
            OR excluded.output_tokens > output_tokens
            OR excluded.cache_write_five_minutes > cache_write_five_minutes
            OR excluded.cache_write_one_hour > cache_write_one_hour
            OR excluded.cache_read_tokens > cache_read_tokens
        """;

    private readonly SqliteCommand command;

    public UsageRecordWriter(SqliteConnection connection, SqliteTransaction transaction)
    {
        command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = UpsertSql;
        AddParameters();
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
        SetValue("$inputTokens", record.Tokens.Input);
        SetValue("$outputTokens", record.Tokens.Output);
        SetValue("$cacheWriteFiveMinutes", record.Tokens.CacheWriteFiveMinutes);
        SetValue("$cacheWriteOneHour", record.Tokens.CacheWriteOneHour);
        SetValue("$cacheReadTokens", record.Tokens.CacheRead);

        var changedRowCount = command.ExecuteNonQuery();
        return changedRowCount > 0;
    }

    public void Dispose() => command.Dispose();

    private void AddParameters()
    {
        string[] parameterNames =
        [
            "$deduplicationKey", "$sourceId", "$timestampMs", "$model", "$sessionId",
            "$project", "$gitBranch", "$isSidechain", "$speed", "$inputTokens",
            "$outputTokens", "$cacheWriteFiveMinutes", "$cacheWriteOneHour", "$cacheReadTokens",
        ];

        foreach (var parameterName in parameterNames)
        {
            command.Parameters.Add(new SqliteParameter(parameterName, DBNull.Value));
        }
    }

    private void SetValue(string parameterName, object? value) =>
        command.Parameters[parameterName].Value = value ?? DBNull.Value;
}
