using System.Globalization;
using Hakari.Core.Localization;
using Hakari.Core.Presentation;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Hakari.Surfaces.Dashboard;

/// <summary>
/// Whether a plan pays for itself: each account's usage at per-token prices as a bar, with a
/// tick where the plan's own monthly price falls. A bar well past the tick is a plan worth
/// having; a bar short of it means the plan cost more than paying per token would have.
/// </summary>
internal sealed partial class PlanValuePage : LoadedPage<PlanValueData>
{
    private const double LabelColumn = 170;
    private const double FigureColumn = 120;
    private const double TrackHeight = 18;
    private const double TrackRadius = 3;
    private const double TickWidth = 2;
    private const double TextSize = 12.5;
    private const double RowSpacing = 10;
    private const double WholeNumberFrom = 10;

    public PlanValuePage()
        : base("dashboard.planValue")
    {
    }

    protected override PlanValueData Read(DashboardFilter filter) => PlanValueData.Load(filter);

    protected override UIElement Build(PlanValueData data)
    {
        var body = new StackPanel { Spacing = RowSpacing };
        if (data.Rows.Count == 0)
        {
            body.Children.Add(Text(Texts.Get("dashboard.empty"), "HakariInkFaintBrush"));
        }

        var scale = data.Rows
            .Select(row => Math.Max(row.Cost, row.Price ?? 0))
            .DefaultIfEmpty(1m)
            .Max();
        foreach (var row in data.Rows)
        {
            body.Children.Add(Row(row, scale, data.Currency));
        }

        body.Children.Add(Note());
        return DashboardCard.Create(
            Texts.Get("dashboard.planValue.title"),
            Texts.Get("dashboard.planValue.caption"),
            body);
    }

    private static Grid Row(PlanRow row, decimal scale, string currency)
    {
        var line = new Grid { ColumnSpacing = 12 };
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(LabelColumn) });
        line.ColumnDefinitions.Add(new ColumnDefinition());
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(FigureColumn) });

        var label = Text(row.Label, "HakariInkBrush");
        ToolTipService.SetToolTip(label, row.Label);
        var track = Track(row, scale);
        Grid.SetColumn(track, 1);
        var figures = Figures(row, currency);
        Grid.SetColumn(figures, 2);
        line.Children.Add(label);
        line.Children.Add(track);
        line.Children.Add(figures);
        return line;
    }

    /// <summary>The bar for the cost, and over it a tick at the plan's price.</summary>
    private static Grid Track(PlanRow row, decimal scale)
    {
        var track = new Grid
        {
            Height = TrackHeight,
            Background = DashboardCard.Brush("HakariGroundBrush"),
            CornerRadius = new CornerRadius(TrackRadius),
        };
        track.Children.Add(Portion(
            scale <= 0 ? 0 : (double)(row.Cost / scale),
            new Border
            {
                Background = DashboardCard.Brush("HakariChart2Brush"),
                CornerRadius = new CornerRadius(TrackRadius),
            }));
        if (row.Price is { } price && scale > 0)
        {
            track.Children.Add(Portion(
                (double)(price / scale),
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

    private static StackPanel Figures(PlanRow row, string currency)
    {
        var figures = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        if (row.Multiple is { } multiple)
        {
            var text = Text(Multiple(multiple), "HakariInkBrush");
            text.FontWeight = FontWeights.SemiBold;
            figures.Children.Add(text);
        }

        figures.Children.Add(Text(MoneyText.Format(row.Cost, currency), "HakariInkMutedBrush"));
        return figures;
    }

    private static string Multiple(double multiple) =>
        (multiple >= WholeNumberFrom
            ? multiple.ToString("0", CultureInfo.InvariantCulture)
            : multiple.ToString("0.0", CultureInfo.InvariantCulture)) + "×";

    private static TextBlock Note() => new()
    {
        Text = Texts.Get("dashboard.planValue.note"),
        FontSize = 12,
        Foreground = DashboardCard.Brush("HakariInkFaintBrush"),
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 6, 0, 0),
    };

    private static TextBlock Text(string text, string brushKey) => new()
    {
        Text = text,
        FontSize = TextSize,
        Foreground = DashboardCard.Brush(brushKey),
        TextTrimming = TextTrimming.CharacterEllipsis,
        VerticalAlignment = VerticalAlignment.Center,
    };
}
