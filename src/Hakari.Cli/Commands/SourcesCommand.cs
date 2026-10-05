using Hakari.Cli.CommandLine;
using Hakari.Cli.Output;
using Hakari.Core.Accounts;

namespace Hakari.Cli.Commands;

internal sealed class SourcesCommand : ICliCommand
{
    private const string NotAvailable = "-";

    public string Name => "sources";

    public string Description => "List detected usage sources and their accounts";

    public int Execute(CliArguments arguments)
    {
        var table = new TableWriter("Id", "Kind", "Account", "Plan", "Config directory");

        foreach (var source in CommandContext.DiscoverSources(arguments))
        {
            var account = AccountFileReader.Read(source);
            table.AddRow(
                source.Id,
                source.Kind.ToString(),
                account?.Email ?? NotAvailable,
                account?.Plan.ToString() ?? NotAvailable,
                source.ConfigDirectory);
        }

        table.WriteTo(Console.Out);
        return ExitCodes.Success;
    }
}
