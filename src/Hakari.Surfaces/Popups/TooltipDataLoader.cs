using Hakari.Core.Accounts;
using Hakari.Core.Configuration;
using Hakari.Core.Indexing;
using Hakari.Core.Limits;
using Hakari.Core.Localization;
using Hakari.Core.Presentation;
using Hakari.Core.Presentation.Widget;
using Hakari.Core.Pricing;
using Hakari.Core.Querying;
using Hakari.Core.Settings;
using Hakari.Core.Sources;
using Hakari.Surfaces.Flyout;
using Microsoft.Data.Sqlite;

namespace Hakari.Surfaces.Popups;

/// <summary>What the hover card says per shown account, read like the flyout.</summary>
internal static class TooltipDataLoader
{
    private const string TitleSeparator = " · ";

    public static IReadOnlyList<TooltipAccount> Load()
    {
        if (!File.Exists(HakariPaths.DefaultIndexPath))
        {
            return [];
        }

        try
        {
            using var store = IndexStore.OpenReadOnly(HakariPaths.DefaultIndexPath);
            return Read(store, SettingsStore.Default.Load(), DateTimeOffset.Now);
        }
        catch (Exception exception) when (exception is SqliteException or IOException)
        {
            return [];
        }
    }

    private static List<TooltipAccount> Read(
        IndexStore store,
        HakariSettings settings,
        DateTimeOffset now)
    {
        var query = new UsageQuery(
            store,
            PricingSources.LoadCurrent(),
            FlyoutDataLoader.StoredConverter(store, settings));
        var cache = new LimitCache(store);
        var known = new AccountRepository(store).ListAccounts()
            .Select(account => (Account: account, Snapshot: cache.Load(account.AccountId)))
            .Where(entry => entry.Snapshot is not null)
            .Select(entry => (entry.Account, Snapshot: entry.Snapshot!.ProjectedTo(now)));
        var arranged = AccountArrangement.Arrange(
            known,
            entry => entry.Account.AccountId,
            entry => LimitPriority.Rank(entry.Snapshot),
            settings);
        var cards = arranged
            .Select(entry => Card(entry.Account, entry.Snapshot, settings, query, now))
            .ToList();
        if (cards.Count > 1 && settings.AccountsMode == MultiAccountMode.TakeTurns)
        {
            var last = cards[^1];
            cards[^1] = last with
            {
                Updated = last.Updated + TitleSeparator + Texts.Get("tooltip.scroll"),
            };
        }

        if (State(settings, query, store, now) is { } state)
        {
            cards.Insert(0, new TooltipAccount(state, [], string.Empty));
        }

        return cards;
    }

    private static TooltipAccount Card(
        AccountInfo account,
        LimitSnapshot snapshot,
        HakariSettings settings,
        UsageQuery query,
        DateTimeOffset now)
    {
        var title = AccountLabels.Full(account, settings.NicknameOf(account.AccountId))
            + TitleSeparator + PlanNames.Short(account.Plan);
        var lines = snapshot.Limits.Select(limit => LimitLine(limit, now)).ToList();
        lines.AddRange(MoneyLines(query, account.AccountId, now));
        return new TooltipAccount(title, lines, Updated(snapshot, now));
    }

    private static string LimitLine(UsageLimit limit, DateTimeOffset now)
    {
        var name = LimitNames.Long(limit);
        var percent = limit.Percent >= LimitForecaster.FullPercent
            ? Texts.Get("flyout.full")
            : $"{limit.Percent}%";
        return limit.ResetsAt is { } resetsAt
            ? Texts.Format("tooltip.limit", name, percent, ResetText.Clock(resetsAt, now))
            : $"{name} {percent}";
    }

    private static readonly TimeSpan ActiveWindow = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Why the widget reads as it does when it is not plain numbers: paused, or still
    /// reading the logs for the first time.
    /// </summary>
    private static string? State(
        HakariSettings settings,
        UsageQuery query,
        IndexStore store,
        DateTimeOffset now)
    {
        if (settings.Paused)
        {
            return Texts.Get("tooltip.paused");
        }

        if (settings.PausedUntil is { } until && until > now)
        {
            return Texts.Format(
                "tooltip.pausedUntil", until.ToLocalTime().ToString("HH:mm", Texts.Culture));
        }

        if (query.HasAny())
        {
            return null;
        }

        return Texts.Get(CurrentSources.FoundNone(store)
            ? "flyout.notice.noLogs"
            : "flyout.notice.reading");
    }

    /// <summary>"Today ฿… · this month ฿…", then "Burn ฿…/h · 2 active sessions".</summary>
    private static IEnumerable<string> MoneyLines(
        UsageQuery query,
        string accountId,
        DateTimeOffset now)
    {
        decimal Since(DateTimeOffset from) =>
            query.Total(new UsageFilter(From: from, AccountId: accountId)).Cost;

        yield return Texts.Format(
            "tooltip.money",
            MoneyText.Format(Since(TimePeriods.StartOfToday(now)), query.Currency),
            MoneyText.Format(Since(TimePeriods.StartOfMonth(now)), query.Currency));
        var active = query.Summarize(
            new UsageFilter(From: now - ActiveWindow, AccountId: accountId),
            GroupBy.Session).Count;
        yield return Texts.Format(
            "tooltip.burn",
            MoneyText.Format(Since(now.AddHours(-1)), query.Currency),
            active);
    }

    private static string Updated(LimitSnapshot snapshot, DateTimeOffset now)
    {
        var age = now - snapshot.FetchedAt;
        return age < TimeSpan.FromMinutes(1)
            ? Texts.Get("flyout.updatedNow")
            : Texts.Format("flyout.updatedAgo", AgeText.Format(age));
    }
}
