using Hakari.Taskbar.Rendering;

namespace Hakari.Taskbar.Tray;

/// <summary>Shown on the tray icon instead of the logo: a whole percent and tone.</summary>
public sealed record TrayBadge(int Percent, WidgetTone Tone)
{
    public const int FullPercent = 100;

    public bool IsFull => Percent >= FullPercent;
}
