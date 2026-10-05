using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;

namespace Hakari.Surfaces.Flyout;

internal static partial class NativeDpi
{
    private const int EffectiveDpi = 0;
    private const uint DefaultDpi = 96;
    private const uint NearestMonitor = 2;

    /// <summary>The effective DPI of the monitor holding the display area.</summary>
    public static uint ForDisplay(DisplayArea display)
    {
        var center = new NativePoint
        {
            X = display.OuterBounds.X + display.OuterBounds.Width / 2,
            Y = display.OuterBounds.Y + display.OuterBounds.Height / 2,
        };
        var monitor = MonitorFromPoint(center, NearestMonitor);
        var succeeded = GetDpiForMonitor(monitor, EffectiveDpi, out var dpiX, out _) == 0;
        return succeeded ? dpiX : DefaultDpi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [LibraryImport("user32.dll")]
    private static partial IntPtr MonitorFromPoint(NativePoint point, uint flags);

    [LibraryImport("shcore.dll")]
    private static partial int GetDpiForMonitor(
        IntPtr monitor,
        int type,
        out uint dpiX,
        out uint dpiY);
}
