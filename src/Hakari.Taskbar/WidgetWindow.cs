using System.Drawing;
using System.Runtime.InteropServices;
using Hakari.Taskbar.Interop;
using Hakari.Taskbar.Placement;

namespace Hakari.Taskbar;

/// <summary>
/// One widget on one taskbar. It only shows a bitmap and reports the mouse; the host decides
/// what to draw and where.
/// </summary>
internal sealed class WidgetWindow : IDisposable
{
    private const string ClassName = "HakariTaskbarWidget";
    private static readonly Dictionary<IntPtr, WidgetWindow> WindowsByHandle = [];

    private readonly AttachMode mode;
    private Rectangle lastPlacement;

    private WidgetWindow(TaskbarTarget target, IntPtr handle, IntPtr parent, AttachMode mode)
    {
        Target = target;
        Handle = handle;
        ParentHandle = parent;
        this.mode = mode;
    }

    public event EventHandler? Clicked;

    public event EventHandler? RightClicked;

    public event EventHandler? HoverChanged;

    public TaskbarTarget Target { get; }

    public IntPtr Handle { get; }

    public IntPtr ParentHandle { get; }

    public bool IsHovered { get; private set; }

    public Size RenderedSize { get; private set; }

    /// <summary>Zero until the first render, so a new widget always gets drawn.</summary>
    public uint RenderedDpi { get; private set; }

    /// <summary>A child window dies with Explorer; the host then creates a new one.</summary>
    public bool IsAlive => User32.IsWindow(Handle)
        && (mode != AttachMode.ChildOfTaskbar || User32.IsWindow(ParentHandle));

    public static WidgetWindow Create(TaskbarTarget target, IntPtr taskbarHandle, AttachMode mode)
    {
        WindowClassRegistry.Register(ClassName, RouteMessage);
        var handle = User32.CreateWindowEx(
            ExtendedStyleFor(mode),
            ClassName,
            string.Empty,
            WindowStyles.Popup,
            0,
            0,
            0,
            0,
            IntPtr.Zero,
            IntPtr.Zero,
            Kernel32.GetModuleHandle(null),
            IntPtr.Zero);

        if (mode == AttachMode.ChildOfTaskbar)
        {
            var childStyle = WindowStyles.Child | WindowStyles.ClipSiblings;
            User32.SetWindowLongPointer(handle, WindowLongIndex.Style, new IntPtr(childStyle));
            User32.SetParent(handle, taskbarHandle);
        }

        var window = new WidgetWindow(target, handle, taskbarHandle, mode);
        WindowsByHandle[handle] = window;
        return window;
    }

    public void Present(Bitmap bitmap, uint dpi)
    {
        RenderedSize = bitmap.Size;
        RenderedDpi = dpi;
        LayeredBitmapPresenter.Present(Handle, bitmap);
    }

    /// <summary>Moves the widget; returns true when its position actually changed.</summary>
    public bool Place(TaskbarInfo taskbar, Rectangle screenBounds)
    {
        var isChild = mode == AttachMode.ChildOfTaskbar;
        if (isChild && screenBounds == lastPlacement && IsAboveSiblings())
        {
            return false;
        }

        var position = isChild
            ? new Point(
                screenBounds.Left - taskbar.Bounds.Left,
                screenBounds.Top - taskbar.Bounds.Top)
            : screenBounds.Location;

        // Child: top of the taskbar's children, because after an Explorer restart the
        // taskbar's XAML content can be created after us and would otherwise cover us.
        var insertAfter = isChild ? SpecialWindowHandles.Top : SpecialWindowHandles.TopMost;
        var flags = SetWindowPositionFlags.NoActivate | SetWindowPositionFlags.ShowWindow;

        User32.SetWindowPos(
            Handle,
            insertAfter,
            position.X,
            position.Y,
            screenBounds.Width,
            screenBounds.Height,
            flags);

        var moved = screenBounds != lastPlacement;
        lastPlacement = screenBounds;
        return moved;
    }

    public string Describe()
    {
        User32.GetWindowRect(Handle, out var bounds);
        var visibility = User32.IsWindowVisible(Handle) ? "visible" : "hidden";
        var order = IsAboveSiblings() ? "on top" : "covered";
        return $"{visibility}, {order}, x {bounds.Left}..{bounds.Right}, y {bounds.Top}";
    }

    private bool IsAboveSiblings() =>
        User32.GetWindow(Handle, WindowRelationships.PreviousSibling) == IntPtr.Zero;

    public void Hide()
    {
        lastPlacement = Rectangle.Empty;
        User32.ShowWindow(Handle, ShowWindowCommands.Hide);
    }

    public void Dispose()
    {
        WindowsByHandle.Remove(Handle);
        if (User32.IsWindow(Handle))
        {
            User32.DestroyWindow(Handle);
        }
    }

    private static long ExtendedStyleFor(AttachMode mode)
    {
        var style = ExtendedWindowStyles.Layered
            | ExtendedWindowStyles.ToolWindow
            | ExtendedWindowStyles.NoActivate;
        return mode == AttachMode.TopMostOverlay ? style | ExtendedWindowStyles.TopMost : style;
    }

    private static IntPtr RouteMessage(
        IntPtr windowHandle,
        uint message,
        IntPtr wordParameter,
        IntPtr longParameter)
    {
        if (WindowsByHandle.TryGetValue(windowHandle, out var window)
            && window.HandleMouse(message))
        {
            return IntPtr.Zero;
        }

        return User32.DefWindowProc(windowHandle, message, wordParameter, longParameter);
    }

    private bool HandleMouse(uint message)
    {
        switch (message)
        {
            case WindowMessages.MouseMove:
                SetHovered(true);
                return true;
            case WindowMessages.MouseLeave:
                SetHovered(false);
                return true;
            case WindowMessages.LeftButtonUp:
                Clicked?.Invoke(this, EventArgs.Empty);
                return true;
            case WindowMessages.RightButtonUp:
                RightClicked?.Invoke(this, EventArgs.Empty);
                return true;
            default:
                return false;
        }
    }

    private void SetHovered(bool hovered)
    {
        if (IsHovered == hovered)
        {
            return;
        }

        IsHovered = hovered;
        if (hovered)
        {
            var tracking = new TrackMouseEventOptions
            {
                Size = (uint)Marshal.SizeOf<TrackMouseEventOptions>(),
                Flags = TrackMouseEventFlags.Leave,
                WindowHandle = Handle,
            };
            User32.TrackMouseEvent(ref tracking);
        }

        HoverChanged?.Invoke(this, EventArgs.Empty);
    }
}
