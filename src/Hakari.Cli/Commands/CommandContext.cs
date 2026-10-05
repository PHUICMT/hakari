using Hakari.Cli.CommandLine;
using Hakari.Core.Accounts;
using Hakari.Core.Configuration;
using Hakari.Core.Indexing;
using Hakari.Core.Performance;
using Hakari.Core.Sources;

namespace Hakari.Cli.Commands;

internal static class CommandContext
{
    public static IndexStore OpenStore(CliArguments arguments) =>
        new(arguments.GetValue(OptionNames.Database) ?? HakariPaths.DefaultIndexPath);

    public static IReadOnlyList<UsageSource> DiscoverSources(CliArguments arguments)
    {
        var options = new SourceDiscoveryOptions(
            WslMode: ParseWslMode(arguments.GetValue(OptionNames.Wsl)),
            ExtraConfigDirectories: arguments.GetValues(OptionNames.ConfigDirectory));

        return SourceDiscovery.Discover(options);
    }

    public static IndexStatistics RunIndexer(
        IndexStore store,
        IReadOnlyList<UsageSource> sources,
        bool rebuild)
    {
        if (rebuild)
        {
            store.DeleteAll();
        }

        using var backgroundMode = BackgroundThreadMode.Enter();
        CreateAccountTracker(store).Observe(sources);
        return new Indexer(store).Index(sources);
    }

    public static AccountTracker CreateAccountTracker(IndexStore store) =>
        new(new AccountRepository(store), TimeProvider.System);

    /// <summary>Email if known, else the account id, for display.</summary>
    public static string AccountLabel(IndexStore store, string accountId)
    {
        var account = new AccountRepository(store).ListAccounts()
            .FirstOrDefault(candidate => candidate.AccountId == accountId);
        var label = account?.Email ?? accountId;
        return account is null ? label : $"{label} · {account.Plan}";
    }

    private static WslScanMode ParseWslMode(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return SourceDiscoveryOptions.Default.WslMode;
        }

        if (Enum.TryParse<WslScanMode>(value, ignoreCase: true, out var mode))
        {
            return mode;
        }

        var validModes = string.Join(", ", Enum.GetNames<WslScanMode>());
        throw new ArgumentException($"Unknown WSL mode '{value}'. Use one of: {validModes}.");
    }
}
