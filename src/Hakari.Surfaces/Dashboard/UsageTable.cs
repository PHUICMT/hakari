using System.Globalization;
using Hakari.Core.Localization;
using Hakari.Core.Presentation;
using Hakari.Core.Querying;
using Hakari.Surfaces.Flyout;
using Hakari.Surfaces.Motion;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Hakari.Surfaces.Dashboard;

/// <summary>
/// Rows of name, responses, tokens and cost with a bar for each row's share. Number columns
/// have fixed widths, so the rows of folded groups still line up with each other.
/// </summary>
internal static class UsageTable
{
    private const double HeaderSize = 11.5;
    private const double RowSize = 12.5;
    private const double DetailSize = 11;
    private const double ResponsesWidth = 84;
    private const double TokensWidth = 72;
    private const double CostWidth = 190;
    private const double ShareBarWidth = 80;
    private const double ShareBarHeight = 4;
    private const double ChevronSize = 10;
    private const double FoldedAngle = -90;
    private const double ChildIndent = 22;
    private const string AnglePath = "Angle";
    private const string RowTag = "usage-row";
    private const string ShareTag = "usage-share";
    private const double CompactWidth = 420;
    private const double FullWidth = 560;
    private const double NarrowCostWidth = 96;
    private static readonly Thickness CellPadding = new(0, 8, 12, 8);

    public static UIElement Create(
        IReadOnlyList<UsageSummary> rows,
        decimal totalCost,
        string currency,
        string nameHeader,
        Func<UsageSummary, (string Title, string? Detail)> nameOf)
    {
        if (rows.Count == 0)
        {
            return EmptyText();
        }

        var table = new StackPanel();
        table.Children.Add(HeaderRow(nameHeader));
        foreach (var row in rows)
        {
            table.Children.Add(Row(row, totalCost, currency, NameCell(nameOf(row)), divider: true));
        }

        return Responsive(table);
    }

    /// <summary>
    /// Rows gathered under their project, each project a header with its own totals that
    /// folds its rows away. The most expensive project comes first; all start folded.
    /// </summary>
    public static UIElement CreateGrouped(
        IReadOnlyList<UsageSummary> rows,
        decimal totalCost,
        string currency,
        string nameHeader,
        Func<UsageSummary, (string Title, string? Detail)> nameOf,
        Func<UsageSummary, string> groupOf,
        Func<string, (string Title, string? Detail)> groupNameOf)
    {
        if (rows.Count == 0)
        {
            return EmptyText();
        }

        var table = new StackPanel();
        table.Children.Add(HeaderRow(nameHeader));
        var groups = rows.GroupBy(groupOf)
            .Select(group => (Key: group.Key, Rows: group.ToList(), Total: Sum(group.Key, group)))
            .OrderByDescending(group => group.Total.Cost);
        foreach (var group in groups)
        {
            var section = new GroupSection(group.Rows, group.Total, groupNameOf(group.Key));
            table.Children.Add(section.Build(totalCost, currency, nameOf));
        }

        return Responsive(table);
    }

    private sealed record GroupSection(
        List<UsageSummary> Rows,
        UsageSummary Total,
        (string Title, string? Detail) Name)
    {
        public StackPanel Build(
            decimal totalCost,
            string currency,
            Func<UsageSummary, (string Title, string? Detail)> nameOf)
        {
            var body = new StackPanel { Visibility = Visibility.Collapsed };
            foreach (var row in Rows)
            {
                var name = NameCell(nameOf(row));
                name.Margin = new Thickness(ChildIndent, 0, 0, 0);
                body.Children.Add(Row(row, totalCost, currency, name, divider: true));
            }

            var chevron = Chevron();
            var title = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            title.Children.Add(chevron);
            title.Children.Add(NameCell((Name.Title, Texts.Format(
                "dashboard.groupDetail",
                Rows.Count,
                Name.Detail ?? string.Empty))));

            var header = new Button
            {
                Content = Row(Total, totalCost, currency, title, divider: false),
                Style = (Style)Application.Current.Resources["HakariCardHeaderButton"],
                Padding = new Thickness(0),
            };
            header.Click += (_, _) => Toggle(body, chevron);

            var section = new StackPanel();
            section.Children.Add(Divider());
            section.Children.Add(header);
            section.Children.Add(body);
            return section;
        }

        private static void Toggle(StackPanel body, FontIcon chevron)
        {
            var folding = body.Visibility == Visibility.Visible;
            CardFold.Run(body, folding, fitWindow: () => { });
            if (chevron.RenderTransform is RotateTransform turn)
            {
                SurfaceMotion.Settle(turn, AnglePath, folding ? FoldedAngle : 0);
            }
        }

        private static FontIcon Chevron() => new()
        {
            Glyph = "",
            FontSize = ChevronSize,
            FontFamily = (FontFamily)Application.Current.Resources["HakariIconFont"],
            Foreground = DashboardCard.Brush("HakariInkMutedBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5),
            RenderTransform = new RotateTransform { Angle = FoldedAngle },
        };
    }

    /// <summary>
    /// In a narrow card the tokens column goes first, then responses and the share bars, so
    /// the name and the cost always have room.
    /// </summary>
    private static StackPanel Responsive(StackPanel table)
    {
        WidthSteps.Watch(table, [CompactWidth, FullWidth], level =>
        {
            foreach (var element in Descendants(table))
            {
                if (element is Grid { Tag: RowTag } row)
                {
                    var responses = level >= 1 ? ResponsesWidth : 0;
                    row.ColumnDefinitions[1].Width = new GridLength(responses);
                    row.ColumnDefinitions[2].Width = new GridLength(level >= 2 ? TokensWidth : 0);
                    row.ColumnDefinitions[3].Width = new GridLength(
                        level >= 1 ? CostWidth : NarrowCostWidth);
                }
                else if (element is Grid { Tag: ShareTag } track)
                {
                    track.Visibility = level >= 1 ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        });
        return table;
    }

    /// <summary>Every element below, through panels and button contents, built or not.</summary>
    private static IEnumerable<UIElement> Descendants(UIElement element)
    {
        var children = element switch
        {
            Panel panel => panel.Children.ToList(),
            ContentControl { Content: UIElement content } => [content],
            Border { Child: { } child } => [child],
            _ => [],
        };
        foreach (var child in children)
        {
            yield return child;
            foreach (var nested in Descendants(child))
            {
                yield return nested;
            }
        }
    }

    private static UsageSummary Sum(string key, IEnumerable<UsageSummary> rows) =>
        rows.Aggregate(UsageSummary.Empty with { Key = key }, (sum, row) => sum.Merge(row));

    private static Grid HeaderRow(string nameHeader) => Columns(
        Header(nameHeader, HorizontalAlignment.Left),
        Header(Texts.Get("dashboard.column.responses"), HorizontalAlignment.Right),
        Header(Texts.Get("dashboard.column.tokens"), HorizontalAlignment.Right),
        Header(Texts.Get("dashboard.column.cost"), HorizontalAlignment.Right));

    private static StackPanel Row(
        UsageSummary row,
        decimal totalCost,
        string currency,
        FrameworkElement name,
        bool divider)
    {
        var share = totalCost <= 0 ? 0 : (double)(row.Cost / totalCost);
        var line = new StackPanel();
        if (divider)
        {
            line.Children.Add(Divider());
        }

        line.Children.Add(Columns(
            name,
            Number(row.Messages.ToString("N0", CultureInfo.InvariantCulture)),
            Number(TokenText.Format(row.Tokens.TotalInput + row.Tokens.Output)),
            CostCell(MoneyText.Format(row.Cost, currency), share)));
        return line;
    }

    private static Grid Columns(params FrameworkElement[] cells)
    {
        var grid = new Grid { Tag = RowTag };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ResponsesWidth) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(TokensWidth) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(CostWidth) });
        for (var column = 0; column < cells.Length; column++)
        {
            Grid.SetColumn(cells[column], column);
            grid.Children.Add(cells[column]);
        }

        return grid;
    }

    private static Border Divider() => new()
    {
        Height = 1,
        Background = DashboardCard.Brush("HakariLineBrush"),
    };

    private static TextBlock EmptyText() => new()
    {
        Text = Texts.Get("dashboard.empty"),
        Foreground = DashboardCard.Brush("HakariInkFaintBrush"),
    };

    private static TextBlock Header(string text, HorizontalAlignment alignment) => new()
    {
        Text = text,
        FontSize = HeaderSize,
        Foreground = DashboardCard.Brush("HakariInkFaintBrush"),
        Padding = CellPadding,
        HorizontalAlignment = alignment,
    };

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

    private static StackPanel CostCell(string cost, double share)
    {
        var cell = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Padding = CellPadding,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var track = new Grid
        {
            Tag = ShareTag,
            Width = ShareBarWidth,
            VerticalAlignment = VerticalAlignment.Center,
        };
        track.Children.Add(new Border
        {
            Height = ShareBarHeight,
            CornerRadius = new CornerRadius(ShareBarHeight / 2),
            Background = DashboardCard.Brush("HakariLineBrush"),
        });
        track.Children.Add(new Border
        {
            Height = ShareBarHeight,
            Width = ShareBarWidth * Math.Clamp(share, 0, 1),
            HorizontalAlignment = HorizontalAlignment.Left,
            CornerRadius = new CornerRadius(ShareBarHeight / 2),
            Background = DashboardCard.Brush("HakariAccentBrush"),
        });
        cell.Children.Add(track);
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
