using Microsoft.Win32;

namespace Hakari.Core.Startup;

/// <summary>
/// Start with Windows for the unpackaged build, through the per-user Run key. Changed only
/// when the user asks, so running a development build never registers it. The Store build
/// will use an MSIX StartupTask instead.
/// </summary>
public static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Hakari";

    public static bool IsRegistered(string executablePath)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return runKey?.GetValue(ValueName) is string command
            && command.Contains(executablePath, StringComparison.OrdinalIgnoreCase);
    }

    public static void Set(string executablePath, bool startWithWindows)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (startWithWindows)
        {
            runKey.SetValue(ValueName, $"\"{executablePath}\"");
        }
        else
        {
            runKey.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
