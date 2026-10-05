namespace Hakari.Core.Querying;

internal static class GroupByExpressions
{
    private const string LocalSeconds = "timestamp_ms / 1000, 'unixepoch', 'localtime'";

    public static string For(GroupBy groupBy) => groupBy switch
    {
        GroupBy.None => "''",
        GroupBy.Model => "model",
        GroupBy.Source => "source_id",
        GroupBy.Day => $"date({LocalSeconds})",
        GroupBy.Hour => $"strftime('%Y-%m-%d %H:00', {LocalSeconds})",
        GroupBy.Project => "coalesce(project, '')",
        GroupBy.Session => "session_id",
        GroupBy.Branch => "coalesce(git_branch, '')",
        GroupBy.Account => AccountSql.AccountOfRecord,
        _ => throw new ArgumentOutOfRangeException(nameof(groupBy), groupBy, null),
    };
}
