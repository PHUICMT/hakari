using System.Runtime.InteropServices;

namespace Hakari.Core.Displays.Interop;

/// <summary>MONITORINFOEXW.</summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct MonitorInformation
{
    public const uint PrimaryFlag = 1;
    private const int DeviceNameLength = 32;

    public int Size;
    public int MonitorLeft;
    public int MonitorTop;
    public int MonitorRight;
    public int MonitorBottom;
    public int WorkLeft;
    public int WorkTop;
    public int WorkRight;
    public int WorkBottom;
    public uint Flags;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = DeviceNameLength)]
    public string DeviceName;
}
