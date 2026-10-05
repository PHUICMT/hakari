namespace Hakari.Core.Interprocess;

/// <summary>Commands from Hakari.exe to the window process: open this, at that point.</summary>
public static class SurfaceChannel
{
    private const string PipeNamePrefix = "Hakari.Surfaces.";

    public static string PipeName => PipeNamePrefix + Environment.UserName;

    /// <summary>Returns false when no window process is listening.</summary>
    public static bool TrySend(SurfaceCommand command) =>
        PipeLines.TrySend(PipeName, command.ToLine());

    /// <summary>Serves commands until cancelled; each arrives on a thread-pool thread.</summary>
    public static Task ListenAsync(
        Action<SurfaceCommand> onCommand,
        CancellationToken cancellationToken) =>
        PipeLines.ListenAsync(
            PipeName,
            line =>
            {
                if (SurfaceCommand.ParseLine(line) is { } command)
                {
                    onCommand(command);
                }
            },
            cancellationToken);
}
