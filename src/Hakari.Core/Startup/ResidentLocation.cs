using Hakari.Core.Configuration;

namespace Hakari.Core.Startup;

/// <summary>
/// Where Hakari.exe runs from, written by Hakari.exe at start, so the settings window (a
/// different executable) can register the right program to start with Windows.
/// </summary>
public static class ResidentLocation
{
    private const string FileName = "resident.path";

    private static string FilePath => Path.Combine(HakariPaths.DataDirectory, FileName);

    public static void Record(string executablePath)
    {
        Directory.CreateDirectory(HakariPaths.DataDirectory);
        File.WriteAllText(FilePath, executablePath);
    }

    public static string? Read()
    {
        try
        {
            var path = File.ReadAllText(FilePath).Trim();
            return File.Exists(path) ? path : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
