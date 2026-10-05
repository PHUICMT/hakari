using System.Runtime.InteropServices;

namespace Hakari.Core.Displays.Interop;

/// <summary>DISPLAYCONFIG_SOURCE_DEVICE_NAME: the GDI name of a desktop source.</summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct SourceDeviceName
{
    private const int NameLength = 32;

    public DeviceInfoHeader Header;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = NameLength)]
    public string GdiDeviceName;
}
