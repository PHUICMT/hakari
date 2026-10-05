using Hakari.Core.Settings;
using Hakari.Surfaces.Motion;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace Hakari.Surfaces.Dashboard;

/// <summary>
/// Placeholder cards shaped like a page while its data is read: a row of tiles and two
/// larger cards, gently pulsing. Off draws them still.
/// </summary>
internal static class LoadingSkeleton
{
    private const int TileCount = 4;
    private const double TileHeight = 96;
    private const double CardHeight = 120;
    private const double TallCardHeight = 240;
    private const double Spacing = 16;
    private const double PulseLow = 0.45;
    private static readonly TimeSpan PulseHalf = TimeSpan.FromMilliseconds(700);

    public static StackPanel Create()
    {
        var page = new StackPanel { Spacing = Spacing };
        var tiles = new Grid { ColumnSpacing = 12 };
        for (var index = 0; index < TileCount; index++)
        {
            tiles.ColumnDefinitions.Add(new ColumnDefinition());
            var tile = Block(TileHeight);
            Grid.SetColumn(tile, index);
            tiles.Children.Add(tile);
        }

        page.Children.Add(tiles);
        page.Children.Add(Block(CardHeight));
        page.Children.Add(Block(TallCardHeight));
        Pulse(page);
        return page;
    }

    private static Border Block(double height) => new()
    {
        Height = height,
        Style = (Style)Application.Current.Resources["HakariCard"],
    };

    private static void Pulse(UIElement page)
    {
        if (SurfaceMotion.Current() == AnimationSetting.Off)
        {
            return;
        }

        var pulse = new DoubleAnimation
        {
            From = 1,
            To = PulseLow,
            Duration = PulseHalf,
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
        };
        Storyboard.SetTarget(pulse, page);
        Storyboard.SetTargetProperty(pulse, "Opacity");
        var storyboard = new Storyboard();
        storyboard.Children.Add(pulse);
        if (page is FrameworkElement element)
        {
            element.Loaded += (_, _) => storyboard.Begin();
            element.Unloaded += (_, _) => storyboard.Stop();
        }
    }
}
