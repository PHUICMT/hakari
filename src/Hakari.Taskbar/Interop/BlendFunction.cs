using System.Runtime.InteropServices;

namespace Hakari.Taskbar.Interop;

[StructLayout(LayoutKind.Sequential)]
internal struct BlendFunction
{
    public byte BlendOperation;
    public byte BlendFlags;
    public byte SourceConstantAlpha;
    public byte AlphaFormat;
}
