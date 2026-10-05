using System.Runtime.InteropServices;

namespace Hakari.Taskbar.Interop;

[StructLayout(LayoutKind.Sequential)]
internal struct NativeMessage
{
    public IntPtr WindowHandle;
    public uint Message;
    public IntPtr WordParameter;
    public IntPtr LongParameter;
    public uint Time;
    public NativePoint Point;
}
