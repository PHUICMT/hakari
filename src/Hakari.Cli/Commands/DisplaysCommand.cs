using Hakari.Cli.CommandLine;
using Hakari.Cli.Output;
using Hakari.Core.Displays;

namespace Hakari.Cli.Commands;

internal sealed class DisplaysCommand : ICliCommand
{
    private const string MainMarker = "yes";

    public string Name => "displays";

    public string Description => "List connected displays and their ids";

    public int Execute(CliArguments arguments)
    {
        var table = new TableWriter("Name", "Main", "Device", "Id");
        foreach (var display in DisplayCatalog.List())
        {
            table.AddRow(
                display.Name,
                display.IsPrimary ? MainMarker : string.Empty,
                display.DeviceName,
                display.Id);
        }

        table.WriteTo(Console.Out);
        return ExitCodes.Success;
    }
}
