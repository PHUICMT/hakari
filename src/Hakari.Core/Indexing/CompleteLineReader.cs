using System.Buffers;

namespace Hakari.Core.Indexing;

public static class CompleteLineReader
{
    private const int InitialBufferSize = 2 * 1024 * 1024;
    private const byte LineFeed = (byte)'\n';

    /// <summary>
    /// Reads every newline-terminated line after <paramref name="startOffset"/> and returns the
    /// offset just past the last complete line, so a partially written line is retried later.
    /// </summary>
    public static long ReadFrom(string path, long startOffset, LineHandler onLine)
    {
        using var stream = OpenShared(path);
        stream.Seek(startOffset, SeekOrigin.Begin);

        var buffer = ArrayPool<byte>.Shared.Rent(InitialBufferSize);
        var consumedOffset = startOffset;
        var pendingLength = 0;

        try
        {
            while (true)
            {
                buffer = EnsureFreeSpace(buffer, pendingLength);
                var bytesRead = stream.Read(buffer, pendingLength, buffer.Length - pendingLength);
                if (bytesRead == 0)
                {
                    return consumedOffset;
                }

                var available = buffer.AsSpan(0, pendingLength + bytesRead);
                var completeLength = DispatchCompleteLines(available, onLine);
                consumedOffset += completeLength;

                pendingLength = available.Length - completeLength;
                available[completeLength..].CopyTo(buffer);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static int DispatchCompleteLines(ReadOnlySpan<byte> available, LineHandler onLine)
    {
        var lineStart = 0;
        int lineLength;
        while ((lineLength = available[lineStart..].IndexOf(LineFeed)) >= 0)
        {
            onLine(available.Slice(lineStart, lineLength));
            lineStart += lineLength + 1;
        }

        return lineStart;
    }

    private static byte[] EnsureFreeSpace(byte[] buffer, int pendingLength)
    {
        if (pendingLength < buffer.Length)
        {
            return buffer;
        }

        var largerBuffer = ArrayPool<byte>.Shared.Rent(buffer.Length * 2);
        buffer.AsSpan(0, pendingLength).CopyTo(largerBuffer);
        ArrayPool<byte>.Shared.Return(buffer);
        return largerBuffer;
    }

    private static FileStream OpenShared(string path) => new(
        path,
        FileMode.Open,
        FileAccess.Read,
        FileShare.ReadWrite | FileShare.Delete,
        bufferSize: 1,
        FileOptions.SequentialScan);
}
