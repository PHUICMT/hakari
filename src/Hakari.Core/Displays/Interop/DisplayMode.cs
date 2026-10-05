using System.Runtime.InteropServices;

namespace Hakari.Core.Displays.Interop;

/// <summary>DISPLAYCONFIG_MODE_INFO. Only its size matters; the mode is unused.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct DisplayMode
{
    private const int ModeUnionBytes = 48;

    public uint InfoType;
    public uint Id;
    public Luid AdapterId;
    public fixed byte Mode[ModeUnionBytes];
}
