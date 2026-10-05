using System.IO.Pipes;

namespace Hakari.Core.Interprocess;

/// <summary>
/// A named pipe, private to the signed-in user, carrying one command per connection from
/// Hakari.exe to the window process.
/// </summary>
public static class SurfaceChannel
{
    private const string PipeNamePrefix = "Hakari.Surfaces.";
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromMilliseconds(150);

    public static string PipeName => PipeNamePrefix + Environment.UserName;

    /// <summary>Returns false when no window process is listening.</summary>
    public static bool TrySend(SurfaceCommand command)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            pipe.Connect(ConnectTimeout);
            using var writer = new StreamWriter(pipe) { AutoFlush = true };
            writer.WriteLine(command.ToLine());
            return true;
        }
        catch (Exception exception) when (exception is TimeoutException or IOException)
        {
            return false;
        }
    }

    /// <summary>Serves commands until cancelled; each arrives on a thread-pool thread.</summary>
    public static async Task ListenAsync(
        Action<SurfaceCommand> onCommand,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await using var pipe = new NamedPipeServerStream(
                PipeName,
                PipeDirection.In,
                maxNumberOfServerInstances: 1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            try
            {
                await pipe.WaitForConnectionAsync(cancellationToken);
                using var reader = new StreamReader(pipe);
                var line = await reader.ReadLineAsync(cancellationToken);
                if (SurfaceCommand.ParseLine(line) is { } command)
                {
                    onCommand(command);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (IOException)
            {
            }
        }
    }
}
