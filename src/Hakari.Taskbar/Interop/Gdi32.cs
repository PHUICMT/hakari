using System.Runtime.InteropServices;

namespace Hakari.Taskbar.Interop;

internal static class Gdi32
{
    private const string Library = "gdi32.dll";

    [DllImport(Library)]
    public static extern IntPtr CreateCompatibleDC(IntPtr deviceContext);

    [DllImport(Library)]
    public static extern IntPtr SelectObject(IntPtr deviceContext, IntPtr graphicsObject);

    [DllImport(Library)]
    public static extern bool DeleteObject(IntPtr graphicsObject);

    [DllImport(Library)]
    public static extern bool DeleteDC(IntPtr deviceContext);
}
