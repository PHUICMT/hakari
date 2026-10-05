using System.Runtime.InteropServices;

namespace Hakari.Core.Displays.Interop;

[StructLayout(LayoutKind.Sequential)]
internal struct Luid
{
    public uint LowPart;
    public int HighPart;
}
