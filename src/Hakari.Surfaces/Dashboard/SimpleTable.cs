using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Hakari.Surfaces.Dashboard;

/// <summary>
/// A short table of text: a header row and a line per row, each row set off by a hairline.
/// Columns that are not essential drop out when the table gets narrow, so the rest never get
/// cut off. For tables of a few rows; long ones belong in the virtualized TableView.
/// </summary>
internal static class SimpleTable
{
    private const double HeaderSize = 11.5;
    private const double RowSize = 12.5;
    private static readonly Thickness CellPadding = new(0, 8, 12, 8);
    private static readonly Thickness DividerLine = new(0, 1, 0, 0);

    /// <param name="rows">Per row, a string or element for each column.</param>
    public static StackPanel Create(
        IReadOnlyList<SimpleColumn> columns,
        IReadOnlyList<IReadOnlyList<object>> rows)
    {
        var table = new StackPanel();
        var grids = new List<Grid>
        {
            Row(columns, columns.Select(column => (object)column.Header).ToList(), isHeader: true),
        };
        grids.AddRange(rows.Select(row => Row(columns, row, isHeader: false)));
        foreach (var grid in grids)
        {
            table.Children.Add(grid);
        }

        WidthSteps.Watch(table, [.. columns.Select(column => column.ShownFrom).Distinct().Order()],
            _ => ApplyWidths(table, columns, grids));
        return table;
    }

    private static Grid Row(
        IReadOnlyList<SimpleColumn> columns,
        IReadOnlyList<object> cells,
        bool isHeader)
    {
        var row = new Grid();
        for (var index = 0; index < columns.Count; index++)
        {
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = columns[index].Width });
            var cell = Cell(cells[index], columns[index], isHeader);
            Grid.SetColumn(cell, index);
            row.Children.Add(cell);
        }

        if (!isHeader)
        {
            row.BorderThickness = DividerLine;
            row.BorderBrush = DashboardCard.Brush("HakariLineBrush");
        }

        return row;
    }

    private static FrameworkElement Cell(object content, SimpleColumn column, bool isHeader)
    {
        var alignment = column.IsNumber ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        if (content is FrameworkElement element)
        {
            element.HorizontalAlignment = alignment;
            element.VerticalAlignment = VerticalAlignment.Center;
            return new Border { Padding = CellPadding, Child = element };
        }

        return new TextBlock
        {
            Text = content.ToString(),
            FontSize = isHeader ? HeaderSize : RowSize,
            Foreground = DashboardCard.Brush(isHeader ? "HakariInkFaintBrush" : "HakariInkBrush"),
            Padding = CellPadding,
            HorizontalAlignment = alignment,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
    }

    /// <summary>A column too wide for the table is given no width at all.</summary>
    private static void ApplyWidths(
        FrameworkElement table,
        IReadOnlyList<SimpleColumn> columns,
        IEnumerable<Grid> grids)
    {
        foreach (var grid in grids)
        {
            for (var index = 0; index < columns.Count; index++)
            {
                var shown = table.ActualWidth >= columns[index].ShownFrom;
                grid.ColumnDefinitions[index].Width =
                    shown ? columns[index].Width : new GridLength(0);
                grid.Children[index].Visibility =
                    shown ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }
}
