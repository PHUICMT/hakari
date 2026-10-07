using Hakari.Core.Settings;
using Hakari.Surfaces.Motion;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Graphics;

namespace Hakari.Surfaces.Flyout;

/// <summary>
/// Quick Settings-style popup above the widget. Opens with a rise and fade, closes on Esc,
/// on clicking elsewhere, or on a second click on the widget.
/// </summary>
public sealed partial class FlyoutWindow : Window
{
    private const double LogicalWidth = 360;
    private const double DefaultDpi = 96;
    private const double RiseDistance = 12;

    /// <summary>
    /// Clicking the widget while open first takes focus away (closing it) and then sends a
    /// request to open; a request this soon after closing is that same click.
    /// </summary>
    private static readonly TimeSpan ReopenGuard = TimeSpan.FromMilliseconds(400);

    private static readonly TimeSpan FullEntrance = SurfaceMotion.Entrance;
    private static readonly TimeSpan ReducedEntrance = SurfaceMotion.ReducedFade;
    private static readonly TimeSpan MeterFill = TimeSpan.FromMilliseconds(480);

    private DateTimeOffset hiddenAt = DateTimeOffset.MinValue;
    private (int AnchorX, int AnchorY, double Scale)? pendingFit;
    private (int AnchorX, int AnchorY, double Scale)? lastAnchor;
    private double availableHeight = double.PositiveInfinity;
    private const double ScreenRoom = 32;
    private const double MinimumAccountsHeight = 160;

    private const int StatColumns = 2;
    private const double SparkWidth = 120;
    private const double SparkHeight = 22;
    private const double SparkInset = 2;
    private const double FoldedChevronAngle = -90;
    private const string AnglePath = "Angle";
    private const string OpacityPath = "Opacity";
    private int fitAttempts;

    private const int FitTolerance = 1;
    private const int MaximumFitAttempts = 3;

    public FlyoutWindow()
    {
        InitializeComponent();
        ConfigureChrome();
        Activated += OnActivated;
    }

    public event EventHandler? Hidden;

    public event EventHandler? SettingsRequested;

    /// <summary>
    /// A notice's main button: prices open Settings, "turn on" allows limits, a newer
    /// version opens its download page.
    /// </summary>
    private void OnNoticeActionClicked(object sender, RoutedEventArgs args)
    {
        if ((sender as FrameworkElement)?.DataContext is not FlyoutNotice notice)
        {
            return;
        }

        if (notice.Action == NoticeAction.TurnOnLimits)
        {
            AnswerLimits(notice, on: true);
            return;
        }

        if (notice.Action == NoticeAction.OpenUpdate)
        {
            HideFlyout();
            var page = Hakari.Core.Updates.UpdateCheck.Load()?.Url
                ?? Hakari.Core.Updates.UpdateCheck.ReleasesPage;
            if (Uri.TryCreate(page, UriKind.Absolute, out var address))
            {
                _ = Windows.System.Launcher.LaunchUriAsync(address);
            }

            return;
        }

        HideFlyout();
        SettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>"Use estimate": the account's sign-in is left alone.</summary>
    private void OnNoticeSecondActionClicked(object sender, RoutedEventArgs args)
    {
        if ((sender as FrameworkElement)?.DataContext is FlyoutNotice notice)
        {
            AnswerLimits(notice, on: false);
        }
    }

    /// <summary>Saves the answer; Hakari.exe reads limits at once when they were allowed.</summary>
    private void AnswerLimits(FlyoutNotice notice, bool on)
    {
        SettingsStore.Default.Update(current => current.WithLimitsChoice(notice.AccountId, on));
        if (NoticeList.ItemsSource is IReadOnlyList<FlyoutNotice> notices)
        {
            NoticeList.ItemsSource = notices.Where(other => other != notice).ToList();
            FitWindowNow();
        }
    }

    public event EventHandler? DashboardRequested;

    public bool IsShowing { get; private set; }

    public void Toggle(int anchorX, int anchorY)
    {
        if (IsShowing)
        {
            HideFlyout();
            return;
        }

        if (DateTimeOffset.UtcNow - hiddenAt < ReopenGuard)
        {
            return;
        }

        Show(anchorX, anchorY);
    }

    private void ConfigureChrome()
    {
        SystemBackdrop = new DesktopAcrylicBackdrop();
        WindowIcon.ApplyTo(AppWindow);

        // Without this, the space of the hidden title bar stays reserved as an empty band.
        ExtendsContentIntoTitleBar = true;
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Collapsed;
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: false);
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsAlwaysOnTop = true;
        }

        AppWindow.IsShownInSwitchers = false;
    }

    private void Show(int anchorX, int anchorY)
    {
        var snapshot = FlyoutDataLoader.Load();
        Fill(snapshot);
        var scale = ScaleAt(anchorX, anchorY);
        availableHeight = AvailableHeight(anchorX, anchorY, scale);
        FoldUntilFits(snapshot);
        LimitAccountsToScreen(measured: true);
        var size = MeasureSize(scale);

        // Size the client area, not the outer frame, so the border never eats content.
        ResizeToClient(size);
        var outer = AppWindow.Size;
        var placement = FlyoutPlacement.Above(anchorX, anchorY, outer, scale, AnchorSide.Center);
        AppWindow.Move(new PointInt32(placement.X, placement.Y));
        IsShowing = true;
        lastAnchor = (anchorX, anchorY, scale);
        pendingFit = (anchorX, anchorY, scale);
        Root.LayoutUpdated -= FitAfterLayout;
        Root.LayoutUpdated += FitAfterLayout;
        Activate();
        NativeFocus.BringToFront(WinRT.Interop.WindowNative.GetWindowHandle(this));
    }

    /// <summary>
    /// The size measured before showing differs from the real layout (item containers are
    /// created on first layout), so the window is fitted to the laid-out content while still
    /// invisible, and only then rises in.
    /// </summary>
    private void FitAfterLayout(object? sender, object args)
    {
        var contentHeight = Body.ActualHeight + Footer.ActualHeight;
        if (pendingFit is not var (anchorX, anchorY, scale) || contentHeight <= 0)
        {
            return;
        }

        // The real layout can come out taller than measured; the accounts give way first.
        var tooTall = contentHeight > availableHeight + FitTolerance;
        if (tooTall && LimitAccountsToScreen(measured: false))
        {
            return;
        }

        // Correct by the difference between the space XAML got and what the content uses;
        // this also absorbs any area the hidden title bar still reserves.
        var targetHeight = (int)Math.Ceiling(contentHeight * scale);
        var spareRows = AppWindow.ClientSize.Height - targetHeight;
        if (Math.Abs(spareRows) > FitTolerance && fitAttempts < MaximumFitAttempts)
        {
            fitAttempts++;
            ResizeToClient(new SizeInt32(AppWindow.ClientSize.Width, targetHeight));
            var placement = FlyoutPlacement.Above(
                anchorX, anchorY, AppWindow.Size, scale, AnchorSide.Center);
            AppWindow.Move(new PointInt32(placement.X, placement.Y));
            return;
        }

        Root.LayoutUpdated -= FitAfterLayout;
        pendingFit = null;
        fitAttempts = 0;
        PlayEntrance();
    }

    private void HideFlyout()
    {
        IsShowing = false;
        hiddenAt = DateTimeOffset.UtcNow;
        Root.Opacity = 0;
        AppWindow.Hide();
        Hidden?.Invoke(this, EventArgs.Empty);
    }

    private void Fill(FlyoutSnapshot snapshot)
    {
        AccountScroller.MaxHeight = double.PositiveInfinity;
        AccountText.Text = snapshot.AccountSummary;
        UpdatedText.Text = snapshot.UpdatedText;
        AccountList.ItemsSource = snapshot.Accounts;
        FillStats(snapshot.Stats);
        BurnText.Text = snapshot.BurnRate;
        BurnSpark.Points = SparkPoints(snapshot.HourlyBurn);
        SourceList.ItemsSource = snapshot.Sources;
        NoticeList.ItemsSource = snapshot.Notices;
    }

    /// <summary>
    /// AppWindow.ResizeClient still adds room for the hidden title bar (about 30 px), so the
    /// frame thickness is measured from the live window and the outer size set directly.
    /// </summary>
    private void ResizeToClient(SizeInt32 client)
    {
        var frameWidth = AppWindow.Size.Width - AppWindow.ClientSize.Width;
        var frameHeight = AppWindow.Size.Height - AppWindow.ClientSize.Height;
        AppWindow.Resize(new SizeInt32(client.Width + frameWidth, client.Height + frameHeight));
    }

    /// <summary>Scaled to the line's box; a quiet day draws a flat line along the bottom.</summary>
    private static PointCollection SparkPoints(IReadOnlyList<decimal> values)
    {
        var points = new PointCollection();
        var highest = values.Count == 0 ? 0 : (double)values.Max();
        for (var index = 0; index < values.Count; index++)
        {
            var x = SparkWidth * index / Math.Max(1, values.Count - 1);
            var share = highest <= 0 ? 0 : (double)values[index] / highest;
            var y = SparkHeight - SparkInset - share * (SparkHeight - SparkInset * 2);
            points.Add(new Windows.Foundation.Point(x, y));
        }

        return points;
    }

    private void FillStats(IReadOnlyList<StatTile> stats)
    {
        StatGrid.Children.Clear();
        for (var index = 0; index < stats.Count; index++)
        {
            var tile = StatTileView.Create(stats[index]);
            Grid.SetColumn(tile, index % StatColumns);
            Grid.SetRow(tile, index / StatColumns);
            StatGrid.Children.Add(tile);
        }
    }

    /// <summary>
    /// Folds or opens one account's card, remembers it for next time, and fits the window to
    /// the new height without replaying the entrance.
    /// </summary>
    private void OnAccountHeaderClicked(object sender, RoutedEventArgs args)
    {
        if (sender is not Button { Tag: string accountId, Parent: StackPanel card } header
            || card.Children.Count < 2
            || header.Content is not Grid { Children.Count: >= 3 } headerGrid)
        {
            return;
        }

        if (card.Children[1] is not FrameworkElement limits)
        {
            return;
        }

        var folding = !CardFold.IsFoldedOrFolding(limits);
        var summary = headerGrid.Children[1];
        summary.Visibility = Visibility.Visible;
        summary.Opacity = folding ? 0 : 1;
        SurfaceMotion.Settle(summary, OpacityPath, folding ? 1 : 0);
        if (headerGrid.Children[2] is FrameworkElement { RenderTransform: RotateTransform turn })
        {
            SurfaceMotion.Settle(turn, AnglePath, folding ? FoldedChevronAngle : 0);
        }

        if (headerGrid.Children[0] is StackPanel { Children.Count: >= 2 } names
            && names.Children[1] is FrameworkElement details)
        {
            CardFold.Run(details, folding, () => { });
        }

        CardFold.Run(limits, folding, FitWindowNow);
        SettingsStore.Default.Update(current => current with
        {
            CollapsedAccounts = folding
                ? [.. current.CollapsedAccounts.Append(accountId).Distinct()]
                : [.. current.CollapsedAccounts.Where(id => id != accountId)],
        });
    }

    /// <summary>
    /// Lays out now and sets size and place in one call, so the window and its content change
    /// in the same frame. The bottom edge stays on the taskbar; the top moves.
    /// </summary>
    private void FitWindowNow()
    {
        if (lastAnchor is not var (anchorX, anchorY, scale))
        {
            return;
        }

        // Runs every frame of a fold: one layout pass, and a second only when the accounts
        // have to give way to stay on screen. The cap is lifted first, so room that a fold or
        // a dismissed notice gave back goes to the accounts again.
        AccountScroller.MaxHeight = double.PositiveInfinity;
        Root.UpdateLayout();
        if (Body.ActualHeight + Footer.ActualHeight > availableHeight
            && LimitAccountsToScreen(measured: false))
        {
            Root.UpdateLayout();
        }

        var clientHeight = (int)Math.Ceiling((Body.ActualHeight + Footer.ActualHeight) * scale);
        var frameWidth = AppWindow.Size.Width - AppWindow.ClientSize.Width;
        var frameHeight = AppWindow.Size.Height - AppWindow.ClientSize.Height;
        var outer = new SizeInt32(
            AppWindow.ClientSize.Width + frameWidth,
            clientHeight + frameHeight);
        var placement = FlyoutPlacement.Above(anchorX, anchorY, outer, scale, AnchorSide.Center);
        AppWindow.MoveAndResize(new RectInt32(placement.X, placement.Y, outer.Width, outer.Height));
    }

    /// <summary>
    /// The height the flyout may take on its screen, in layout units: the work area less
    /// the gap above the taskbar, the margin at the top and the window's frame.
    /// </summary>
    private static double AvailableHeight(int anchorX, int anchorY, double scale)
    {
        var area = DisplayArea.GetFromPoint(
            new PointInt32(anchorX, anchorY),
            DisplayAreaFallback.Primary).WorkArea;
        return area.Height / scale - ScreenRoom;
    }

    /// <summary>
    /// Too tall for the screen: the least pressing open cards fold, from the bottom up, for
    /// this showing only; the most pressing stays open. What the user folded stays folded.
    /// </summary>
    private void FoldUntilFits(FlyoutSnapshot snapshot)
    {
        var groups = snapshot.Accounts.ToList();
        for (var index = groups.Count - 1;
            index > 0 && MeasuredHeight() > availableHeight;
            index--)
        {
            if (groups[index].IsCollapsed)
            {
                continue;
            }

            groups[index] = groups[index] with { IsCollapsed = true };
            AccountList.ItemsSource = groups.ToList();
        }
    }

    /// <summary>
    /// Still too tall: the accounts get a scroll of their own, never shorter than room for
    /// about one card, so the money and the buttons below stay on screen.
    /// </summary>
    /// <param name="measured">Measure first (before showing), or use the laid-out sizes.</param>
    /// <returns>True when the accounts' height changed.</returns>
    private bool LimitAccountsToScreen(bool measured)
    {
        var total = measured ? MeasuredHeight() : Body.ActualHeight + Footer.ActualHeight;
        var accounts = measured
            ? AccountScroller.DesiredSize.Height
            : AccountScroller.ActualHeight;
        var overflow = total - availableHeight;
        if (overflow <= 0)
        {
            return false;
        }

        var limited = Math.Max(MinimumAccountsHeight, accounts - overflow);
        if (Math.Abs(AccountScroller.MaxHeight - limited) < 1)
        {
            return false;
        }

        AccountScroller.MaxHeight = limited;
        return true;
    }

    private double MeasuredHeight()
    {
        Root.Measure(new Windows.Foundation.Size(LogicalWidth, double.PositiveInfinity));
        return Root.DesiredSize.Height;
    }

    private SizeInt32 MeasureSize(double scale)
    {
        Root.Measure(new Windows.Foundation.Size(LogicalWidth, double.PositiveInfinity));
        var height = Root.DesiredSize.Height;
        var width = (int)Math.Ceiling(LogicalWidth * scale);
        return new SizeInt32(width, (int)Math.Ceiling(height * scale));
    }

    private static double ScaleAt(int anchorX, int anchorY)
    {
        var display = DisplayArea.GetFromPoint(
            new PointInt32(anchorX, anchorY),
            DisplayAreaFallback.Primary);
        var dpi = NativeDpi.ForDisplay(display);
        return dpi / DefaultDpi;
    }

    private void PlayEntrance()
    {
        var motion = SurfaceMotion.Current();
        if (motion == AnimationSetting.Off)
        {
            Root.Opacity = 1;
            RootOffset.Y = 0;
            return;
        }

        var duration = motion == AnimationSetting.Full ? FullEntrance : ReducedEntrance;
        var storyboard = new Storyboard();
        storyboard.Children.Add(Animate(Root, "Opacity", 0, 1, duration));
        if (motion == AnimationSetting.Full)
        {
            storyboard.Children.Add(Animate(RootOffset, "Y", RiseDistance, 0, duration));
            AddMeterFills(storyboard);
        }

        storyboard.Begin();
    }

    /// <summary>Meters grow from empty to their value after the flyout starts to rise.</summary>
    private void AddMeterFills(Storyboard storyboard)
    {
        foreach (var fill in FindMeterFills(AccountList))
        {
            if (fill.RenderTransform is ScaleTransform scale && fill.Tag is double fraction)
            {
                storyboard.Children.Add(Animate(scale, "ScaleX", 0, fraction, MeterFill));
            }
        }
    }

    private static IEnumerable<Border> FindMeterFills(DependencyObject parent)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is Border { Name: "MeterFill" } fill)
            {
                yield return fill;
            }

            foreach (var nested in FindMeterFills(child))
            {
                yield return nested;
            }
        }
    }

    private static DoubleAnimation Animate(
        DependencyObject target,
        string property,
        double from,
        double to,
        TimeSpan duration) =>
        SurfaceMotion.Animate(target, property, from, to, duration);

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated && IsShowing)
        {
            HideFlyout();
        }
    }

    private void OnEscapeInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        HideFlyout();
    }

    private void OnCloseClicked(object sender, RoutedEventArgs args) => HideFlyout();

    private void OnDashboardClicked(object sender, RoutedEventArgs args)
    {
        HideFlyout();
        DashboardRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnSettingsClicked(object sender, RoutedEventArgs args)
    {
        HideFlyout();
        SettingsRequested?.Invoke(this, EventArgs.Empty);
    }
}
