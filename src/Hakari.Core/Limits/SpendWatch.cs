using System.Globalization;
using Hakari.Core.Indexing;
using Hakari.Core.Querying;

namespace Hakari.Core.Limits;

/// <summary>
/// What spending crossed: a day's or a month's budget, a project's month, or one costly
/// session.
/// </summary>
public enum SpendAlertKind
{
    DailyBudget,
    MonthlyBudget,
    UnusualSession,
    ProjectBudget,
}

/// <param name="Spent">The day's, the month's, the project's or the session's cost.</param>
/// <param name="Mark">The budget, or for a session what a usual one costs.</param>
/// <param name="Session">The session's key for an unusual one; the project's folder for a
/// project budget.</param>
public sealed record SpendAlert(SpendAlertKind Kind, decimal Spent, decimal Mark, string? Session);

/// <summary>
/// Watches spending against the budgets and for a session far costlier than usual. Each is
/// told once: a budget once a day or month, a session once ever. What was told is kept in the
/// index, so a restart does not tell it again.
/// </summary>
public sealed class SpendWatch(IndexStore store)
{
    public const decimal UnusualFactor = 3;
    public const int SessionsForUsual = 5;

    private const string DayOption = "spendAlert.day";
    private const string MonthOption = "spendAlert.month";
    private const string SessionsOption = "spendAlert.sessions";
    private const string ProjectsOption = "spendAlert.projects";
    private const char LineSeparator = '\n';
    private const int RememberedSessions = 50;
    private const char Separator = ',';
    private static readonly TimeSpan UsualWindow = TimeSpan.FromDays(7);
    private static readonly TimeSpan RecentSession = TimeSpan.FromHours(1);

    public IReadOnlyList<SpendAlert> Check(
        UsageQuery query,
        decimal? dailyBudget,
        decimal? monthlyBudget,
        bool watchSessions,
        DateTimeOffset now,
        IReadOnlyDictionary<string, decimal>? projectBudgets = null)
    {
        var alerts = new List<SpendAlert>();
        var day = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var month = now.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        if (dailyBudget is { } perDay && store.GetOption(DayOption) != day)
        {
            var spent = query.Total(new UsageFilter(From: TimePeriods.StartOfToday(now))).Cost;
            if (spent >= perDay)
            {
                store.SetOption(DayOption, day);
                alerts.Add(new SpendAlert(SpendAlertKind.DailyBudget, spent, perDay, null));
            }
        }

        if (monthlyBudget is { } perMonth && store.GetOption(MonthOption) != month)
        {
            var spent = query.Total(new UsageFilter(From: TimePeriods.StartOfMonth(now))).Cost;
            if (spent >= perMonth)
            {
                store.SetOption(MonthOption, month);
                alerts.Add(new SpendAlert(SpendAlertKind.MonthlyBudget, spent, perMonth, null));
            }
        }

        if (watchSessions)
        {
            alerts.AddRange(UnusualSessions(query, now));
        }

        if (projectBudgets is { Count: > 0 })
        {
            alerts.AddRange(ProjectsOverBudget(query, projectBudgets, month, now));
        }

        return alerts;
    }

    /// <summary>
    /// Each project whose month passed its budget, once a month. The note of which were told
    /// starts with the month, so a new month starts a new list.
    /// </summary>
    private IEnumerable<SpendAlert> ProjectsOverBudget(
        UsageQuery query,
        IReadOnlyDictionary<string, decimal> budgets,
        string month,
        DateTimeOffset now)
    {
        var saved = (store.GetOption(ProjectsOption) ?? string.Empty).Split(LineSeparator);
        var told = saved[0] == month ? saved.Skip(1).ToList() : [];
        var costs = query.Summarize(
                new UsageFilter(From: TimePeriods.StartOfMonth(now)),
                GroupBy.Project)
            .ToDictionary(project => project.Key, project => project.Cost);
        foreach (var (project, budget) in budgets)
        {
            var spent = costs.GetValueOrDefault(project);
            if (budget <= 0 || spent < budget || told.Contains(project))
            {
                continue;
            }

            told.Add(project);
            store.SetOption(ProjectsOption, string.Join(LineSeparator, [month, .. told]));
            yield return new SpendAlert(SpendAlertKind.ProjectBudget, spent, budget, project);
        }
    }

    /// <summary>
    /// A session active in the last hour that costs more than three times the median session
    /// of the past week, once there are enough sessions to know what usual is.
    /// </summary>
    private IEnumerable<SpendAlert> UnusualSessions(UsageQuery query, DateTimeOffset now)
    {
        var week = query.Summarize(new UsageFilter(From: now - UsualWindow), GroupBy.Session)
            .Where(session => session.Cost > 0)
            .ToList();
        if (week.Count < SessionsForUsual)
        {
            yield break;
        }

        var usual = Median(week.Select(session => session.Cost));
        var told = (store.GetOption(SessionsOption) ?? string.Empty)
            .Split(Separator, StringSplitOptions.RemoveEmptyEntries)
            .ToList();
        foreach (var session in week.Where(session =>
            session.LastSeen >= now - RecentSession
            && session.Cost > usual * UnusualFactor
            && !told.Contains(session.Key)))
        {
            told.Add(session.Key);
            store.SetOption(
                SessionsOption,
                string.Join(Separator, told.TakeLast(RememberedSessions)));
            yield return new SpendAlert(
                SpendAlertKind.UnusualSession, session.Cost, usual, session.Key);
        }
    }

    public static decimal Median(IEnumerable<decimal> values)
    {
        var sorted = values.Order().ToList();
        if (sorted.Count == 0)
        {
            return 0;
        }

        var middle = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
    }
}
