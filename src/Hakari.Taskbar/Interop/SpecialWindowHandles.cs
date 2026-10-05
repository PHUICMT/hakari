namespace Hakari.Taskbar.Interop;

internal static class SpecialWindowHandles
{
    public static readonly IntPtr Top = IntPtr.Zero;
    public static readonly IntPtr TopMost = new(-1);
}
