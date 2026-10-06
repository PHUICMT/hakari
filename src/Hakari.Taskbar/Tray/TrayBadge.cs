using Hakari.Taskbar.Rendering;

namespace Hakari.Taskbar.Tray;

/// <summary>
/// Shown on the tray icon instead of the logo: a whole percent and tone, or with an amount,
/// today's spending under its currency code.
/// </summary>
public sealed record TrayBadge(
    int Percent,
    WidgetTone Tone,
    string? Amount = null,
    string? Unit = null)
{
    public const int FullPercent = 100;

    public bool IsFull => Percent >= FullPercent;
}
