using System.Runtime.InteropServices;

namespace Hakari.Taskbar.Interop;

internal static class User32
{
    private const string Library = "user32.dll";

    [DllImport(Library, CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern ushort RegisterClassEx(ref WindowClassDefinition definition);

    [DllImport(Library, CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr CreateWindowEx(
        long extendedStyle,
        string className,
        string windowName,
        long style,
        int x,
        int y,
        int width,
        int height,
        IntPtr parent,
        IntPtr menu,
        IntPtr instance,
        IntPtr parameter);

    [DllImport(Library, CharSet = CharSet.Unicode)]
    public static extern IntPtr DefWindowProc(
        IntPtr windowHandle,
        uint message,
        IntPtr wordParameter,
        IntPtr longParameter);

    [DllImport(Library, CharSet = CharSet.Unicode)]
    public static extern int GetMessage(
        out NativeMessage message,
        IntPtr windowHandle,
        uint filterMinimum,
        uint filterMaximum);

    [DllImport(Library)]
    public static extern bool TranslateMessage(ref NativeMessage message);

    [DllImport(Library, CharSet = CharSet.Unicode)]
    public static extern IntPtr DispatchMessage(ref NativeMessage message);

    [DllImport(Library, CharSet = CharSet.Unicode)]
    public static extern bool PostMessage(
        IntPtr windowHandle,
        uint message,
        IntPtr wordParameter,
        IntPtr longParameter);

    [DllImport(Library)]
    public static extern void PostQuitMessage(int exitCode);

    [DllImport(Library)]
    public static extern bool DestroyWindow(IntPtr windowHandle);

    [DllImport(Library, SetLastError = true)]
    public static extern IntPtr SetParent(IntPtr child, IntPtr newParent);

    [DllImport(Library, EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    public static extern IntPtr SetWindowLongPointer(IntPtr windowHandle, int index, IntPtr value);

    [DllImport(Library, EntryPoint = "GetWindowLongPtrW")]
    public static extern IntPtr GetWindowLongPointer(IntPtr windowHandle, int index);

    [DllImport(Library, SetLastError = true)]
    public static extern bool SetWindowPos(
        IntPtr windowHandle,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [DllImport(Library)]
    public static extern bool GetWindowRect(IntPtr windowHandle, out NativeRectangle rectangle);

    [DllImport(Library, CharSet = CharSet.Unicode)]
    public static extern IntPtr FindWindow(string? className, string? windowName);

    [DllImport(Library, CharSet = CharSet.Unicode)]
    public static extern IntPtr FindWindowEx(
        IntPtr parent,
        IntPtr childAfter,
        string? className,
        string? windowName);

    [DllImport(Library, CharSet = CharSet.Unicode)]
    public static extern uint RegisterWindowMessage(string name);

    [DllImport(Library, SetLastError = true)]
    public static extern bool UpdateLayeredWindow(
        IntPtr windowHandle,
        IntPtr destinationContext,
        IntPtr destinationPoint,
        ref NativeSize size,
        IntPtr sourceContext,
        ref NativePoint sourcePoint,
        uint colorKey,
        ref BlendFunction blend,
        uint flags);

    [DllImport(Library)]
    public static extern IntPtr GetDC(IntPtr windowHandle);

    [DllImport(Library)]
    public static extern int ReleaseDC(IntPtr windowHandle, IntPtr deviceContext);

    [DllImport(Library)]
    public static extern IntPtr SetTimer(
        IntPtr windowHandle,
        IntPtr timerId,
        uint intervalMilliseconds,
        IntPtr callback);

    [DllImport(Library)]
    public static extern bool KillTimer(IntPtr windowHandle, IntPtr timerId);

    [DllImport(Library)]
    public static extern bool ShowWindow(IntPtr windowHandle, int command);

    [DllImport(Library)]
    public static extern bool IsWindow(IntPtr windowHandle);

    [DllImport(Library)]
    public static extern bool IsWindowVisible(IntPtr windowHandle);

    [DllImport(Library)]
    public static extern uint GetDpiForWindow(IntPtr windowHandle);

    [DllImport(Library)]
    public static extern bool SetProcessDpiAwarenessContext(IntPtr context);

    [DllImport(Library)]
    public static extern bool TrackMouseEvent(ref TrackMouseEventOptions options);

    [DllImport(Library)]
    public static extern IntPtr GetWindow(IntPtr windowHandle, uint relationship);

    [DllImport(Library, SetLastError = true)]
    public static extern bool SystemParametersInfo(
        uint action,
        uint parameter,
        ref bool value,
        uint flags);
}
