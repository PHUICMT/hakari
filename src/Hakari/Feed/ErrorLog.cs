using Hakari.Core.Configuration;

namespace Hakari.Feed;

/// <summary>What went wrong in the background, kept in the data folder for a bug report.</summary>
internal static class ErrorLog
{
    private const string FileName = "resident-errors.log";
    private const long MaximumBytes = 256 * 1024;

    public static void Write(Exception exception)
    {
        try
        {
            Directory.CreateDirectory(HakariPaths.DataDirectory);
            var path = Path.Combine(HakariPaths.DataDirectory, FileName);
            if (File.Exists(path) && new FileInfo(path).Length > MaximumBytes)
            {
                File.Delete(path);
            }

            File.AppendAllText(
                path,
                $"{DateTimeOffset.Now:O} {exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (Exception logFailure) when (logFailure is IOException
            or UnauthorizedAccessException)
        {
            // Nowhere to write; the widget still shows the error.
        }
    }
}