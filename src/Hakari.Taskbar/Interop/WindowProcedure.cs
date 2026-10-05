namespace Hakari.Taskbar.Interop;

internal delegate IntPtr WindowProcedure(
    IntPtr windowHandle,
    uint message,
    IntPtr wordParameter,
    IntPtr longParameter);
