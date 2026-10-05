using System.Runtime.InteropServices;

namespace Hakari.Taskbar.Interop;

[StructLayout(LayoutKind.Sequential)]
internal struct NativeSize
{
    public int Width;
    public int Height;
}
