using System.Runtime.InteropServices;

namespace Hakari.Taskbar.Interop;

/// <summary>MONITORINFOEXW; only the device name is read.</summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct MonitorInformation
{
    private const int DeviceNameLength = 32;

    public int Size;
    public NativeRectangle Monitor;
    public NativeRectangle WorkArea;
    public uint Flags;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = DeviceNameLength)]
    public string DeviceName;
}
