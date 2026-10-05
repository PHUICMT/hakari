using Hakari.Cli.CommandLine;
using Hakari.Cli.Output;
using Hakari.Core.Limits;
using Hakari.Core.Limits.Credentials;

namespace Hakari.Cli.Commands;

internal sealed class LimitsCommand : ICliCommand
{
    private const string LiveLabel = "live";
    private const string LastKnownLabel = "last known";
    private const string NoResetLabel = "-";

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

    public string Name => "limits";

    public string Description =>
        $"Show usage limits per source ({OptionNames.RefreshSignIn} renews expired sign-ins)";

    public int Execute(CliArguments arguments)
    {
        using var store = CommandContext.OpenStore(arguments);
        using var httpClient = new HttpClient { Timeout = RequestTimeout };
        var options = new LimitServiceOptions(arguments.HasFlag(OptionNames.RefreshSignIn));
        var service = new LimitService(
            new UsageLimitClient(httpClient, TimeProvider.System),
            new LimitCache(store),
            new ClaudeCodeActivity(),
            options,
            TimeProvider.System);

        foreach (var source in CommandContext.DiscoverSources(arguments))
        {
            var credentials = CredentialsFile.For(source).Read()?.Credentials;
            var result = service.GetAsync(source, CancellationToken.None).GetAwaiter().GetResult();
            WriteSource(source.Id, credentials, result);
        }

        return ExitCodes.Success;
    }

    private static void WriteSource(
        string sourceId,
        OAuthCredentials? credentials,
        LimitResult result)
    {
        var tier = credentials?.RateLimitTier ?? "unknown plan";
        var signIn = credentials is null
            ? "no sign-in"
            : $"sign-in valid until {credentials.ExpiresAt.ToLocalTime():yyyy-MM-dd HH:mm}";
        Console.WriteLine($"{sourceId} · {tier} · {signIn}");

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
