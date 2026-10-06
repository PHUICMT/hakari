using Microsoft.Data.Sqlite;

namespace Hakari.Core.Querying;

internal static class UsageFilterSql
{
    private const string SourceParameterPrefix = "$source";

    public static string BuildWhereClause(UsageFilter filter, SqliteCommand command)
    {
        var conditions = new List<string>();

        if (filter.From is { } from)
        {
            conditions.Add("timestamp_ms >= $from");
            command.Parameters.AddWithValue("$from", from.ToUnixTimeMilliseconds());
        }

        if (filter.To is { } to)
        {
            conditions.Add("timestamp_ms < $to");
            command.Parameters.AddWithValue("$to", to.ToUnixTimeMilliseconds());
        }

        if (filter.Model is { } model)
        {
            conditions.Add("model = $model");
            command.Parameters.AddWithValue("$model", model);
        }

        if (filter.Project is { } project)
        {
            conditions.Add("project = $project");
            command.Parameters.AddWithValue("$project", project);
        }

        if (filter.AccountId is { } accountId)
        {
            conditions.Add($"{AccountSql.AccountOfRecord} = $accountId");
            command.Parameters.AddWithValue("$accountId", accountId);
        }

        if (filter.IsSidechain is { } isSidechain)
        {
            conditions.Add("is_sidechain = $sidechain");
            command.Parameters.AddWithValue("$sidechain", isSidechain ? 1 : 0);
        }

        if (filter.SessionId is { } sessionId)
        {
            conditions.Add("session_id = $session");
            command.Parameters.AddWithValue("$session", sessionId);
        }

        if (filter.SourceIds is { Count: > 0 } sourceIds)
        {
            conditions.Add(BuildSourceCondition(sourceIds, command));
        }

        return conditions.Count == 0 ? string.Empty : "WHERE " + string.Join(" AND ", conditions);
    }

    private static string BuildSourceCondition(
        IReadOnlyCollection<string> sourceIds,
        SqliteCommand command)
    {
        var parameterNames = sourceIds.Select((sourceId, position) =>
        {
            var parameterName = $"{SourceParameterPrefix}{position}";
            command.Parameters.AddWithValue(parameterName, sourceId);
            return parameterName;
        });

        return $"source_id IN ({string.Join(", ", parameterNames)})";
    }
}
