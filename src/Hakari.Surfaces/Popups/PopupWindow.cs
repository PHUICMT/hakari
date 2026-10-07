using Hakari.Surfaces.Flyout;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace Hakari.Surfaces.Popups;

/// <summary>
/// A small borderless window above the taskbar, sized to its content: the base of the
/// widget's hover card and menu. It never shows in Alt+Tab or the taskbar.
/// </summary>
public abstract partial class PopupWindow : Window
{
    private const double DefaultDpi = 96;

    private readonly Border frame;

    protected PopupWindow(double logicalWidth)
    {
        LogicalWidth = logicalWidth;
        frame = new Border
        {
            Background = Brush("HakariSurfaceBrush"),
            BorderBrush = Brush("HakariLineBrush"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(ContentPadding),
        };
        Content = frame;
        ConfigureChrome();
    }

    protected double LogicalWidth { get; }

    protected virtual double ContentPadding => 12;

    protected UIElement? Body
    {
        get => frame.Child;
        set => frame.Child = value;
    }

    public bool IsShowing { get; private set; }

    /// <summary>Sizes to the content at the anchor's scale, placed above the widget.</summary>
    protected void ShowAbove(
        int anchorX,
        int anchorY,
        bool activate,
        AnchorSide side = AnchorSide.End)
    {
        var display = DisplayArea.GetFromPoint(
            new PointInt32(anchorX, anchorY),
            DisplayAreaFallback.Primary);
        var scale = NativeDpi.ForDisplay(display) / DefaultDpi;
        frame.Measure(new Windows.Foundation.Size(LogicalWidth, double.PositiveInfinity));
        var client = new SizeInt32(
            (int)Math.Ceiling(LogicalWidth * scale),
            (int)Math.Ceiling(frame.DesiredSize.Height * scale));
        var frameWidth = AppWindow.Size.Width - AppWindow.ClientSize.Width;
        var frameHeight = AppWindow.Size.Height - AppWindow.ClientSize.Height;
        var outer = new SizeInt32(client.Width + frameWidth, client.Height + frameHeight);
        AppWindow.MoveAndResize(FlyoutPlacement.Above(anchorX, anchorY, outer, scale, side));
        IsShowing = true;
        AppWindow.Show(activate);
        if (activate)
        {
            Activate();
            NativeFocus.BringToFront(WinRT.Interop.WindowNative.GetWindowHandle(this));
        }
    }

    public void HidePopup()
    {
        IsShowing = false;
        OnHiding();
        AppWindow.Hide();
    }

    /// <summary>Lets a popup drop work it started for showing.</summary>
    protected virtual void OnHiding()
    {
    }

    protected static Brush Brush(string key) => (Brush)Application.Current.Resources[key];

    private void ConfigureChrome()
    {
        SystemBackdrop = new DesktopAcrylicBackdrop();
        WindowIcon.ApplyTo(AppWindow);
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
}
