using System.Runtime.InteropServices;

namespace Hakari.Taskbar.Interop;

[StructLayout(LayoutKind.Sequential)]
internal struct TrackMouseEventOptions
{
    public uint Size;
    public uint Flags;
    public IntPtr WindowHandle;
    public uint HoverTime;
}
