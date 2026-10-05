using Hakari.Cli.CommandLine;

namespace Hakari.Cli.Commands;

public static class CommandRunner
{
    private const string ExecutableName = "hakari";

    private static readonly ICliCommand[] Commands =
    [
        new SourcesCommand(),
        new IndexCommand(),
        new ReportCommand(),
        new LimitsCommand(),
        new DisplaysCommand(),
    ];

    public static int Run(CliArguments arguments)
    {
        var command = Commands.FirstOrDefault(candidate => string.Equals(
            candidate.Name,
            arguments.CommandName,
            StringComparison.OrdinalIgnoreCase));

        if (command is null)
        {
            WriteHelp();
            return arguments.CommandName is null ? ExitCodes.Success : ExitCodes.InvalidArguments;
        }

        try
        {
            return command.Execute(arguments);
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return ExitCodes.InvalidArguments;
        }
    }

    private static void WriteHelp()
    {
        Console.WriteLine($"Usage: {ExecutableName} <command> [options]");
        Console.WriteLine();

        foreach (var command in Commands)
        {
            Console.WriteLine($"  {command.Name,-10} {command.Description}");
        }

        Console.WriteLine();
        Console.WriteLine("Common options:");
        Console.WriteLine($"  {OptionNames.Database} <path>     index database path");
        Console.WriteLine($"  {OptionNames.Wsl} off|runningonly|all");
        Console.WriteLine($"  {OptionNames.ConfigDirectory} <path>   extra config directory");
        Console.WriteLine($"  {OptionNames.Rebuild}               re-index from scratch");
        Console.WriteLine($"  {OptionNames.Currency} <code>     convert costs, e.g. THB");
        Console.WriteLine($"  {OptionNames.RateMode} day|latest       rate per usage day");
    }
}
