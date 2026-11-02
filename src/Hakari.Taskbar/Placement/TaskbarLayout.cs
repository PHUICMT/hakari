using System.Drawing;

namespace Hakari.Taskbar.Placement;

/// <summary>
/// Where the taskbar's XAML content really is. Win32 child windows such as ReBarWindow32 no
/// longer match it on Windows 11, so this comes from UI Automation.
/// </summary>
/// <param name="FreeBeforeStart">
/// The empty stretch left of Start, which a centered taskbar has and a left-aligned one does not.
/// </param>
public sealed record TaskbarLayout(
    Rectangle AppButtons,
    Rectangle NotificationArea,
    Rectangle FreeBeforeStart);
