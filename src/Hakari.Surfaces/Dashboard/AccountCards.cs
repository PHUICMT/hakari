using Hakari.Core.Localization;
using Hakari.Surfaces.Flyout;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Hakari.Surfaces.Dashboard;

/// <summary>
/// The cards of the Accounts page: a strip with the totals across accounts, one card per
/// account, and a strip for the usage that came before Hakari.
/// </summary>
internal static class AccountCards
{
    private const double NameSize = 14;
    private const double DetailSize = 12;
    private const double LimitNameSize = 12.5;
    private const double LimitValueSize = 15;
    private const double ResetSize = 11.5;
    private const double MeterHeight = 6;
    private const double CardSpacing = 12;
    private const double FigureSize = 14;
    private const double BigFigureSize = 20;
    private const double FigureGap = 28;
    private const double StripWideFrom = 700;
    private static readonly Thickness CardPadding = new(16, 14, 16, 16);

    /// <summary>
    /// Name and badge on top, then the limits, and the spending pinned to the bottom, so cards
    /// side by side line up whatever number of limits each one has.
    /// </summary>
    public static Border Account(AccountCardData data, string periodCaption)
    {
        var body = new Grid { RowSpacing = CardSpacing };
        var badge = data.BadgeText.Length == 0
            ? null
            : DashboardBadge.Create(data.BadgeText, data.BadgeTone);
        AddRow(body, Top(data.Name, data.Detail, badge), GridLength.Auto);
        foreach (var limit in data.Limits)
        {
            AddRow(body, Limit(limit), GridLength.Auto);
        }

        AddRow(body, new Border(), new GridLength(1, GridUnitType.Star));
        AddRow(body, Figure(periodCaption, data.PeriodCost, FigureSize), GridLength.Auto);
        return Frame(body);
    }

    /// <summary>The totals across every account, as one strip above them.</summary>
    public static Border Summary(AccountsData data, string periodCaption, int sources)
    {
        var title = Top(
            Texts.Get("dashboard.allAccounts"),
            Texts.Format("dashboard.accounts.summary", data.Accounts.Count, sources),
            null);
        return Frame(Strip(
            title,
            [
                Block(Texts.Get("dashboard.period.all"), data.AllTime, BigFigureSize),
                Block(periodCaption, data.Period, BigFigureSize),
                Block(Texts.Get("dashboard.period.today"), data.Today, BigFigureSize),
            ]));
    }

    /// <summary>Usage from before Hakari ran, with why it belongs to no account.</summary>
    public static Border Earlier(AccountCardData data, string periodCaption)
    {
        var figures = new List<FrameworkElement>
        {
            Block(periodCaption, data.PeriodCost, FigureSize),
        };
        if (data.AllTime is { } allTime && allTime != data.PeriodCost)
        {
            figures.Add(Block(Texts.Get("dashboard.period.all"), allTime, FigureSize));
        }

        var body = new StackPanel { Spacing = CardSpacing };
        body.Children.Add(Strip(Top(data.Name, data.Detail, null), figures));
        body.Children.Add(Text(
            Texts.Get("dashboard.accounts.note"),
            DetailSize,
            "HakariInkFaintBrush",
            wrap: true));
        return Frame(body);
    }

    /// <summary>A title at the left, figures at the right, or under it when narrow.</summary>
    private static Grid Strip(FrameworkElement title, IReadOnlyList<FrameworkElement> figures)
    {
        var strip = new Grid { ColumnSpacing = FigureGap, RowSpacing = CardSpacing };
        strip.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        strip.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var column = 0; column <= figures.Count; column++)
        {
            strip.ColumnDefinitions.Add(new ColumnDefinition());
        }

        strip.Children.Add(title);
        foreach (var figure in figures)
        {
            strip.Children.Add(figure);
        }

        WidthSteps.Watch(strip, [StripWideFrom], level =>
        {
            var wide = level >= 1;
            Grid.SetColumnSpan(title, wide ? 1 : figures.Count + 1);
            for (var index = 0; index < figures.Count; index++)
            {
                Grid.SetRow(figures[index], wide ? 0 : 1);
                Grid.SetColumn(figures[index], wide ? index + 1 : index);
                strip.ColumnDefinitions[index + 1].Width = wide
                    ? GridLength.Auto
                    : new GridLength(1, GridUnitType.Star);
            }
        });
        return strip;
    }

    private static void AddRow(Grid grid, FrameworkElement element, GridLength height)
    {
        grid.RowDefinitions.Add(new RowDefinition { Height = height });
        Grid.SetRow(element, grid.RowDefinitions.Count - 1);
        grid.Children.Add(element);
    }

    private static Grid Top(string name, string detail, FrameworkElement? badge)
    {
        var top = new Grid { ColumnSpacing = 8 };
        top.ColumnDefinitions.Add(new ColumnDefinition());
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var text = new StackPanel();
        text.Children.Add(Text(name, NameSize, "HakariInkBrush", bold: true));
        text.Children.Add(Text(detail, DetailSize, "HakariInkFaintBrush"));
        top.Children.Add(text);
        if (badge is not null)
        {
            Grid.SetColumn(badge, 1);
            top.Children.Add(badge);
        }

        return top;
    }

    /// <summary>A limit with a meter; one that is not in use is a single quiet line.</summary>
    private static StackPanel Limit(LimitRow limit)
    {
        var line = new Grid();
        line.ColumnDefinitions.Add(new ColumnDefinition());
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        line.Children.Add(Text(limit.Name, LimitNameSize, "HakariInkMutedBrush"));
        var idle = limit.Fraction <= 0;
        var value = Text(
            limit.Value,
            idle ? LimitNameSize : LimitValueSize,
            idle ? "HakariInkFaintBrush" : "HakariInkBrush",
            bold: !idle);
        if (!idle)
        {
            value.Foreground = FlyoutBrushes.ForValue(limit.Tone);
        }

        value.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(value, 1);
        line.Children.Add(value);

        var block = new StackPanel { Spacing = 6 };
        block.Children.Add(line);
        if (!idle)
        {
            block.Children.Add(Meter(limit));
            block.Children.Add(Text(limit.ResetText, ResetSize, "HakariInkFaintBrush"));
        }

        return block;
    }

    /// <summary>A track with its fill: as long as the share used, in the limit's tone.</summary>
    private static Grid Meter(LimitRow limit)
    {
        var meter = new Grid { Height = MeterHeight };
        var track = new Border
        {
            Background = DashboardCard.Brush("HakariLineBrush"),
            CornerRadius = new CornerRadius(MeterHeight / 2),
        };
        Grid.SetColumnSpan(track, 2);
        meter.Children.Add(track);
        meter.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(Math.Max(limit.Fraction, 0), GridUnitType.Star),
        });
        meter.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(Math.Max(1 - limit.Fraction, 0), GridUnitType.Star),
        });
        var fill = new Border
        {
            Background = FlyoutBrushes.ForTone(limit.Tone),
            CornerRadius = new CornerRadius(MeterHeight / 2),
        };
        Grid.SetColumn(fill, 0);
        meter.Children.Add(fill);
        return meter;
    }

    /// <summary>A label with its value on the same line, the value at the right.</summary>
    private static Grid Figure(string label, string value, double size)
    {
        var line = new Grid();
        line.ColumnDefinitions.Add(new ColumnDefinition());
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        line.Children.Add(Text(label, DetailSize, "HakariInkMutedBrush"));
        var figure = Text(value, size, "HakariInkBrush", bold: true);
        figure.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(figure, 1);
        line.Children.Add(figure);
        return line;
    }

    /// <summary>A label over a value, for the figures of a strip.</summary>
    private static StackPanel Block(string label, string value, double size)
    {
        var block = new StackPanel { Spacing = 2 };
        block.Children.Add(Text(label, DetailSize, "HakariInkMutedBrush"));
        block.Children.Add(Text(value, size, "HakariInkBrush", bold: true));
        return block;
    }

    private static TextBlock Text(
        string text,
        double size,
        string brushKey,
        bool bold = false,
        bool wrap = false) => new()
    {
        Text = text,
        FontSize = size,
        Foreground = DashboardCard.Brush(brushKey),
        FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
        TextTrimming = wrap ? TextTrimming.None : TextTrimming.CharacterEllipsis,
        TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
    };

    private static Border Frame(UIElement body) => new()
    {
        Style = (Style)Application.Current.Resources["HakariCard"],
        Padding = CardPadding,
        Child = body,
        VerticalAlignment = VerticalAlignment.Stretch,
    };
}
