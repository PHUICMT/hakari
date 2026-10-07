using System.Collections.ObjectModel;
using System.Globalization;
using Hakari.Core.Localization;
using Hakari.Core.Presentation;
using Hakari.Surfaces.Motion;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Hakari.Surfaces.Dashboard;

/// <summary>
/// A header row over a virtualized list of rows. Opening a group inserts its rows into the
/// list, where they fade in; folding removes them. In a narrow card the tokens column goes
/// first, then responses and the share bars, so the name and cost always have room.
/// </summary>
internal sealed partial class TableView : StackPanel
{
    private const double HeaderSize = 11.5;
    private const double RowSize = 12.5;
    private const double DetailSize = 11;
    private const double ResponsesWidth = 84;
    private const double TokensWidth = 72;
    private const double CostWidth = 190;
    private const double NarrowCostWidth = 96;
    private const double ShareBarWidth = 80;
    private const double ShareBarHeight = 4;
    private const double ChevronSize = 10;
    private const double ChevronColumn = 22;
    private const double FoldedAngle = -90;
    private const double CompactWidth = 420;
    private const double FullWidth = 560;
    private const string AnglePath = "Angle";
    private const string OpacityPath = "Opacity";
    private static readonly Thickness CellPadding = new(0, 8, 12, 8);
    private static readonly Thickness DividerLine = new(0, 1, 0, 0);

    private readonly ObservableCollection<TableItem> items;
    private readonly decimal totalCost;
    private readonly string currency;
    private readonly ItemsRepeater list;
    private readonly Grid header;
    private readonly HashSet<TableItem> openGroups = new(ReferenceEqualityComparer.Instance);
    private int level = 2;

    public TableView(
        ObservableCollection<TableItem> items,
        decimal totalCost,
        string currency,
        string nameHeader)
    {
        this.items = items;
        this.totalCost = totalCost;
        this.currency = currency;
        header = Columns(
            SortHeader(nameHeader, isNumber: false, column: 0),
            SortHeader(Texts.Get("dashboard.column.responses"), isNumber: true, column: 1),
            SortHeader(Texts.Get("dashboard.column.tokens"), isNumber: true, column: 2),
            SortHeader(Texts.Get("dashboard.column.cost"), isNumber: true, column: 3));
        list = new ItemsRepeater
        {
            ItemsSource = items,
            ItemTemplate = new TableRowFactory(BuildRow),
        };
        Children.Add(header);
        Children.Add(list);
        WidthSteps.Watch(this, [CompactWidth, FullWidth], SetLevel);
    }

    /// <summary>
    /// Rebuilds the visible rows for the new width; the list is unchanged. The first size
    /// usually matches the rows already built, and a rebuild waits until layout is done.
    /// </summary>
    private void SetLevel(int newLevel)
    {
        if (newLevel == level)
        {
            return;
        }

        level = newLevel;
        DispatcherQueue.TryEnqueue(() =>
        {
            if (level != newLevel)
            {
                return;
            }

            ApplyColumns(header);
            list.ItemsSource = null;
            list.ItemsSource = items;
        });
    }

    private UIElement BuildRow(TableItem item)
    {
        FrameworkElement row = item.IsGroup ? GroupRow(item) : FigureRow(item);
        if (item.FadeIn && SurfaceMotion.Current() != Core.Settings.AnimationSetting.Off)
        {
            row.Opacity = 0;
            row.Loaded += (_, _) => SurfaceMotion.Settle(row, OpacityPath, 1);
        }

        return row;
    }

    private Grid FigureRow(TableItem item)
    {
        var name = NameCell(item.Name);
        if (item.IsIndented)
        {
            name.Margin = new Thickness(ChevronColumn, 0, 0, 0);
        }

        var row = Figures(item, name);
        row.BorderThickness = DividerLine;
        row.BorderBrush = DashboardCard.Brush("HakariLineBrush");
        return row;
    }

    private Button GroupRow(TableItem item)
    {
        var chevron = new FontIcon
        {
            Glyph = "",
            FontSize = ChevronSize,
            Width = ChevronColumn,
            HorizontalAlignment = HorizontalAlignment.Left,
            FontFamily = (FontFamily)Application.Current.Resources["HakariIconFont"],
            Foreground = DashboardCard.Brush("HakariInkMutedBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5),
            RenderTransform = new RotateTransform
            {
                Angle = openGroups.Contains(item) ? 0 : FoldedAngle,
            },
        };
        var name = new StackPanel { Orientation = Orientation.Horizontal };
        name.Children.Add(chevron);
        name.Children.Add(NameCell(item.Name));

        var row = Figures(item, name);
        var button = new Button
        {
            Content = row,
            Style = (Style)Application.Current.Resources["HakariCardHeaderButton"],
            Padding = new Thickness(0),
            BorderThickness = DividerLine,
            BorderBrush = DashboardCard.Brush("HakariLineBrush"),
        };
        button.Click += (_, _) => Toggle(item, chevron);
        return button;
    }

    /// <summary>
    /// Opening inserts the group's rows right after it and folding takes them out. Which
    /// groups are open is kept here, not in the items, so the header element stays and its
    /// chevron turns instead of being rebuilt.
    /// </summary>
    private void Toggle(TableItem group, FontIcon chevron)
    {
        var index = items.IndexOf(group);
        if (index < 0 || group.Children is not { } children)
        {
            return;
        }

        var opening = openGroups.Add(group);
        if (!opening)
        {
            openGroups.Remove(group);
        }

        if (chevron.RenderTransform is RotateTransform turn)
        {
            SurfaceMotion.Settle(turn, AnglePath, opening ? 0 : FoldedAngle);
        }

        for (var child = 0; child < children.Count; child++)
        {
            if (opening)
            {
                items.Insert(index + 1 + child, children[child] with { FadeIn = true });
            }
            else
            {
                items.RemoveAt(index + 1);
            }
        }
    }
    private Grid Figures(TableItem item, FrameworkElement name)
    {
        var summary = item.Summary;
        var share = totalCost <= 0 ? 0 : (double)(summary.Cost / totalCost);
        var row = Columns(
            name,
            Number(summary.Messages.ToString("N0", CultureInfo.InvariantCulture)),
            Number(TokenText.Format(summary.Tokens.TotalInput + summary.Tokens.Output)),
            CostCell(MoneyText.Format(summary.Cost, currency), share));
        return row;
    }

    private Grid Columns(params FrameworkElement[] cells)
    {
        var grid = new Grid();
        for (var column = 0; column < cells.Length; column++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            Grid.SetColumn(cells[column], column);
            grid.Children.Add(cells[column]);
        }

        ApplyColumns(grid);
        return grid;
    }

    private void ApplyColumns(Grid grid)
    {
        grid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
        grid.ColumnDefinitions[1].Width = new GridLength(level >= 1 ? ResponsesWidth : 0);
        grid.ColumnDefinitions[2].Width = new GridLength(level >= 2 ? TokensWidth : 0);
        grid.ColumnDefinitions[3].Width = new GridLength(level >= 1 ? CostWidth : NarrowCostWidth);
    }

    private readonly List<FontIcon> arrows = [];
    private int sortedBy = -1;
    private bool descending;

    /// <summary>A header that sorts by its column; a second click turns the order round.</summary>
    private Button SortHeader(string text, bool isNumber, int column)
    {
        var (button, arrow) = SimpleTable.SortButton(text, isNumber);
        button.Margin = new Thickness(-4, 4, 8, 4);
        arrows.Add(arrow);
        button.Click += (_, _) =>
        {
            descending = sortedBy == column ? !descending : isNumber;
            sortedBy = column;
            SimpleTable.ShowArrow(arrows, column, descending);
            Sort(column);
        };
        return button;
    }

    /// <summary>
    /// Sorts the top rows; open groups fold first, so no group's rows end up under another.
    /// The rows come back fading in, in their new order.
    /// </summary>
    private void Sort(int column)
    {
        IComparable KeyOf(TableItem item) => column switch
        {
            0 => item.Name.Title,
            1 => item.Summary.Messages,
            2 => item.Summary.Tokens.TotalInput + item.Summary.Tokens.Output,
            _ => item.Summary.Cost,
        };

        var top = items.Where(item => !item.IsIndented).ToList();
        var ordered = top.OrderBy(KeyOf).ToList();
        if (descending)
        {
            ordered.Reverse();
        }

        openGroups.Clear();
        items.Clear();
        foreach (var item in ordered)
        {
            items.Add(item with { FadeIn = true });
        }
    }

    /// <summary>The name, with where it belongs underneath when there is a second line.</summary>
    private static StackPanel NameCell((string Title, string? Detail) name)
    {
        var cell = new StackPanel { Padding = CellPadding, Spacing = 1 };
        cell.Children.Add(new TextBlock
        {
            Text = name.Title,
            FontSize = RowSize,
            FontWeight = FontWeights.SemiBold,
            FontFamily = (FontFamily)Application.Current.Resources["HakariMonoFont"],
            Foreground = DashboardCard.Brush("HakariInkBrush"),
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        if (!string.IsNullOrEmpty(name.Detail))
        {
            cell.Children.Add(new TextBlock
            {
                Text = name.Detail,
                FontSize = DetailSize,
                Foreground = DashboardCard.Brush("HakariInkFaintBrush"),
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
        }

        return cell;
    }

    private static TextBlock Number(string text) => new()
    {
        Text = text,
        FontSize = RowSize,
        Foreground = DashboardCard.Brush("HakariInkMutedBrush"),
        Padding = CellPadding,
        HorizontalAlignment = HorizontalAlignment.Right,
        VerticalAlignment = VerticalAlignment.Center,
    };

    /// <summary>The share bar is one track with its fill inside; hidden in a narrow card.</summary>
    private StackPanel CostCell(string cost, double share)
    {
        var cell = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Padding = CellPadding,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        if (level >= 1)
        {
            cell.Children.Add(new Border
            {
                Width = ShareBarWidth,
                Height = ShareBarHeight,
                VerticalAlignment = VerticalAlignment.Center,
                CornerRadius = new CornerRadius(ShareBarHeight / 2),
                Background = DashboardCard.Brush("HakariLineBrush"),
                Child = new Border
                {
                    Width = ShareBarWidth * Math.Clamp(share, 0, 1),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    CornerRadius = new CornerRadius(ShareBarHeight / 2),
                    Background = DashboardCard.Brush("HakariAccentBrush"),
                },
            });
        }

        cell.Children.Add(new TextBlock
        {
            Text = cost,
            FontSize = RowSize,
            FontWeight = FontWeights.SemiBold,
            Foreground = DashboardCard.Brush("HakariInkBrush"),
        });
        return cell;
    }
}
