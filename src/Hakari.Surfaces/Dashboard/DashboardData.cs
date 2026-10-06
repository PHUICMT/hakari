using Hakari.Core.Accounts;
using Hakari.Core.Configuration;
using Hakari.Core.Indexing;
using Hakari.Core.Pricing;
using Hakari.Core.Querying;
using Hakari.Core.Settings;
using Hakari.Surfaces.Flyout;
using Microsoft.Data.Sqlite;

namespace Hakari.Surfaces.Dashboard;

/// <summary>
/// Opens the index read-only for one read and closes it again, so the dashboard never holds
/// the database while Hakari.exe writes to it.
/// </summary>
internal static class DashboardData
{
    public static T Read<T>(Func<UsageQuery, IndexStore, T> read, T fallback)
    {
        if (!File.Exists(HakariPaths.DefaultIndexPath))
        {
            return fallback;
        }

        try
        {
            using var store = IndexStore.OpenReadOnly(HakariPaths.DefaultIndexPath);
            var settings = SettingsStore.Default.Load();
            var query = new UsageQuery(
                store,
                PricingTable.LoadBundled(),
                FlyoutDataLoader.StoredConverter(store, settings));
            return read(query, store);
        }
        catch (Exception exception) when (exception is SqliteException or IOException)
        {
            return fallback;
        }
    }

    public static IReadOnlyList<AccountInfo> Accounts() =>
        Read((_, store) => new AccountRepository(store).ListAccounts(), []);

    public static bool HasUsageBeforeAccounts() => Read(
        (query, _) => query.Total(new UsageFilter(AccountId: string.Empty)).Messages > 0,
        false);

    public static IReadOnlyList<string> SourceIds() =>
        Read(
            (_, store) => SourceActivity.Load(store).Select(source => source.SourceId).ToList(),
            []);
}
