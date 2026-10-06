using Hakari.Core.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Hakari.Surfaces.Dashboard;

/// <summary>"Less ▢▢▢▢▢ More": what the heat map's shades mean.</summary>
internal static class HeatScale
{
    private const double Square = 12;
    private const double TextSize = 11;

    public static StackPanel Create()
    {
        var scale = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 3,
            Margin = new Thickness(0, 12, 0, 0),
        };
        scale.Children.Add(Text(Texts.Get("dashboard.heat.less")));
        for (var level = 0; level < HeatMap.LevelCount; level++)
        {
            scale.Children.Add(new Border
            {
                Width = Square,
                Height = Square,
                CornerRadius = new CornerRadius(2),
                Background = DashboardCard.Brush(HeatMap.LevelBrush(level)),
                VerticalAlignment = VerticalAlignment.Center,
            });
        }

        scale.Children.Add(Text(Texts.Get("dashboard.heat.more")));
        return scale;
    }

    private static TextBlock Text(string text) => new()
    {
        Text = text,
        FontSize = TextSize,
        Foreground = DashboardCard.Brush("HakariInkFaintBrush"),
        VerticalAlignment = VerticalAlignment.Center,
    };
}
