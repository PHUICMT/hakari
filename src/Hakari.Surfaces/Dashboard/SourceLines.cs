using Hakari.Core.Accounts;
using Hakari.Core.Indexing;
using Hakari.Core.Localization;
using Hakari.Core.Presentation;
using Hakari.Core.Querying;
using Hakari.Core.Settings;
using Hakari.Surfaces.Flyout;

namespace Hakari.Surfaces.Dashboard;

/// <summary>Reads every place usage comes from, with what it holds and what it cost.</summary>
internal sealed class SourceLines
{
    private const string Dash = "—";
    private static readonly TimeSpan LiveWithin = TimeSpan.FromMinutes(10);

    private readonly UsageQuery query;
    private readonly AccountRepository repository;
    private readonly HakariSettings settings;
    private readonly DateTimeOffset now;
    private readonly Dictionary<string, SourceActivity> activity;
    private readonly Dictionary<string, SourceFootprint> footprint;
    private readonly Dictionary<string, decimal> costs;
    private readonly Dictionary<string, AccountInfo> accounts;

    public SourceLines(
        UsageQuery query,
        IndexStore store,
        AccountRepository repository,
        HakariSettings settings,
        DateTimeOffset now)
    {
        this.query = query;
        this.repository = repository;
        this.settings = settings;
        this.now = now;
        activity = SourceActivity.Load(store).ToDictionary(source => source.SourceId);
        footprint = SourceFootprint.Load(store).ToDictionary(source => source.SourceId);
        costs = query.Summarize(UsageFilter.Everything, GroupBy.Source)
            .ToDictionary(summary => summary.Key, summary => summary.Cost);
        accounts = repository.ListAccounts().ToDictionary(account => account.AccountId);
    }

    /// <summary>The ones in use first, then the largest.</summary>
    public List<SourceLine> Load() =>
    [
        .. activity.Keys.Union(footprint.Keys)
            .Select(Line)
            .OrderByDescending(line => line.IsLive)
            .ThenByDescending(line => line.Bytes),
    ];

    private SourceLine Line(string sourceId)
    {
        TimeSpan? age = activity.TryGetValue(sourceId, out var used) ? now - used.LastUsage : null;
        var isLive = age < LiveWithin;
        var files = footprint.GetValueOrDefault(sourceId);
        return new SourceLine(
            SourceNames.Display(sourceId),
            AccountName(sourceId),
            Status(age, isLive),
            isLive,
            files?.Files ?? 0,
            files?.Bytes ?? 0,
            MoneyText.Format(costs.GetValueOrDefault(sourceId), query.Currency));
    }

    private string AccountName(string sourceId)
    {
        var period = repository.LatestPeriod(sourceId);
        return period is not null && accounts.TryGetValue(period.AccountId, out var account)
            ? AccountLabels.Full(account, settings.NicknameOf(account.AccountId))
            : Dash;
    }

    private static string Status(TimeSpan? age, bool isLive) =>
        isLive ? Texts.Get("dashboard.sources.live")
            : age is { } past ? FlyoutDataLoader.LastUsedText(past)
            : Dash;
}
