using Hakari.Surfaces.Motion;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.Foundation;

namespace Hakari.Surfaces.Dashboard;

/// <summary>
/// The small card that follows the pointer over a chart and says what is under it: a title
/// and a line per series. It floats above everything, never takes the pointer, and flips to
/// the other side of the pointer near the window's edge so it is never cut off.
/// </summary>
internal sealed class ChartReadout
{
    private const double Offset = 14;
    private const double TitleSize = 12;
    private const double RowSize = 12;
    private const double Swatch = 9;
    private static readonly Thickness CardPadding = new(10, 8, 10, 8);

    private readonly Popup popup = new() { IsHitTestVisible = false };
    private readonly Border card;
    private readonly StackPanel body = new() { Spacing = 4 };
    private string? shownTitle;

    public ChartReadout()
    {
        card = new Border
        {
            Child = body,
            Padding = CardPadding,
            MinWidth = 120,
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            BorderBrush = DashboardCard.Brush("HakariLineStrongBrush"),
            Background = DashboardCard.Brush("HakariSurfaceBrush"),
            Opacity = 0,
        };
        popup.Child = card;
    }

    public void Show(
        FrameworkElement anchor,
        Point at,
        string title,
        IReadOnlyList<ReadoutRow> rows)
    {
        popup.XamlRoot = anchor.XamlRoot;
        if (shownTitle != title)
        {
            shownTitle = title;
            Fill(title, rows);
        }

        card.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var origin = anchor.TransformToVisual(null).TransformPoint(at);
        var root = anchor.XamlRoot.Size;
        var left = origin.X + Offset;
        if (left + card.DesiredSize.Width > root.Width)
        {
            left = origin.X - Offset - card.DesiredSize.Width;
        }

        var top = origin.Y + Offset;
        if (top + card.DesiredSize.Height > root.Height)
        {
            top = origin.Y - Offset - card.DesiredSize.Height;
        }

        popup.HorizontalOffset = Math.Max(0, left);
        popup.VerticalOffset = Math.Max(0, top);
        if (!popup.IsOpen)
        {
            popup.IsOpen = true;
            SurfaceMotion.Settle(card, "Opacity", 1);
        }
    }

    public void Hide()
    {
        shownTitle = null;
        popup.IsOpen = false;
        card.Opacity = 0;
    }

    private void Fill(string title, IReadOnlyList<ReadoutRow> rows)
    {
        body.Children.Clear();
        body.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = TitleSize,
            FontWeight = FontWeights.SemiBold,
            Foreground = DashboardCard.Brush("HakariInkBrush"),
        });
        foreach (var row in rows)
        {
            body.Children.Add(Line(row));
        }
    }

    private static Grid Line(ReadoutRow row)
    {
        var line = new Grid { ColumnSpacing = 8 };
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        line.ColumnDefinitions.Add(new ColumnDefinition());
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        if (row.BrushKey is { } brushKey)
        {
            line.Children.Add(new Border
            {
                Width = Swatch,
                Height = Swatch,
                CornerRadius = new CornerRadius(2),
                Background = DashboardCard.Brush(brushKey),
                VerticalAlignment = VerticalAlignment.Center,
            });
        }

        var label = Text(row.Label, "HakariInkMutedBrush");
        Grid.SetColumn(label, 1);
        var value = Text(row.Value, "HakariInkBrush");
        Grid.SetColumn(value, 2);
        value.HorizontalAlignment = HorizontalAlignment.Right;
        line.Children.Add(label);
        line.Children.Add(value);
        return line;
    }

    private static TextBlock Text(string text, string brushKey) => new()
    {
        Text = text,
        FontSize = RowSize,
        Foreground = DashboardCard.Brush(brushKey),
    };
}
