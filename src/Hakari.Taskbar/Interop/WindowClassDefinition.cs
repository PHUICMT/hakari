using System.Runtime.InteropServices;

namespace Hakari.Taskbar.Interop;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct WindowClassDefinition
{
    public uint Size;
    public uint Style;
    public IntPtr WindowProcedure;
    public int ClassExtraBytes;
    public int WindowExtraBytes;
    public IntPtr Instance;
    public IntPtr Icon;
    public IntPtr Cursor;
    public IntPtr BackgroundBrush;
    public string? MenuName;
    public string ClassName;
    public IntPtr SmallIcon;
}
