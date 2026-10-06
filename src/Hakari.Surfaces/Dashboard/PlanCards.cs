using System.Globalization;
using Hakari.Core.Localization;
using Hakari.Core.Presentation;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Hakari.Surfaces.Dashboard;

/// <summary>
/// One account's card on the Plan value page: its plan and price, a verdict, a bar for each
/// month's usage at per-token prices with a tick where the plan's price falls, and when the
/// plan paid for itself. Each card scales its bars to itself, so a small account stays legible.
/// </summary>
internal static class PlanCards
{
    private const double MonthColumn = 44;
    private const double FigureColumn = 132;
    private const double TrackHeight = 18;
    private const double TrackRadius = 3;
    private const double TickWidth = 2;
    private const double TextSize = 12.5;
    private const double NoteSize = 12;
    private const double Spacing = 10;
    private const double WorthIt = 1;
    private const double CloseToIt = 0.5;
    private const double WholeNumberFrom = 10;
    private const string DateFormat = "MMM d";
    private static readonly Thickness CardPadding = new(16, 14, 16, 16);

    public static Border Account(PlanAccount account, string currency, PlanValueData data)
    {
        var body = new StackPanel { Spacing = Spacing };
        var badge = Verdict(account, data) is { } verdict
            ? DashboardBadge.Create(Texts.Get(verdict.TextKey), verdict.Tone)
            : null;
        body.Children.Add(AccountCards.Top(account.Name, Detail(account, currency), badge));

        var shown = account.Months.Where((month, index) => index == 0 || month.Cost > 0).ToList();
        var scale = shown.Max(month => Math.Max(month.Cost, account.Price ?? 0));
        foreach (var month in shown)
        {
            body.Children.Add(Row(month, account.Price, scale, currency));
        }

        body.Children.Add(Text(BreakEvenText(account), NoteSize, "HakariInkMutedBrush"));
        return new Border
        {
            Style = (Style)Application.Current.Resources["HakariCard"],
            Padding = CardPadding,
            Child = body,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
    }

    private static string Detail(PlanAccount account, string currency) =>
        account.Kind == PlanCardKind.Unassigned
            ? Texts.Get("dashboard.planValue.unassignedDetail")
            : account.Price is { } price
            ? Texts.Format(
                "dashboard.planValue.detail",
                account.Plan,
                MoneyText.Format(price, currency))
            : account.Plan;

    /// <summary>Judged on the larger of this month's pace and last month's total.</summary>
    private static (string TextKey, BadgeTone Tone)? Verdict(
        PlanAccount account,
        PlanValueData data)
    {
        if (account.Kind == PlanCardKind.Unassigned || account.Price is not > 0)
        {
            return null;
        }

        var pace = account.ThisMonth.Cost / data.Today.Day * data.DaysInMonth;
        var last = account.Months.Count > 1 ? account.Months[1].Cost : 0;
        var multiple = (double)(Math.Max(pace, last) / account.Price.Value);
        return multiple switch
        {
            >= WorthIt => ("dashboard.planValue.worth", BadgeTone.Ok),
            >= CloseToIt => ("dashboard.planValue.close", BadgeTone.Warn),
            _ => ("dashboard.planValue.notPaying", BadgeTone.Neutral),
        };
    }

    private static string BreakEvenText(PlanAccount account)
    {
        if (account.Kind == PlanCardKind.Unassigned)
        {
            return Texts.Get("dashboard.planValue.unassigned");
        }

        if (account.Price is null)
        {
            return Texts.Get("dashboard.planValue.noPrice");
        }

        return account.BreakEven switch
        {
            { ReachedOn: { } day } => Texts.Format(
                "dashboard.planValue.reached",
                day.ToString(DateFormat, Texts.Culture)),
            { ProjectedOn: { } day } => Texts.Format(
                "dashboard.planValue.onPace",
                day.ToString(DateFormat, Texts.Culture)),
            null => Texts.Get("dashboard.planValue.noUsage"),
            _ => Texts.Get("dashboard.planValue.wontReach"),
        };
    }

    private static Grid Row(PlanMonth month, decimal? price, decimal scale, string currency)
    {
        var line = new Grid { ColumnSpacing = 12 };
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(MonthColumn) });
        line.ColumnDefinitions.Add(new ColumnDefinition());
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(FigureColumn) });
        line.Children.Add(Text(month.Label, TextSize, "HakariInkBrush"));
        var track = Track(month.Cost, price, scale);
        Grid.SetColumn(track, 1);
        line.Children.Add(track);
        var figures = Figures(month.Cost, price, currency);
        Grid.SetColumn(figures, 2);
        line.Children.Add(figures);
        return line;
    }

    /// <summary>The bar for the cost, and over it a tick at the plan's price.</summary>
    private static Grid Track(decimal cost, decimal? price, decimal scale)
    {
        var track = new Grid
        {
            Height = TrackHeight,
            Background = DashboardCard.Brush("HakariGroundBrush"),
            CornerRadius = new CornerRadius(TrackRadius),
        };
        track.Children.Add(Portion(
            scale <= 0 ? 0 : (double)(cost / scale),
            new Border
            {
                Background = DashboardCard.Brush("HakariChart2Brush"),
                CornerRadius = new CornerRadius(TrackRadius),
            }));
        if (price is { } monthly && scale > 0)
        {
            track.Children.Add(Portion(
                (double)(monthly / scale),
                new Border
                {
                    BorderBrush = DashboardCard.Brush("HakariInkBrush"),
                    BorderThickness = new Thickness(0, 0, TickWidth, 0),
                }));
        }

        return track;
    }

    /// <summary>Puts an element in the first part of a track, as long as the fraction.</summary>
    private static Grid Portion(double fraction, UIElement element)
    {
        var share = Math.Clamp(fraction, 0, 1);
        var portion = new Grid();
        portion.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(share, GridUnitType.Star),
        });
        portion.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(1 - share, GridUnitType.Star),
        });
        portion.Children.Add(element);
        return portion;
    }

    private static StackPanel Figures(decimal cost, decimal? price, string currency)
    {
        var figures = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        if (price is > 0)
        {
            var text = Text(Multiple((double)(cost / price.Value)), TextSize, "HakariInkBrush");
            text.FontWeight = FontWeights.SemiBold;
            figures.Children.Add(text);
        }

        var money = MoneyText.Format(cost, currency);
        figures.Children.Add(Text(money, TextSize, "HakariInkMutedBrush"));
        return figures;
    }

    public static string Multiple(double multiple) =>
        (multiple >= WholeNumberFrom
            ? multiple.ToString("0", CultureInfo.InvariantCulture)
            : multiple.ToString("0.0", CultureInfo.InvariantCulture)) + "×";

    private static TextBlock Text(string text, double size, string brushKey) => new()
    {
        Text = text,
        FontSize = size,
        Foreground = DashboardCard.Brush(brushKey),
        TextWrapping = TextWrapping.Wrap,
        VerticalAlignment = VerticalAlignment.Center,
    };
}
