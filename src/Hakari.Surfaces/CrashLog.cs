using System.Globalization;
using Hakari.Core.Configuration;

namespace Hakari.Surfaces;

/// <summary>
/// Notes why the window process stopped, in the data folder, so a crash can be explained.
/// Only the error itself is written: never usage, settings or anything from the logs.
/// </summary>
internal static class CrashLog
{
    private const string FileName = "surfaces-errors.log";
    private const long MaximumBytes = 256 * 1024;

    public static void Write(Exception? exception, string message)
    {
        try
        {
            Directory.CreateDirectory(HakariPaths.DataDirectory);
            var path = Path.Combine(HakariPaths.DataDirectory, FileName);
            if (File.Exists(path) && new FileInfo(path).Length > MaximumBytes)
            {
                File.Delete(path);
            }

            var stamp = DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture);
            File.AppendAllText(path, $"{stamp} {message}{Environment.NewLine}{exception}"
                + Environment.NewLine + Environment.NewLine);
        }
        catch (Exception logging) when (logging is IOException or UnauthorizedAccessException)
        {
        }
    }
}
