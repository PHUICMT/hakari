using System.IO.Pipes;

namespace Hakari.Core.Interprocess;

/// <summary>
/// One line per connection over a named pipe that only the signed-in user can open. Used in
/// both directions between Hakari.exe and the window process.
/// </summary>
internal static class PipeLines
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromMilliseconds(150);

    /// <summary>Returns false when nobody is listening.</summary>
    public static bool TrySend(string pipeName, string line)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.Out);
            pipe.Connect(ConnectTimeout);
            using var writer = new StreamWriter(pipe) { AutoFlush = true };
            writer.WriteLine(line);
            return true;
        }
        catch (Exception exception) when (exception is TimeoutException or IOException)
        {
            return false;
        }
    }

    /// <summary>Serves lines until cancelled; each arrives on a thread-pool thread.</summary>
    public static async Task ListenAsync(
        string pipeName,
        Action<string> onLine,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await using var pipe = new NamedPipeServerStream(
                pipeName,
                PipeDirection.In,
                maxNumberOfServerInstances: 1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            try
            {
                await pipe.WaitForConnectionAsync(cancellationToken);
                using var reader = new StreamReader(pipe);
                if (await reader.ReadLineAsync(cancellationToken) is { } line)
                {
                    onLine(line);
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
