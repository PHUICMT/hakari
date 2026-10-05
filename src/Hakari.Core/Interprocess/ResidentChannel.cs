namespace Hakari.Core.Interprocess;

/// <summary>Commands from the window process back to Hakari.exe.</summary>
public static class ResidentChannel
{
    private const string PipeNamePrefix = "Hakari.Resident.";

    public static string PipeName => PipeNamePrefix + Environment.UserName;

    public static bool TrySend(ResidentCommand command) =>
        PipeLines.TrySend(PipeName, command.ToString());

    public static Task ListenAsync(
        Action<ResidentCommand> onCommand,
        CancellationToken cancellationToken) =>
        PipeLines.ListenAsync(
            PipeName,
            line =>
            {
                if (Enum.TryParse<ResidentCommand>(line, ignoreCase: false, out var command))
                {
                    onCommand(command);
                }
            },
            cancellationToken);
}
