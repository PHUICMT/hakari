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
        grids.AddRange(rows.Select((row, index) =>
        {
            var grid = Row(columns, row, isHeader: false);
            grid.Tag = index;
            return grid;
        }));
        foreach (var grid in grids)
        {
            table.Children.Add(grid);
        }

        WidthSteps.Watch(table, [.. columns.Select(column => column.ShownFrom).Distinct().Order()],
            _ => ApplyWidths(table, columns, grids));
        return table;
    }

    private const string UpGlyph = "\uE70E";
    private const string DownGlyph = "\uE70D";
    private const double ArrowSize = 9;

    /// <summary>
    /// The same table, with headers that sort: a click sorts by that column, a second click
    /// turns the order round. Figures start largest first, words from A. Each row keeps its
    /// first position in <see cref="FrameworkElement.Tag"/>, for whatever opens it.
    /// </summary>
    /// <param name="keys">Per row, what each column sorts by; null where it does not sort.</param>
    public static StackPanel CreateSortable(
        IReadOnlyList<SimpleColumn> columns,
        IReadOnlyList<IReadOnlyList<object>> rows,
        IReadOnlyList<IReadOnlyList<IComparable?>> keys)
    {
        var table = Create(columns, rows);
        if (table.Children[0] is not Grid header)
        {
            return table;
        }

        var sortedBy = -1;
        var descending = false;
        var arrows = new List<FontIcon>();
        for (var column = 0; column < columns.Count; column++)
        {
            if (keys.Count == 0 || keys[0][column] is null
                || header.Children[column] is not TextBlock title)
            {
                arrows.Add(new FontIcon());
                continue;
            }

            var position = column;
            var arrow = new FontIcon
            {
                FontSize = ArrowSize,
                FontFamily = (Microsoft.UI.Xaml.Media.FontFamily)
                    Application.Current.Resources["HakariIconFont"],
                Foreground = DashboardCard.Brush("HakariAccentBrush"),
                Visibility = Visibility.Collapsed,
            };
            arrows.Add(arrow);
            var label = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            if (columns[column].IsNumber)
            {
                label.Children.Add(arrow);
            }

            label.Children.Add(new TextBlock { Text = title.Text, FontSize = HeaderSize });
            if (!columns[column].IsNumber)
            {
                label.Children.Add(arrow);
            }

            var button = new Button
            {
                Content = label,
                Style = (Style)Application.Current.Resources["HakariSubtleButton"],
                Padding = new Thickness(4, 4, 4, 4),
                Margin = new Thickness(-4, 0, 8, 0),
                HorizontalAlignment = title.HorizontalAlignment,
                VerticalAlignment = VerticalAlignment.Center,
            };
            button.Click += (_, _) =>
            {
                descending = sortedBy == position ? !descending : columns[position].IsNumber;
                sortedBy = position;
                for (var index = 0; index < arrows.Count; index++)
                {
                    arrows[index].Visibility =
                        index == position ? Visibility.Visible : Visibility.Collapsed;
                }

                arrows[position].Glyph = descending ? DownGlyph : UpGlyph;
                Sort(table, keys, position, descending);
            };
            Grid.SetColumn(button, column);
            header.Children[column] = button;
        }

        return table;
    }

    /// <summary>A header's label with room for the sort arrow, on the figures' side.</summary>
    internal static (Button Button, FontIcon Arrow) SortButton(string text, bool isNumber)
    {
        var arrow = new FontIcon
        {
            FontSize = ArrowSize,
            FontFamily = (Microsoft.UI.Xaml.Media.FontFamily)
                Application.Current.Resources["HakariIconFont"],
            Foreground = DashboardCard.Brush("HakariAccentBrush"),
            Visibility = Visibility.Collapsed,
        };
        var label = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        var title = new TextBlock
        {
            Text = text,
            FontSize = HeaderSize,
            Foreground = DashboardCard.Brush("HakariInkFaintBrush"),
        };
        if (isNumber)
        {
            label.Children.Add(arrow);
            label.Children.Add(title);
        }
        else
        {
            label.Children.Add(title);
            label.Children.Add(arrow);
        }

        var button = new Button
        {
            Content = label,
            Style = (Style)Application.Current.Resources["HakariSubtleButton"],
            Padding = new Thickness(4),
            Margin = new Thickness(-4, 0, 8, 0),
            HorizontalAlignment = isNumber ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
        };
        return (button, arrow);
    }

    internal static void ShowArrow(IReadOnlyList<FontIcon> arrows, int column, bool descending)
    {
        for (var index = 0; index < arrows.Count; index++)
        {
            arrows[index].Visibility =
                index == column ? Visibility.Visible : Visibility.Collapsed;
        }

        arrows[column].Glyph = descending ? DownGlyph : UpGlyph;
    }

    private static void Sort(
        Panel table,
        IReadOnlyList<IReadOnlyList<IComparable?>> keys,
        int column,
        bool descending)
    {
        var rows = table.Children.Skip(1).OfType<Grid>().ToList();
        var ordered = rows.OrderBy(row => keys[(int)row.Tag][column], Comparer<IComparable?>.Create(
            (left, right) => left is null ? -1 : right is null ? 1 : left.CompareTo(right)));
        var sorted = (descending ? ordered.Reverse() : ordered).ToList();
        var before = rows.ToDictionary(row => row, row => (double)row.ActualOffset.Y);
        for (var index = 0; index < sorted.Count; index++)
        {
            table.Children.Remove(sorted[index]);
            table.Children.Insert(index + 1, sorted[index]);
        }

        if (Motion.SurfaceMotion.Current() == Core.Settings.AnimationSetting.Off)
        {
            return;
        }

        // Each row starts where it was and glides to its new place.
        table.UpdateLayout();
        foreach (var row in sorted)
        {
            var moved = before[row] - row.ActualOffset.Y;
            if (Math.Abs(moved) < 1)
            {
                continue;
            }

            var offset = new Microsoft.UI.Xaml.Media.TranslateTransform { Y = moved };
            row.RenderTransform = offset;
            Motion.SurfaceMotion.Settle(offset, "Y", 0);
        }
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
