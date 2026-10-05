using System.Runtime.InteropServices;

namespace Hakari.Taskbar.Interop;

[StructLayout(LayoutKind.Sequential)]
internal struct NativePoint
{
    public int X;
    public int Y;
}
