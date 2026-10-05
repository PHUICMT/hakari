using System.Runtime.InteropServices;

namespace Hakari.Core.Displays.Interop;

/// <summary>DISPLAYCONFIG_TARGET_DEVICE_NAME: the monitor's own name and path.</summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct TargetDeviceName
{
    private const int FriendlyNameLength = 64;
    private const int DevicePathLength = 128;

    public DeviceInfoHeader Header;
    public uint Flags;
    public uint OutputTechnology;
    public ushort ManufactureId;
    public ushort ProductCodeId;
    public uint ConnectorInstance;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = FriendlyNameLength)]
    public string FriendlyName;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = DevicePathLength)]
    public string DevicePath;
}
