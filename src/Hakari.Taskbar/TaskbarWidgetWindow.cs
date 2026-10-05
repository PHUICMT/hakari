using System.Drawing;
using System.Runtime.InteropServices;
using Hakari.Taskbar.Interop;
using Hakari.Taskbar.Placement;
using Hakari.Taskbar.Rendering;

namespace Hakari.Taskbar;

/// <summary>
/// The Hakari meter drawn on the taskbar. Every method runs on the thread that created it;
/// other threads hand over content with <see cref="PostContent"/>.
/// </summary>
public sealed class TaskbarWidgetWindow : IDisposable
{
    private const string ClassName = "HakariTaskbarWidget";
    private const string TaskbarCreatedMessageName = "TaskbarCreated";
    private const uint ContentChangedMessage = WindowMessages.Application + 1;
    private const uint PlacementTimerInterval = 1000;
    private static readonly IntPtr PlacementTimerId = new(1);

    private readonly AttachMode mode;
    private readonly WidgetRenderer renderer = new();
    private readonly WindowProcedure windowProcedure;
    private readonly uint taskbarCreatedMessage;
    private readonly object pendingLock = new();

    private IntPtr handle;
    private TaskbarInfo? taskbar;
    private WidgetContent content;
    private WidgetContent? pendingContent;
    private Size renderedSize;
    private Rectangle lastPlacement;
    private bool hovered;
    private bool hiddenForFullScreen;

    public TaskbarWidgetWindow(AttachMode mode, WidgetContent initialContent)
    {
        this.mode = mode;
        content = initialContent;
        windowProcedure = HandleMessage;
        taskbarCreatedMessage = User32.RegisterWindowMessage(TaskbarCreatedMessageName);
        RegisterWindowClass();
        CreateAndAttach();
        User32.SetTimer(handle, PlacementTimerId, PlacementTimerInterval, IntPtr.Zero);
    }

    public event EventHandler? Clicked;

    public event EventHandler? RightClicked;

    public WidgetDiagnostics Diagnostics { get; } = new();

    /// <summary>Thread-safe: queues new content and wakes the widget thread.</summary>
    public void PostContent(WidgetContent newContent)
    {
        lock (pendingLock)
        {
            pendingContent = newContent;
        }

        User32.PostMessage(handle, ContentChangedMessage, IntPtr.Zero, IntPtr.Zero);
    }

    public void Dispose()
    {
        if (handle != IntPtr.Zero)
        {
            User32.DestroyWindow(handle);
            handle = IntPtr.Zero;
        }

        renderer.Dispose();
    }

    private void RegisterWindowClass()
    {
        var definition = new WindowClassDefinition
        {
            Size = (uint)Marshal.SizeOf<WindowClassDefinition>(),
            WindowProcedure = Marshal.GetFunctionPointerForDelegate(windowProcedure),
            Instance = Kernel32.GetModuleHandle(null),
            ClassName = ClassName,
        };

        User32.RegisterClassEx(ref definition);
    }

    private void CreateAndAttach()
    {
        taskbar = TaskbarLocator.FindPrimary();
        var extendedStyle = ExtendedWindowStyles.Layered
            | ExtendedWindowStyles.ToolWindow
            | ExtendedWindowStyles.NoActivate;
        if (mode == AttachMode.TopMostOverlay)
        {
            extendedStyle |= ExtendedWindowStyles.TopMost;
        }

        handle = User32.CreateWindowEx(
            extendedStyle,
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

        if (mode == AttachMode.ChildOfTaskbar && taskbar is not null)
        {
            AttachAsChild(taskbar.Handle);
        }

        Redraw();
    }

    private void AttachAsChild(IntPtr taskbarHandle)
    {
        var childStyle = WindowStyles.Child | WindowStyles.ClipSiblings;
        User32.SetWindowLongPointer(handle, WindowLongIndex.Style, new IntPtr(childStyle));
        User32.SetParent(handle, taskbarHandle);
    }

    private void ReattachAfterExplorerRestart()
    {
        Diagnostics.ExplorerRestarts++;
        User32.DestroyWindow(handle);
        lastPlacement = Rectangle.Empty;
        CreateAndAttach();
    }

    private void Redraw()
    {
        var palette = TaskbarTheme.IsLight()
            ? WidgetPalette.LightTaskbar
            : WidgetPalette.DarkTaskbar;
        var scale = taskbar?.Scale ?? 1.0;
        using var bitmap = renderer.Render(content, palette, scale, hovered);
        renderedSize = bitmap.Size;
        LayeredBitmapPresenter.Present(handle, bitmap);
        Diagnostics.Renders++;
        UpdatePlacement(force: true);
    }

    private void UpdatePlacement(bool force)
    {
        var current = TaskbarLocator.FindPrimary();
        if (current is null)
        {
            return;
        }

        taskbar = current;
        if (ShouldHideForFullScreen())
        {
            return;
        }

        var target = WidgetPlacement.LeftOfNotificationArea(current, renderedSize);
        if (target is not { } screenBounds)
        {
            Diagnostics.Collisions++;
            User32.ShowWindow(handle, ShowWindowCommands.Hide);
            return;
        }

        if (!force && screenBounds == lastPlacement && mode == AttachMode.ChildOfTaskbar)
        {
            return;
        }

        MoveTo(current, screenBounds);
    }

    private void MoveTo(TaskbarInfo current, Rectangle screenBounds)
    {
        var isChild = mode == AttachMode.ChildOfTaskbar;
        var position = isChild
            ? new Point(
                screenBounds.Left - current.Bounds.Left,
                screenBounds.Top - current.Bounds.Top)
            : screenBounds.Location;
        var insertAfter = isChild ? IntPtr.Zero : SpecialWindowHandles.TopMost;
        var flags = SetWindowPositionFlags.NoActivate | SetWindowPositionFlags.ShowWindow;
        if (isChild)
        {
            flags |= SetWindowPositionFlags.NoZOrder;
        }

        User32.SetWindowPos(
            handle,
            insertAfter,
            position.X,
            position.Y,
            screenBounds.Width,
            screenBounds.Height,
            flags);

        if (screenBounds != lastPlacement)
        {
            Diagnostics.Moves++;
        }

        lastPlacement = screenBounds;
    }

    /// <summary>The child mode hides with the taskbar on its own; the overlay must do it.</summary>
    private bool ShouldHideForFullScreen()
    {
        var fullScreen = mode == AttachMode.TopMostOverlay
            && FullScreenDetector.IsSomethingFullScreen();
        if (fullScreen && !hiddenForFullScreen)
        {
            User32.ShowWindow(handle, ShowWindowCommands.Hide);
        }

        hiddenForFullScreen = fullScreen;
        return fullScreen;
    }

    private void ApplyPendingContent()
    {
        lock (pendingLock)
        {
            if (pendingContent is null)
            {
                return;
            }

            content = pendingContent;
            pendingContent = null;
        }

        Redraw();
    }

    private void SetHovered(bool value)
    {
        if (hovered == value)
        {
            return;
        }

        hovered = value;
        if (value)
        {
            var tracking = new TrackMouseEventOptions
            {
                Size = (uint)Marshal.SizeOf<TrackMouseEventOptions>(),
                Flags = TrackMouseEventFlags.Leave,
                WindowHandle = handle,
            };
            User32.TrackMouseEvent(ref tracking);
        }

        Redraw();
    }

    private IntPtr HandleMessage(
        IntPtr windowHandle,
        uint message,
        IntPtr wordParameter,
        IntPtr longParameter)
    {
        if (message == taskbarCreatedMessage)
        {
            ReattachAfterExplorerRestart();
            return IntPtr.Zero;
        }

        switch (message)
        {
            case ContentChangedMessage:
                ApplyPendingContent();
                return IntPtr.Zero;
            case WindowMessages.Timer:
                UpdatePlacement(force: false);
                return IntPtr.Zero;
            case WindowMessages.DpiChanged:
            case WindowMessages.DisplayChange:
            case WindowMessages.SettingChange:
                Redraw();
                return IntPtr.Zero;
            case WindowMessages.MouseMove:
                SetHovered(true);
                return IntPtr.Zero;
            case WindowMessages.MouseLeave:
                SetHovered(false);
                return IntPtr.Zero;
            case WindowMessages.LeftButtonUp:
                Clicked?.Invoke(this, EventArgs.Empty);
                return IntPtr.Zero;
            case WindowMessages.RightButtonUp:
                RightClicked?.Invoke(this, EventArgs.Empty);
                return IntPtr.Zero;
            default:
                return User32.DefWindowProc(windowHandle, message, wordParameter, longParameter);
        }
    }
}
