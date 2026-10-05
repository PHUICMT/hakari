using Microsoft.UI.Windowing;

namespace Hakari.Surfaces;

/// <summary>The Hakari mark on the title bar, taskbar button and Alt+Tab.</summary>
internal static class WindowIcon
{
    private const string FileName = "Hakari.ico";

    public static void ApplyTo(AppWindow window)
    {
        var path = Path.Combine(AppContext.BaseDirectory, FileName);
        if (File.Exists(path))
        {
            window.SetIcon(path);
        }
    }
}
