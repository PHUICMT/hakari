using System.ComponentModel;
using System.Diagnostics;
using Hakari.Core.Interprocess;

namespace Hakari.Surfaces;

/// <summary>Opens a window in the window process, starting the process if needed.</summary>
internal static class SurfacesLauncher
{
    private const string ExecutableName = "Hakari.Surfaces.exe";

    /// <summary>Next to Hakari.exe when installed; the sibling project in development.</summary>
    private static readonly string[] RelativeCandidates =
    [
        ExecutableName,
        Path.Combine(
            "..", "..", "..", "..", "Hakari.Surfaces", "bin", "x64", "Release",
            "net9.0-windows10.0.19041.0", "win-x64", ExecutableName),
        Path.Combine(
            "..", "..", "..", "..", "Hakari.Surfaces", "bin", "x64", "Debug",
            "net9.0-windows10.0.19041.0", "win-x64", ExecutableName),
    ];

    public static void Show(SurfaceCommand command)
    {
        if (SurfaceChannel.TrySend(command))
        {
            return;
        }

        if (FindExecutable() is not { } executable)
        {
            return;
        }

        try
        {
            var startInfo = new ProcessStartInfo(executable) { UseShellExecute = false };
            foreach (var argument in command.ToArguments())
            {
                startInfo.ArgumentList.Add(argument);
            }

            Process.Start(startInfo)?.Dispose();
        }
        catch (Win32Exception)
        {
        }
    }

    private static string? FindExecutable() =>
        RelativeCandidates
            .Select(candidate => Path.Combine(AppContext.BaseDirectory, candidate))
            .Select(Path.GetFullPath)
            .FirstOrDefault(File.Exists);
}
