using System.Runtime.InteropServices;

namespace Hakari.Core.Displays.Interop;

[StructLayout(LayoutKind.Sequential)]
internal struct DeviceInfoHeader
{
    public const uint SourceNameType = 1;
    public const uint TargetNameType = 2;

    public uint Type;
    public uint Size;
    public Luid AdapterId;
    public uint Id;
}
