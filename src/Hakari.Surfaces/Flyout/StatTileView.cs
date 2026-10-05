using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Hakari.Surfaces.Flyout;

/// <summary>A money tile: label, value, detail, with the theme's tile styling.</summary>
internal static class StatTileView
{
    private const string TileBrushKey = "HakariTileBrush";
    private const string LineBrushKey = "HakariLineBrush";
    private const string CaptionStyleKey = "HakariCaptionText";
    private const string ValueStyleKey = "HakariValueText";
    private const string RadiusKey = "HakariControlRadius";
    private static readonly Thickness TilePadding = new(10, 8, 10, 8);

    public static Border Create(StatTile tile)
    {
        var resources = Application.Current.Resources;
        var content = new StackPanel { Spacing = 1 };
        content.Children.Add(Text(tile.Label, CaptionStyleKey));
        content.Children.Add(Text(tile.Value, ValueStyleKey));
        content.Children.Add(Text(tile.Detail, CaptionStyleKey));

        return new Border
        {
            Padding = TilePadding,
            CornerRadius = (CornerRadius)resources[RadiusKey],
            Background = (Brush)resources[TileBrushKey],
            BorderBrush = (Brush)resources[LineBrushKey],
            BorderThickness = new Thickness(1),
            Child = content,
        };
    }

    private static TextBlock Text(string text, string styleKey) => new()
    {
        Text = text,
        Style = (Style)Application.Current.Resources[styleKey],
    };
}
