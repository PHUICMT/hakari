using Hakari.Cli.CommandLine;

namespace Hakari.Cli.Commands;

public interface ICliCommand
{
    string Name { get; }

    string Description { get; }

    int Execute(CliArguments arguments);
}
