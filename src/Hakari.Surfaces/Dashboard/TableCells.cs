using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Hakari.Surfaces.Dashboard;

/// <summary>Cells the dashboard's tables share: two lines, a badge, rows that open.</summary>
internal static class TableCells
{
    private const double DetailSize = 11;
    private const double BadgeSize = 11;
    private static readonly Thickness BadgePadding = new(6, 1, 6, 2);

    /// <summary>A title over a faint detail, both cut short with the whole in a tooltip.</summary>
    public static StackPanel TwoLines(string title, string? detail)
    {
        var cell = new StackPanel();
        cell.Children.Add(new TextBlock
        {
            Text = title,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        if (!string.IsNullOrEmpty(detail))
        {
            cell.Children.Add(new TextBlock
            {
                Text = detail,
                FontSize = DetailSize,
                Foreground = DashboardCard.Brush("HakariInkFaintBrush"),
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
            ToolTipService.SetToolTip(cell, detail);
        }

        return cell;
    }

    /// <summary>A rounded label such as "WSL" or "High", in a quiet or a warning tone.</summary>
    public static Border Badge(string text, bool isWarning = false) => new()
    {
        Padding = BadgePadding,
        CornerRadius = new CornerRadius(4),
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Center,
        Background = DashboardCard.Brush(isWarning ? "HakariWarnSoftBrush" : "HakariTileBrush"),
        BorderBrush = DashboardCard.Brush(isWarning ? "HakariWarnBrush" : "HakariLineBrush"),
        BorderThickness = new Thickness(1),
        Child = new TextBlock
        {
            Text = text,
            FontSize = BadgeSize,
            Foreground = DashboardCard.Brush(isWarning ? "HakariWarnBrush" : "HakariInkMutedBrush"),
        },
    };

    /// <summary>A figure with a badge after it, kept to the right like the other numbers.</summary>
    public static StackPanel WithBadge(string figure, Border badge)
    {
        var cell = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        cell.Children.Add(new TextBlock
        {
            Text = figure,
            VerticalAlignment = VerticalAlignment.Center,
        });
        cell.Children.Add(badge);
        return cell;
    }

    /// <summary>The rows below the header light up under the pointer and open.</summary>
    public static void MakeRowsOpen(Panel table, Action<int> open)
    {
        for (var index = 1; index < table.Children.Count; index++)
        {
            if (table.Children[index] is not Grid row)
            {
                continue;
            }

            var position = row.Tag is int first ? first : index - 1;
            row.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            row.CornerRadius = new CornerRadius(4);
            row.PointerEntered += (_, _) =>
                row.Background = DashboardCard.Brush("HakariHoverBrush");
            row.PointerExited += (_, _) =>
                row.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            row.Tapped += (_, _) => open(position);
            Hakari.Surfaces.Controls.HandCursor.Apply(row);
        }
    }
}
