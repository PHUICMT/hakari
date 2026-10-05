using Hakari.Cli.CommandLine;
using Hakari.Cli.Output;
using Hakari.Core.Limits;

namespace Hakari.Cli.Commands;

internal sealed class LimitsCommand : ICliCommand
{
    private const string LiveLabel = "live";
    private const string LastKnownLabel = "last known";
    private const string NoResetLabel = "-";

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

    public string Name => "limits";

    public string Description =>
        $"Show usage limits per account ({OptionNames.RefreshSignIn} renews expired sign-ins)";

    public int Execute(CliArguments arguments)
    {
        using var store = CommandContext.OpenStore(arguments);
        using var httpClient = new HttpClient { Timeout = RequestTimeout };
        var sources = CommandContext.DiscoverSources(arguments);
        var tracker = CommandContext.CreateAccountTracker(store);
        tracker.Observe(sources);

        var service = new LimitService(
            new UsageLimitClient(httpClient, TimeProvider.System),
            new LimitCache(store),
            new ClaudeCodeActivity(),
            new LimitServiceOptions(arguments.HasFlag(OptionNames.RefreshSignIn)),
            TimeProvider.System);

        foreach (var (accountId, accountSources) in tracker.SourcesByCurrentAccount(sources))
        {
            var result = service
                .GetForAccountAsync(accountId, accountSources, CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            var sourceList = string.Join(", ", accountSources.Select(source => source.Id));
            Console.WriteLine($"{CommandContext.AccountLabel(store, accountId)} · {sourceList}");
            WriteResult(result);
        }

        return ExitCodes.Success;
    }

    private static void WriteResult(LimitResult result)
    {
        if (result.Snapshot is not { } snapshot)
        {
            Console.WriteLine($"    no limits known ({result.Failure})");
            return;
        }

        var freshness = snapshot.Freshness == LimitFreshness.Live ? LiveLabel : LastKnownLabel;
        Console.WriteLine($"    {freshness}, fetched {snapshot.FetchedAt.ToLocalTime():HH:mm:ss}");
        WriteLimits(snapshot);
        WriteExtraUsage(snapshot.ExtraUsage);
    }

    private static void WriteLimits(LimitSnapshot snapshot)
    {
        var table = new TableWriter("    Limit", "Used", "Severity", "Resets");
        foreach (var limit in snapshot.Limits)
        {
            var name = limit.ScopeName is null ? limit.Kind : $"{limit.Kind} ({limit.ScopeName})";
            var resets = limit.ResetsAt?.ToLocalTime().ToString("ddd HH:mm") ?? NoResetLabel;
            table.AddRow($"    {name}", $"{limit.Percent}%", limit.Severity, resets);
        }

        table.WriteTo(Console.Out);
    }

    private static void WriteExtraUsage(ExtraUsage? extraUsage)
    {
        if (extraUsage is not { IsEnabled: true } extra)
        {
            return;
        }

        Console.WriteLine(
            $"    extra usage {extra.Used:N2} of {extra.MonthlyLimit:N2} {extra.Currency} "
            + $"({DisplayFormat.Percent(extra.Utilization)})");
    }
}
