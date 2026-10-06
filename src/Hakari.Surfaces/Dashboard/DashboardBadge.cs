using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Hakari.Surfaces.Dashboard;

/// <summary>A small pill: a state said in a word, tinted by what it means.</summary>
internal static class DashboardBadge
{
    private const double TextSize = 11.5;
    private static readonly Thickness BadgePadding = new(8, 2, 8, 3);

    public static Border Create(string text, BadgeTone tone)
    {
        var (background, foreground) = tone switch
        {
            BadgeTone.Ok => ("HakariOkSoftBrush", "HakariOkBrush"),
            BadgeTone.Warn => ("HakariWarnSoftBrush", "HakariWarnBrush"),
            BadgeTone.Critical => ("HakariCriticalSoftBrush", "HakariCriticalBrush"),
            BadgeTone.Accent => ("HakariAccentSoftBrush", "HakariAccentBrush"),
            _ => ("HakariGroundBrush", "HakariInkMutedBrush"),
        };
        return new Border
        {
            Background = DashboardCard.Brush(background),
            CornerRadius = new CornerRadius(10),
            Padding = BadgePadding,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Child = new TextBlock
            {
                Text = text,
                FontSize = TextSize,
                Foreground = DashboardCard.Brush(foreground),
                TextWrapping = TextWrapping.NoWrap,
            },
        };
    }
}
