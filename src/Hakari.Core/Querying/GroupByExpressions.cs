namespace Hakari.Core.Querying;

internal static class GroupByExpressions
{
    private const string LocalSeconds = "timestamp_ms / 1000, 'unixepoch', 'localtime'";

    /// <summary>
    /// A Windows folder recorded once as "D:\x" and once as "d:\x" is the same folder, so the
    /// drive letter is written in capitals.
    /// </summary>
    private const string Project =
        "coalesce(CASE WHEN project LIKE '_:%' "
        + "THEN upper(substr(project, 1, 1)) || substr(project, 2) ELSE project END, '')";

    private const string Separator = "char(31)";

    public static string For(GroupBy groupBy) => groupBy switch
    {
        GroupBy.None => "''",
        GroupBy.Model => "model",
        GroupBy.Source => "source_id",
        GroupBy.Day => $"date({LocalSeconds})",
        GroupBy.Hour => $"strftime('%Y-%m-%d %H:00', {LocalSeconds})",
        GroupBy.Project => Project,
        GroupBy.Session => "session_id",
        GroupBy.Branch => "coalesce(git_branch, '')",
        GroupBy.Account => AccountSql.AccountOfRecord,
        GroupBy.ProjectBranch => $"{Project} || {Separator} || coalesce(git_branch, '')",
        GroupBy.WeekdayHour => $"strftime('%w %H', {LocalSeconds})",
        GroupBy.ProjectSession => $"{Project} || {Separator} || session_id",
        _ => throw new ArgumentOutOfRangeException(nameof(groupBy), groupBy, null),
    };
}
