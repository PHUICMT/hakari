namespace Hakari.Taskbar;

public enum AttachMode
{
    /// <summary>
    /// Child window of the taskbar. Follows auto-hide and full screen for free, but shares an
    /// input queue with Explorer, so this thread must never block.
    /// </summary>
    ChildOfTaskbar,

    /// <summary>
    /// Separate top-most popup over the taskbar. Fully isolated from Explorer, but it must
    /// re-assert its z-order and follow the taskbar itself.
    /// </summary>
    TopMostOverlay,
}
