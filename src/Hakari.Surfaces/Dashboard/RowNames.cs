using System.Globalization;
using Hakari.Core.Localization;
using Hakari.Core.Querying;

namespace Hakari.Surfaces.Dashboard;

/// <summary>
/// What a table row is called, in two lines: the thing itself, then where it belongs. A
/// project shows its folder name over the full path; a branch shows the project it is in.
/// </summary>
internal static class RowNames
{
    private const int ShortSessionLength = 8;
    private const string Separator = " · ";
    private const string TimeFormat = "HH:mm";
    private const string DayFormat = "MMM d";
    private static readonly char[] PathSeparators = ['\\', '/'];

    public static (string Title, string? Detail) Of(GroupBy groupBy, UsageSummary row) =>
        groupBy switch
        {
            GroupBy.Project => (FolderName(row.Key), row.Key.Length == 0 ? null : row.Key),
            GroupBy.ProjectBranch => Branch(row.Key),
            GroupBy.ProjectSession => Session(row),
            _ => (row.Key.Length == 0 ? Texts.Get("dashboard.unknown") : row.Key, null),
        };

    /// <summary>Inside its project's group the project is already said, so only the rest.</summary>
    public static (string Title, string? Detail) InGroup(GroupBy groupBy, UsageSummary row)
    {
        var (title, _) = Of(groupBy, row);
        if (groupBy != GroupBy.ProjectSession)
        {
            return (title, null);
        }

        var session = GroupKeys.Split(row.Key).Item;
        return (title, ShortId(session));
    }

    /// <summary>A project group's header: the folder name over its full path.</summary>
    public static (string Title, string? Detail) Project(string path) =>
        (FolderName(path), path.Length == 0 ? null : path);

    private static (string Title, string? Detail) Branch(string key)
    {
        var (project, branch) = GroupKeys.Split(key);
        var title = branch.Length == 0 ? Texts.Get("dashboard.noBranch") : branch;
        return (title, ProjectLine(project));
    }

    /// <summary>When it ran is what tells sessions apart; the id is only a short tail.</summary>
    private static (string Title, string? Detail) Session(UsageSummary row)
    {
        var (project, session) = GroupKeys.Split(row.Key);
        var first = row.FirstSeen.ToLocalTime();
        var last = row.LastSeen.ToLocalTime();
        var culture = Texts.Culture;
        var title = first.ToString(DayFormat, culture) + Separator
            + first.ToString(TimeFormat, CultureInfo.InvariantCulture) + " – "
            + last.ToString(TimeFormat, CultureInfo.InvariantCulture);
        var shortId = ShortId(session);
        return (title, ProjectLine(project) + Separator + shortId);
    }

    private static string ProjectLine(string project) =>
        project.Length == 0
            ? Texts.Get("dashboard.unknown")
            : FolderName(project) + Separator + project;

    private static string ShortId(string session) =>
        session.Length > ShortSessionLength ? session[..ShortSessionLength] : session;

    private static string FolderName(string path)
    {
        if (path.Length == 0)
        {
            return Texts.Get("dashboard.unknown");
        }

        var trimmed = path.TrimEnd(PathSeparators);
        var index = trimmed.LastIndexOfAny(PathSeparators);
        return index < 0 ? trimmed : trimmed[(index + 1)..];
    }
}
