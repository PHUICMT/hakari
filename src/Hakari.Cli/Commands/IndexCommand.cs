using Hakari.Cli.CommandLine;
using Hakari.Cli.Output;

namespace Hakari.Cli.Commands;

internal sealed class IndexCommand : ICliCommand
{
    private const string Separator = " · ";

    public string Name => "index";

    public string Description => "Index new log lines from every source (incremental)";

    public int Execute(CliArguments arguments)
    {
        using var store = CommandContext.OpenStore(arguments);
        var sources = CommandContext.DiscoverSources(arguments);
        var rebuild = arguments.HasFlag(OptionNames.Rebuild);
        var statistics = CommandContext.RunIndexer(store, sources, rebuild);

        string[] parts =
        [
            $"{sources.Count} sources",
            $"{DisplayFormat.Number(statistics.FilesScanned)} files",
            $"{DisplayFormat.Number(statistics.FilesChanged)} changed",
            $"{DisplayFormat.Bytes(statistics.BytesRead)} read",
            $"{DisplayFormat.Number(statistics.RecordsChanged)} records changed",
            $"{statistics.Elapsed.TotalSeconds:N1}s",
        ];

        Console.WriteLine(string.Join(Separator, parts));
        return ExitCodes.Success;
    }
}
