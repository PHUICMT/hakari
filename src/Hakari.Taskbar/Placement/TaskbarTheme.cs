using Microsoft.Win32;

namespace Hakari.Taskbar.Placement;

public static class TaskbarTheme
{
    private const string PersonalizeKey =
        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    /// <summary>The taskbar follows the Windows mode, not the app mode.</summary>
    private const string SystemUsesLightThemeValue = "SystemUsesLightTheme";

    public static bool IsLight()
    {
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
        return key?.GetValue(SystemUsesLightThemeValue) is int value && value != 0;
    }
}
