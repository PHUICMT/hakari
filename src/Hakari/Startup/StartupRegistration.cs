using Microsoft.Win32;

namespace Hakari.Startup;

/// <summary>
/// Start with Windows for the unpackaged build, through the per-user Run key. Changed only
/// when the user picks it in the menu, so running a development build never registers it.
/// The Store build will use an MSIX StartupTask instead.
/// </summary>
internal static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Hakari";

    public static bool IsRegistered()
    {
        using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        var executable = Environment.ProcessPath ?? string.Empty;
        return runKey?.GetValue(ValueName) is string command
            && command.Contains(executable, StringComparison.OrdinalIgnoreCase);
    }

    public static void Toggle()
    {
        using var runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (IsRegistered())
        {
            runKey.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        else
        {
            runKey.SetValue(ValueName, $"\"{Environment.ProcessPath}\"");
        }
    }
}
