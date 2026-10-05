using Hakari.Core.Accounts;
using Hakari.Core.Configuration;
using Hakari.Core.Indexing;
using Hakari.Core.Limits;
using Hakari.Core.Presentation.Widget;
using Hakari.Core.Pricing;
using Hakari.Core.Querying;
using Hakari.Core.Settings;
using Microsoft.Data.Sqlite;

namespace Hakari.Surfaces.Settings;

/// <summary>
/// The same facts the taskbar uses, read from the index for the layout preview. Offline:
/// limits are the last ones Hakari.exe stored.
/// </summary>
internal static class WidgetFactsLoader
{
    private static readonly WidgetFacts Empty = new(0, 0, 0, "USD", []);

    public static WidgetFacts Load(DateTimeOffset now)
    {
        if (!File.Exists(HakariPaths.DefaultIndexPath))
        {
            return Empty;
        }

        try
        {
            using var store = IndexStore.OpenReadOnly(HakariPaths.DefaultIndexPath);
            return Read(store, SettingsStore.Default.Load(), now);
        }
        catch (Exception exception) when (exception is SqliteException or IOException)
        {
            return Empty;
        }
    }

    private static WidgetFacts Read(IndexStore store, HakariSettings settings, DateTimeOffset now)
    {
        var converter = Flyout.FlyoutDataLoader.StoredConverter(store, settings);
        var query = new UsageQuery(store, PricingTable.LoadBundled(), converter);
        var cache = new LimitCache(store);
        var accounts = new AccountRepository(store).ListAccounts()
            .Select(account => cache.Load(account.AccountId) is { } snapshot
                ? new WidgetAccount(
                    account.AccountId,
                    account,
                    snapshot,
                    CostsOf(query, account.AccountId, now))
                : null)
            .OfType<WidgetAccount>();
        var arranged = AccountArrangement.Arrange(
            accounts,
            account => account.AccountId,
            account => LimitPriority.Rank(account.Snapshot),
            settings);

        var total = CostsOf(query, null, now);
        return new WidgetFacts(
            CostToday: total.Today,
            CostThisMonth: total.ThisMonth,
            CostLastHour: total.LastHour,
            Currency: query.Currency,
            Accounts: arranged,
            Nicknames: settings.AccountNicknames);
    }

    private static AccountCosts CostsOf(UsageQuery query, string? accountId, DateTimeOffset now)
    {
        decimal Since(DateTimeOffset from) =>
            query.Total(new UsageFilter(From: from, AccountId: accountId)).Cost;

        return new AccountCosts(
            Since(TimePeriods.StartOfToday(now)),
            Since(TimePeriods.StartOfMonth(now)),
            Since(now.AddHours(-1)));
    }
}
