namespace Hakari.Core.Querying;

internal static class GroupByExpressions
{
    private const string LocalSeconds = "timestamp_ms / 1000, 'unixepoch', 'localtime'";

    /// <summary>
    /// A Windows folder recorded once as "D:\x" and once as "d:\x" is the same folder, so the
    /// drive letter is written in capitals.
    /// </summary>
    internal const string Project =
        "coalesce(CASE WHEN project LIKE '_:%' "
        + "THEN upper(substr(project, 1, 1)) || substr(project, 2) ELSE project END, '')";

    private const string Separator = "char(31)";

    /// <param name="project">The project expression, such as one with joins applied.</param>
    public static string For(GroupBy groupBy, string project = Project) => groupBy switch
    {
        GroupBy.None => "''",
        GroupBy.Model => "model",
        GroupBy.Source => "source_id",
        GroupBy.Day => $"date({LocalSeconds})",
        GroupBy.Hour => $"strftime('%Y-%m-%d %H:00', {LocalSeconds})",
        GroupBy.Project => project,
        GroupBy.Session => "session_id",
        GroupBy.Branch => "coalesce(git_branch, '')",
        GroupBy.Account => AccountSql.AccountOfRecord,
        GroupBy.ProjectBranch => $"{project} || {Separator} || coalesce(git_branch, '')",
        GroupBy.WeekdayHour => $"strftime('%w %H', {LocalSeconds})",
        GroupBy.ProjectSession => $"{project} || {Separator} || session_id",
        _ => throw new ArgumentOutOfRangeException(nameof(groupBy), groupBy, null),
    };
}
