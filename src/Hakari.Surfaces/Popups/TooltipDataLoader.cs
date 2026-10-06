using Hakari.Core.Accounts;
using Hakari.Core.Configuration;
using Hakari.Core.Indexing;
using Hakari.Core.Limits;
using Hakari.Core.Localization;
using Hakari.Core.Presentation;
using Hakari.Core.Pricing;
using Hakari.Core.Querying;
using Hakari.Core.Settings;
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
        return
        [
            .. arranged.Select(entry =>
                Card(entry.Account, entry.Snapshot, settings, query, now)),
        ];
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
        lines.Add(MoneyLine(query, account.AccountId, now));
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

    private static string MoneyLine(UsageQuery query, string accountId, DateTimeOffset now)
    {
        decimal Since(DateTimeOffset from) =>
            query.Total(new UsageFilter(From: from, AccountId: accountId)).Cost;

        return Texts.Format(
            "tooltip.money",
            MoneyText.Format(Since(TimePeriods.StartOfToday(now)), query.Currency),
            MoneyText.Format(Since(TimePeriods.StartOfMonth(now)), query.Currency),
            MoneyText.Format(Since(now.AddHours(-1)), query.Currency));
    }

    private static string Updated(LimitSnapshot snapshot, DateTimeOffset now)
    {
        var age = now - snapshot.FetchedAt;
        return age < TimeSpan.FromMinutes(1)
            ? Texts.Get("flyout.updatedNow")
            : Texts.Format("flyout.updatedAgo", AgeText.Format(age));
    }
}
