using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Hakari.Surfaces.Dashboard;

/// <summary>A headline number: label, value, and a detail line that may carry a color.</summary>
internal static class DashboardTile
{
    private const double LabelSize = 12;
    private const double ValueSize = 24;
    private const double DetailSize = 12;
    private const double LineSpacing = 4;
    private static readonly Thickness TilePadding = new(16, 14, 16, 14);

    /// <param name="detailBrushKey">A tone for the detail, such as a rise in cost.</param>
    public static Border Create(
        string label,
        string value,
        string? detail,
        string? detailBrushKey = null)
    {
        var stack = new StackPanel { Spacing = LineSpacing };
        stack.Children.Add(Text(label, LabelSize, "HakariInkMutedBrush"));
        var valueText = Text(value, ValueSize, "HakariInkBrush");
        valueText.FontWeight = FontWeights.SemiBold;
        stack.Children.Add(valueText);
        if (detail is not null)
        {
            stack.Children.Add(Text(detail, DetailSize, detailBrushKey ?? "HakariInkFaintBrush"));
        }

        return new Border
        {
            Style = (Style)Application.Current.Resources["HakariCard"],
            Padding = TilePadding,
            Child = stack,
        };
    }

    private static TextBlock Text(string text, double size, string brushKey) => new()
    {
        Text = text,
        FontSize = size,
        Foreground = DashboardCard.Brush(brushKey),
        TextTrimming = TextTrimming.CharacterEllipsis,
    };
}
