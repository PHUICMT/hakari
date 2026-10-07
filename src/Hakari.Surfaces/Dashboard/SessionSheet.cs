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
using Microsoft.UI.Xaml.Media.Animation;

namespace Hakari.Surfaces.Dashboard;

/// <summary>What one session's sheet shows; read only when the sheet opens.</summary>
/// <param name="ByDay">A session over more than a day is charted per day, not per hour.</param>
internal sealed record SessionDetail(
    IReadOnlyList<(string Label, decimal Cost)> Timeline,
    bool ByDay,
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

    /// <summary>The dashboard's title bar row; the sheet starts below it, clear of the
    /// window's own minimize, maximize and close buttons.</summary>
    private const double TitleBarHeight = 48;
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

        var (project, session) = GroupKeys.Split(row.Usage.Key);
        var byDay = row.Usage.LastSeen - row.Usage.FirstSeen > TimeSpan.FromDays(1);
        var period = DashboardFilter.Current.ToUsageFilter(DateTimeOffset.Now);
        var detail = await Task.Run(() => DashboardData.Read(
            (query, _) => Read(query, period, project, session, byDay),
            new SessionDetail([], byDay, 0, 0, [])));
        Show(root, row, detail, currency);
    }

    /// <summary>
    /// The row's own project and session under the dashboard's period, account, source and
    /// model, the same filter the row was read with, so every share is of the row's cost.
    /// </summary>
    private static SessionDetail Read(
        UsageQuery query,
        UsageFilter period,
        string project,
        string session,
        bool byDay)
    {
        var filter = period with { Project = project, SessionId = session };
        return new SessionDetail(
            [
                .. query.Summarize(filter, byDay ? GroupBy.Day : GroupBy.Hour)
                    .OrderBy(step => step.Key)
                    .Select(step => (byDay ? DayLabel(step.Key) : Label(step.Key), step.Cost)),
            ],
            byDay,
            query.Total(filter with { IsSidechain = true }).Cost,
            query.CacheSavings(filter),
            query.Summarize(filter, GroupBy.Model));
    }

    /// <summary>"2026-10-04 09:00" becomes "09:00".</summary>
    private static string Label(string hourKey) =>
        hourKey.Length > 11 ? hourKey[11..] : hourKey;

    /// <summary>"8 Sep – 7 Oct": the days a session spread over, under its time in use.</summary>
    private static string DayRange(UsageSummary usage) =>
        usage.FirstSeen.ToLocalTime().ToString("d MMM", Texts.Culture) + " – "
        + usage.LastSeen.ToLocalTime().ToString("d MMM", Texts.Culture);

    /// <summary>"2026-09-08" becomes "8 Sep" (or "8 ก.ย.").</summary>
    private static string DayLabel(string dayKey) =>
        DateOnly.TryParseExact(dayKey, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var day)
            ? day.ToString("d MMM", Texts.Culture)
            : dayKey;

    private static void Show(XamlRoot root, SessionRow row, SessionDetail detail, string currency)
    {
        var popup = new Popup { XamlRoot = root, IsLightDismissEnabled = false };
        var backdrop = new Border
        {
            Background = new SolidColorBrush(Microsoft.UI.Colors.Black),
            Opacity = BackdropOpacity,
        };
        var offset = new TranslateTransform();
        Border? sheetRef = null;
        var closing = false;

        // Leaves the way it came: slides back out and fades, then the popup goes.
        void Close()
        {
            if (closing)
            {
                return;
            }

            closing = true;
            var motion = SurfaceMotion.Current();
            if (motion == AnimationSetting.Off || sheetRef is null)
            {
                popup.IsOpen = false;
                return;
            }

            // Slides back off the right edge, picking up speed as it goes, as a sheet put away.
            var duration = motion == AnimationSetting.Full
                ? SurfaceMotion.Entrance
                : SurfaceMotion.Normal;
            var easeIn = new CubicEase { EasingMode = EasingMode.EaseIn };
            var leave = new Storyboard();
            foreach (var (target, property, to) in new (DependencyObject, string, double)[]
            {
                (offset, "X", sheetRef.ActualWidth),
                (sheetRef, "Opacity", 0),
                (backdrop, "Opacity", 0),
            })
            {
                var animation = SurfaceMotion.Animate(target, property, null, to, duration);
                animation.EasingFunction = easeIn;
                leave.Children.Add(animation);
            }

            leave.Completed += (_, _) => popup.IsOpen = false;
            leave.Begin();
        }

        backdrop.Tapped += (_, _) => Close();
        var sheet = new Border
        {
            Width = Math.Min(SheetWidth, root.Size.Width),
            HorizontalAlignment = HorizontalAlignment.Right,
            Background = DashboardCard.Brush("HakariSurfaceBrush"),
            BorderBrush = DashboardCard.Brush("HakariLineBrush"),
            BorderThickness = new Thickness(1, 0, 0, 0),
            Child = new ScrollViewer
            {
                Content = Body(row, detail, currency, Close),
                Padding = SheetPadding,
            },
        };
        var layer = new Grid
        {
            Width = root.Size.Width,
            Height = Math.Max(0, root.Size.Height - TitleBarHeight),
        };
        layer.Children.Add(backdrop);
        layer.Children.Add(sheet);
        layer.KeyDown += (_, args) =>
        {
            if (args.Key == Windows.System.VirtualKey.Escape)
            {
                Close();
            }
        };
        sheetRef = sheet;
        sheet.RenderTransform = offset;
        popup.Child = layer;
        popup.VerticalOffset = TitleBarHeight;
        popup.IsOpen = true;
        sheet.Focus(FocusState.Programmatic);
        if (SurfaceMotion.Current() != AnimationSetting.Off)
        {
            offset.X = SlideDistance;
            sheet.Opacity = 0;
            backdrop.Opacity = 0;
            SurfaceMotion.Settle(backdrop, "Opacity", BackdropOpacity);
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
                SessionsPage.Duration(row.Active),
                detail.ByDay
                    ? DayRange(usage)
                    : Texts.Format("dashboard.sheet.responses",
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
                detail.ByDay
                    ? "dashboard.sheet.timelineDays"
                    : "dashboard.sheet.timeline",
                null,
                ChartOrTable.Create(
                    CostChart.Create(detail.Timeline, currency),
                    () => ChartOrTable.Costs(detail.Timeline, currency))));
        }

        body.Children.Add(DashboardCard.Create(
            "dashboard.column.model", null, Models(detail.Models, cost, currency)));
        body.Children.Add(DashboardCard.Create(
            "dashboard.sheet.folder", null, Faint(project.Length == 0
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
