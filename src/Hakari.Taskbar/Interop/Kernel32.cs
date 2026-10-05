using System.Runtime.InteropServices;

namespace Hakari.Taskbar.Interop;

internal static class Kernel32
{
    private const string Library = "kernel32.dll";

    [DllImport(Library, CharSet = CharSet.Unicode)]
    public static extern IntPtr GetModuleHandle(string? moduleName);
}
