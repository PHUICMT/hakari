using System.Runtime.InteropServices;
using Hakari.Taskbar.Interop;

namespace Hakari.Taskbar.Tray;

/// <summary>
/// Hakari's notification-area icon. It owns a hidden window, which receives the icon's mouse
/// events and "TaskbarCreated", so the icon comes back after Explorer restarts.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private const string ClassName = "HakariTrayIcon";
    private const string TaskbarCreatedMessageName = "TaskbarCreated";
    private const uint CallbackMessage = WindowMessages.Application + 10;
    private const uint IconId = 1;
    private const int FirstMenuCommand = 1;
    private const int MaximumTipCharacters = NotifyIconData.TipLength - 1;

    private readonly Func<IReadOnlyList<TrayMenuItem>> buildMenu;
    private readonly uint taskbarCreatedMessage;
    private readonly IntPtr windowHandle;
    private IntPtr iconHandle;
    private TrayBadge? shownBadge;
    private string tooltip;

    public TrayIcon(string tooltip, Func<IReadOnlyList<TrayMenuItem>> buildMenu)
    {
        this.tooltip = tooltip;
        this.buildMenu = buildMenu;
        taskbarCreatedMessage = User32.RegisterWindowMessage(TaskbarCreatedMessageName);
        WindowClassRegistry.Register(ClassName, HandleMessage);
        windowHandle = User32.CreateWindowEx(
            ExtendedWindowStyles.ToolWindow,
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
        iconHandle = TrayIconArtwork.CreateIcon();
        AddIcon();
    }

    /// <summary>Left click on the icon.</summary>
    public event EventHandler? Selected;

    /// <summary>
    /// Right click, with the pointer's screen position. When nobody handles it, the system
    /// menu shows instead.
    /// </summary>
    public event EventHandler<System.Drawing.Point>? MenuRequested;

    /// <summary>
    /// A menu action or an icon event threw. It is reported here instead of crossing back
    /// into Windows, which would end the process.
    /// </summary>
    public event Action<Exception>? Faulted;

    public void SetTooltip(string text)
    {
        tooltip = text;
        var data = CreateData(NotifyIconConstants.TipFlag | NotifyIconConstants.ShowTipFlag);
        Shell32.Shell_NotifyIcon(NotifyIconConstants.Modify, ref data);
    }

    /// <summary>
    /// A notification from the icon. It stays quiet while Windows is asked not to disturb the
    /// user, and a warning is shown with the warning icon.
    /// </summary>
    public void ShowBalloon(string title, string text, bool isWarning)
    {
        var data = CreateData(NotifyIconConstants.InfoFlag);
        data.Info = text.Length > NotifyIconData.InfoLength - 1
            ? text[..(NotifyIconData.InfoLength - 1)]
            : text;
        data.InfoTitle = title.Length > NotifyIconData.InfoTitleLength - 1
            ? title[..(NotifyIconData.InfoTitleLength - 1)]
            : title;
        data.InfoFlags = NotifyIconConstants.InfoRespectQuietTime
            | (isWarning ? NotifyIconConstants.InfoWarning : NotifyIconConstants.InfoNone);
        Shell32.Shell_NotifyIcon(NotifyIconConstants.Modify, ref data);
    }

    /// <summary>Shows a limit on the icon, or the logo with null. Unchanged is free.</summary>
    public void SetBadge(TrayBadge? badge)
    {
        if (badge == shownBadge)
        {
            return;
        }

        shownBadge = badge;
        var previous = iconHandle;
        iconHandle = badge is null
            ? TrayIconArtwork.CreateIcon()
            : TrayIconArtwork.CreateBadge(badge);
        var data = CreateData(NotifyIconConstants.IconFlag);
        Shell32.Shell_NotifyIcon(NotifyIconConstants.Modify, ref data);
        User32.DestroyIcon(previous);
    }

    /// <summary>Shows the menu at the pointer; also used for the widget's right click.</summary>
    public void ShowMenu()
    {
        var items = buildMenu();
        var menu = User32.CreatePopupMenu();
        try
        {
            AppendItems(menu, items);
            User32.GetCursorPos(out var cursor);

            // Without this the menu does not close when clicking elsewhere.
            User32.SetForegroundWindow(windowHandle);
            var flags = MenuConstants.ReturnCommand
                | MenuConstants.RightAlign
                | MenuConstants.BottomAlign;
            var command = (int)User32.TrackPopupMenuEx(
                menu,
                flags,
                cursor.X,
                cursor.Y,
                windowHandle,
                IntPtr.Zero);
            User32.PostMessage(windowHandle, MenuConstants.NullMessage, IntPtr.Zero, IntPtr.Zero);
            RunCommand(items, command);
        }
        finally
        {
            User32.DestroyMenu(menu);
        }
    }

    public void Dispose()
    {
        var data = CreateData(flags: 0);
        Shell32.Shell_NotifyIcon(NotifyIconConstants.Delete, ref data);
        User32.DestroyIcon(iconHandle);
        User32.DestroyWindow(windowHandle);
    }

    private static void AppendItems(IntPtr menu, IReadOnlyList<TrayMenuItem> items)
    {
        for (var position = 0; position < items.Count; position++)
        {
            var item = items[position];
            if (item.IsSeparator)
            {
                User32.AppendMenu(menu, MenuConstants.Separator, IntPtr.Zero, null);
                continue;
            }

            var flags = MenuConstants.String | (item.IsChecked ? MenuConstants.Checked : 0);
            User32.AppendMenu(menu, flags, new IntPtr(position + FirstMenuCommand), item.Label);
        }
    }

    private static void RunCommand(IReadOnlyList<TrayMenuItem> items, int command)
    {
        var position = command - FirstMenuCommand;
        if (position >= 0 && position < items.Count)
        {
            items[position].OnSelect?.Invoke();
        }
    }

    private void AddIcon()
    {
        var flags = NotifyIconConstants.MessageFlag
            | NotifyIconConstants.IconFlag
            | NotifyIconConstants.TipFlag
            | NotifyIconConstants.ShowTipFlag;
        var data = CreateData(flags);
        Shell32.Shell_NotifyIcon(NotifyIconConstants.Add, ref data);
        data.VersionOrTimeout = NotifyIconConstants.Version4;
        Shell32.Shell_NotifyIcon(NotifyIconConstants.SetVersion, ref data);
    }

    private NotifyIconData CreateData(uint flags) => new()
    {
        Size = (uint)Marshal.SizeOf<NotifyIconData>(),
        WindowHandle = windowHandle,
        Id = IconId,
        Flags = flags,
        CallbackMessage = CallbackMessage,
        Icon = iconHandle,
        Tip = tooltip.Length > MaximumTipCharacters ? tooltip[..MaximumTipCharacters] : tooltip,
        Info = string.Empty,
        InfoTitle = string.Empty,
    };

    private IntPtr HandleMessage(
        IntPtr handle,
        uint message,
        IntPtr wordParameter,
        IntPtr longParameter)
    {
        try
        {
            return Dispatch(handle, message, wordParameter, longParameter);
        }
        catch (Exception exception)
        {
            Faulted?.Invoke(exception);
            return IntPtr.Zero;
        }
    }

    private IntPtr Dispatch(
        IntPtr handle,
        uint message,
        IntPtr wordParameter,
        IntPtr longParameter)
    {
        if (message == taskbarCreatedMessage)
        {
            AddIcon();
            return IntPtr.Zero;
        }

        if (message == CallbackMessage)
        {
            HandleIconEvent((uint)(longParameter.ToInt64() & 0xFFFF));
            return IntPtr.Zero;
        }

        return User32.DefWindowProc(handle, message, wordParameter, longParameter);
    }

    private void RequestMenu()
    {
        if (MenuRequested is null)
        {
            ShowMenu();
            return;
        }

        User32.GetCursorPos(out var cursor);
        MenuRequested(this, new System.Drawing.Point(cursor.X, cursor.Y));
    }

    private void HandleIconEvent(uint iconEvent)
    {
        switch (iconEvent)
        {
            case NotifyIconConstants.SelectEvent:
            case WindowMessages.LeftButtonUp:
                Selected?.Invoke(this, EventArgs.Empty);
                break;
            case NotifyIconConstants.ContextMenuEvent:
            case WindowMessages.RightButtonUp:
                RequestMenu();
                break;
        }
    }
}
