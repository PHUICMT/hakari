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
                ? new WidgetAccount(account.AccountId, account, snapshot)
                : null)
            .OfType<WidgetAccount>()
            .OrderByDescending(account => LimitPriority.Rank(account.Snapshot))
            .ToList();

        return new WidgetFacts(
            CostToday: query.Total(new UsageFilter(From: TimePeriods.StartOfToday(now))).Cost,
            CostThisMonth: query.Total(new UsageFilter(From: TimePeriods.StartOfMonth(now))).Cost,
            CostLastHour: query.Total(new UsageFilter(From: now.AddHours(-1))).Cost,
            Currency: query.Currency,
            Accounts: accounts);
    }
}
