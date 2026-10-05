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
        Fill(FlyoutDataLoader.Load());
        var scale = ScaleAt(anchorX, anchorY);
        var size = MeasureSize(scale);

        // Size the client area, not the outer frame, so the border never eats content.
        ResizeToClient(size);
        var outer = AppWindow.Size;
        var placement = FlyoutPlacement.Above(anchorX, anchorY, outer, scale);
        AppWindow.Move(new PointInt32(placement.X, placement.Y));
        IsShowing = true;
        pendingFit = (anchorX, anchorY, scale);
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

        // Correct by the difference between the space XAML got and what the content uses;
        // this also absorbs any area the hidden title bar still reserves.
        var targetHeight = (int)Math.Ceiling(contentHeight * scale);
        var spareRows = AppWindow.ClientSize.Height - targetHeight;
        if (Math.Abs(spareRows) > FitTolerance && fitAttempts < MaximumFitAttempts)
        {
            fitAttempts++;
            ResizeToClient(new SizeInt32(AppWindow.ClientSize.Width, targetHeight));
            var placement = FlyoutPlacement.Above(anchorX, anchorY, AppWindow.Size, scale);
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
        AccountText.Text = snapshot.AccountName;
        UpdatedText.Text = snapshot.UpdatedText;
        LimitList.ItemsSource = snapshot.Limits;
        FillStats(snapshot.Stats);
        BurnText.Text = snapshot.BurnRate;
        SourceList.ItemsSource = snapshot.Sources;
        NoticeText.Text = snapshot.Notice ?? string.Empty;
        NoticeCard.Visibility = snapshot.Notice is null ? Visibility.Collapsed : Visibility.Visible;
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

    private void FillStats(IReadOnlyList<StatTile> stats)
    {
        StatGrid.Children.Clear();
        for (var column = 0; column < stats.Count; column++)
        {
            var tile = StatTileView.Create(stats[column]);
            Grid.SetColumn(tile, column);
            StatGrid.Children.Add(tile);
        }
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
        foreach (var fill in FindMeterFills(LimitList))
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

    private void OnSettingsClicked(object sender, RoutedEventArgs args)
    {
        HideFlyout();
        SettingsRequested?.Invoke(this, EventArgs.Empty);
    }
}
