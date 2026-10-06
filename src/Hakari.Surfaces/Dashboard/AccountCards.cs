using Hakari.Core.Localization;
using Hakari.Surfaces.Flyout;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Hakari.Surfaces.Dashboard;

/// <summary>The cards of the Accounts page: one per account, and one for all together.</summary>
internal static class AccountCards
{
    private const double NameSize = 14;
    private const double DetailSize = 12;
    private const double LimitNameSize = 12.5;
    private const double LimitValueSize = 15;
    private const double ResetSize = 11.5;
    private const double MeterHeight = 6;
    private const double CardSpacing = 12;
    private const double BigFigureSize = 20;
    private const double FigureSize = 14;
    private static readonly Thickness CardPadding = new(16, 14, 16, 16);

    public static Border Account(AccountCardData data, string periodCaption)
    {
        var body = new StackPanel { Spacing = CardSpacing };
        var badge = data.BadgeText.Length == 0
            ? null
            : DashboardBadge.Create(data.BadgeText, data.BadgeTone);
        body.Children.Add(Top(data.Name, data.Detail, badge));
        foreach (var limit in data.Limits)
        {
            body.Children.Add(Limit(limit));
        }

        body.Children.Add(Figure(periodCaption, data.PeriodCost, FigureSize));
        if (data.AllTime is { } allTime && allTime != data.PeriodCost)
        {
            body.Children.Add(Figure(Texts.Get("dashboard.period.all"), allTime, FigureSize));
        }

        return Frame(body);
    }

    public static Border Total(AccountsData data, string periodCaption, int sources)
    {
        var body = new StackPanel { Spacing = CardSpacing };
        body.Children.Add(Top(
            Texts.Get("dashboard.allAccounts"),
            Texts.Format("dashboard.accounts.summary", data.Accounts.Count, sources),
            null));
        body.Children.Add(Figure(Texts.Get("dashboard.period.all"), data.AllTime, BigFigureSize));
        body.Children.Add(Figure(periodCaption, data.Period, FigureSize));
        body.Children.Add(Figure(Texts.Get("dashboard.period.today"), data.Today, FigureSize));
        return Frame(body);
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

    private static StackPanel Limit(LimitRow limit)
    {
        var line = new Grid();
        line.ColumnDefinitions.Add(new ColumnDefinition());
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        line.Children.Add(Text(limit.Name, LimitNameSize, "HakariInkMutedBrush"));
        var value = Text(limit.Value, LimitValueSize, "HakariInkBrush", bold: true);
        value.Foreground = FlyoutBrushes.ForValue(limit.Tone);
        value.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(value, 1);
        line.Children.Add(value);

        var block = new StackPanel { Spacing = 6 };
        block.Children.Add(line);
        block.Children.Add(Meter(limit));
        block.Children.Add(Text(limit.ResetText, ResetSize, "HakariInkFaintBrush"));
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

    private static TextBlock Text(
        string text,
        double size,
        string brushKey,
        bool bold = false) => new()
    {
        Text = text,
        FontSize = size,
        Foreground = DashboardCard.Brush(brushKey),
        FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
        TextTrimming = TextTrimming.CharacterEllipsis,
    };

    private static Border Frame(UIElement body) => new()
    {
        Style = (Style)Application.Current.Resources["HakariCard"],
        Padding = CardPadding,
        Child = body,
        VerticalAlignment = VerticalAlignment.Top,
    };
}
