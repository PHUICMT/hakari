using System.Drawing;

namespace Hakari.Taskbar.Placement;

/// <summary>
/// Where the taskbar's XAML content really is. Win32 child windows such as ReBarWindow32 no
/// longer match it on Windows 11, so this comes from UI Automation.
/// </summary>
public sealed record TaskbarLayout(Rectangle AppButtons, Rectangle NotificationArea);
