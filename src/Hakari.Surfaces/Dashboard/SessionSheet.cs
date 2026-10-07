using System.Globalization;
using Hakari.Core.Localization;
using Hakari.Core.Presentation;
using Hakari.Core.Querying;
using Hakari.Core.Settings;
using Hakari.Surfaces.Motion;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

namespace Hakari.Surfaces.Dashboard;

/// <summary>What one session's sheet shows; read only when the sheet opens.</summary>
internal sealed record SessionDetail(
    IReadOnlyList<(string Label, decimal Cost)> Timeline,
    decimal SubagentCost,
    decimal CacheSaved,
    IReadOnlyList<UsageSummary> Models);

/// <summary>
/// A sheet over the right side of the dashboard for one session: when and where it ran, its
/// cost by hour, how much went to subagents, how well the cache served, the models it used,
/// and its working folder. Prompt text is never read, so it is never shown. A click outside
/// or Escape closes it.
/// </summary>
internal static class SessionSheet
{
    private const double SheetWidth = 460;
    private const double SlideDistance = 40;
    private const double PercentScale = 100;
    private const double BackdropOpacity = 0.35;
    private static readonly Thickness SheetPadding = new(24, 20, 24, 24);

    public static async void Open(FrameworkElement owner, SessionRow row, string currency)
    {
        try
        {
            await OpenAsync(owner, row, currency);
        }
        catch (Exception exception)
        {
            CrashLog.Write(exception, "session sheet");
        }
    }

    private static async Task OpenAsync(FrameworkElement owner, SessionRow row, string currency)
    {
        if (owner.XamlRoot is not { } root)
        {
            return;
        }

        var session = GroupKeys.Split(row.Usage.Key).Item;
        var detail = await Task.Run(() => DashboardData.Read(
            (query, _) => Read(query, session),
            new SessionDetail([], 0, 0, [])));
        Show(root, row, detail, currency);
    }

    private static SessionDetail Read(UsageQuery query, string session)
    {
        var filter = new UsageFilter(SessionId: session);
        return new SessionDetail(
            [
                .. query.Summarize(filter, GroupBy.Hour).OrderBy(hour => hour.Key)
                    .Select(hour => (Label(hour.Key), hour.Cost)),
            ],
            query.Total(filter with { IsSidechain = true }).Cost,
            query.CacheSavings(filter),
            query.Summarize(filter, GroupBy.Model));
    }

    /// <summary>"2026-10-04 09:00" becomes "09:00".</summary>
    private static string Label(string hourKey) =>
        hourKey.Length > 11 ? hourKey[11..] : hourKey;

    private static void Show(XamlRoot root, SessionRow row, SessionDetail detail, string currency)
    {
        var popup = new Popup { XamlRoot = root, IsLightDismissEnabled = false };
        var backdrop = new Border
        {
            Background = new SolidColorBrush(Microsoft.UI.Colors.Black),
            Opacity = BackdropOpacity,
        };
        backdrop.Tapped += (_, _) => popup.IsOpen = false;
        var sheet = new Border
        {
            Width = Math.Min(SheetWidth, root.Size.Width),
            HorizontalAlignment = HorizontalAlignment.Right,
            Background = DashboardCard.Brush("HakariSurfaceBrush"),
            BorderBrush = DashboardCard.Brush("HakariLineBrush"),
            BorderThickness = new Thickness(1, 0, 0, 0),
            Child = new ScrollViewer
            {
                Content = Body(row, detail, currency, () => popup.IsOpen = false),
                Padding = SheetPadding,
            },
        };
        var layer = new Grid { Width = root.Size.Width, Height = root.Size.Height };
        layer.Children.Add(backdrop);
        layer.Children.Add(sheet);
        layer.KeyDown += (_, args) =>
        {
            if (args.Key == Windows.System.VirtualKey.Escape)
            {
                popup.IsOpen = false;
            }
        };
        popup.Child = layer;
        popup.IsOpen = true;
        sheet.Focus(FocusState.Programmatic);
        if (SurfaceMotion.Current() != AnimationSetting.Off)
        {
            var offset = new TranslateTransform { X = SlideDistance };
            sheet.RenderTransform = offset;
            sheet.Opacity = 0;
            SurfaceMotion.Settle(offset, "X", 0);
            SurfaceMotion.Settle(sheet, "Opacity", 1);
        }
    }

    private static StackPanel Body(
        SessionRow row,
        SessionDetail detail,
        string currency,
        Action close)
    {
        var usage = row.Usage;
        var (project, session) = GroupKeys.Split(usage.Key);
        var body = new StackPanel { Spacing = 16 };
        body.Children.Add(Header(row.Title ?? SessionsPage.When(usage), close));
        body.Children.Add(Faint(string.Join(" · ", new[]
        {
            SessionsPage.When(usage),
            row.Source ?? string.Empty,
            session,
        }.Where(part => part.Length > 0))));

        var cost = usage.Cost;
        body.Children.Add(DashboardTiles.Create(
        [
            DashboardTile.Create(Texts.Get("dashboard.column.cost"),
                MoneyText.Format(cost, currency), null),
            DashboardTile.Create(Texts.Get("dashboard.column.duration"),
                SessionsPage.Duration(usage.LastSeen - usage.FirstSeen),
                Texts.Format("dashboard.sheet.responses",
                    usage.Messages.ToString("N0", CultureInfo.InvariantCulture))),
            DashboardTile.Create(Texts.Get("dashboard.sheet.subagents"),
                Share(detail.SubagentCost, cost),
                MoneyText.Format(detail.SubagentCost, currency)),
            DashboardTile.Create(Texts.Get("dashboard.column.hitRate"),
                PercentText.Format(usage.Tokens.CacheHitRate * PercentScale, 0),
                Texts.Format("dashboard.sheet.saved",
                    MoneyText.Format(detail.CacheSaved, currency))),
        ]));
        if (detail.Timeline.Count > 0)
        {
            body.Children.Add(DashboardCard.Create(
                Texts.Get("dashboard.sheet.timeline"),
                null,
                ChartOrTable.Create(
                    CostChart.Create(detail.Timeline, currency),
                    () => ChartOrTable.Costs(detail.Timeline, currency))));
        }

        body.Children.Add(DashboardCard.Create(
            Texts.Get("dashboard.column.model"), null, Models(detail.Models, cost, currency)));
        body.Children.Add(DashboardCard.Create(
            Texts.Get("dashboard.sheet.folder"), null, Faint(project.Length == 0
                ? Texts.Get("dashboard.unknown")
                : project)));
        body.Children.Add(Faint(Texts.Get("dashboard.sheet.privacy")));
        return body;
    }

    private static Grid Header(string title, Action close)
    {
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = DashboardCard.Brush("HakariInkBrush"),
            TextWrapping = TextWrapping.Wrap,
        });
        var button = new Button
        {
            Style = (Style)Application.Current.Resources["HakariSubtleButton"],
            Content = new FontIcon
            {
                Glyph = "",
                FontSize = 12,
                FontFamily = (FontFamily)Application.Current.Resources["HakariIconFont"],
            },
            VerticalAlignment = VerticalAlignment.Top,
        };
        Hakari.Surfaces.Controls.Accessible.Name(button, Texts.Get("flyout.close"));
        button.Click += (_, _) => close();
        Grid.SetColumn(button, 1);
        header.Children.Add(button);
        return header;
    }

    private static StackPanel Models(
        IReadOnlyList<UsageSummary> models,
        decimal total,
        string currency)
    {
        var list = new StackPanel { Spacing = 8 };
        foreach (var model in models)
        {
            var row = new Grid { ColumnSpacing = 10 };
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(new TextBlock
            {
                Text = SessionRows.ShortModel(model.Key),
                FontFamily = (FontFamily)Application.Current.Resources["HakariMonoFont"],
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
            var share = new TextBlock
            {
                Text = Share(model.Cost, total),
                Foreground = DashboardCard.Brush("HakariInkMutedBrush"),
            };
            Grid.SetColumn(share, 1);
            row.Children.Add(share);
            var money = new TextBlock { Text = MoneyText.Format(model.Cost, currency) };
            Grid.SetColumn(money, 2);
            row.Children.Add(money);
            list.Children.Add(row);
        }

        return list;
    }

    private static string Share(decimal part, decimal total) =>
        PercentText.Format(total <= 0 ? 0 : (double)(part / total) * PercentScale, 0);

    private static TextBlock Faint(string text) => new()
    {
        Text = text,
        FontSize = 12,
        Foreground = DashboardCard.Brush("HakariInkFaintBrush"),
        TextWrapping = TextWrapping.Wrap,
    };
}
