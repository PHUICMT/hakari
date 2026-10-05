using Hakari.Core.Accounts;
using Hakari.Core.Configuration;
using Hakari.Core.Indexing;
using Microsoft.Data.Sqlite;

namespace Hakari.Surfaces.Settings;

internal static class SettingsDataLoader
{
    /// <summary>Empty before Hakari.exe has built its index for the first time.</summary>
    public static IReadOnlyList<AccountInfo> LoadAccounts()
    {
        if (!File.Exists(HakariPaths.DefaultIndexPath))
        {
            return [];
        }

        try
        {
            using var store = IndexStore.OpenReadOnly(HakariPaths.DefaultIndexPath);
            return new AccountRepository(store).ListAccounts();
        }
        catch (Exception exception) when (exception is SqliteException or IOException)
        {
            return [];
        }
    }
}
