using Hakari.Core.Localization;
using Hakari.Core.Querying;
using Hakari.Core.Settings;
using Hakari.Surfaces.Flyout;
using Hakari.Surfaces.Motion;
using Hakari.Surfaces.Settings;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace Hakari.Surfaces.Dashboard;

/// <summary>
/// The dashboard: navigation on the left, one page at a time on the right. Settings is one
/// of its pages, so every "Settings" in Hakari opens here.
/// </summary>
public sealed partial class DashboardWindow : Window
{
    private const double LogicalWidth = 1100;
    private const double LogicalHeight = 780;
    private const double MinimumLogicalWidth = 640;
    private const double MinimumLogicalHeight = 520;
    private const double ScreenMargin = 48;
    private const double DefaultDpi = 96;
    private const double IconSize = 15;
    private const double IconColumn = 22;
    private const double ItemSpacing = 10;
    private const double PageRise = 10;
    private const double NavigationWidth = 220;
    private const double CompactNavigationWidth = 60;
    private const double FullNavigationWidth = 1040;
    private const string NavGroup = "DashboardPages";
    private static readonly Thickness DividerMargin = new(8, 8, 8, 8);

    private static readonly (DashboardPage Page, string Glyph, string TextKey)[] Pages =
    [
        (DashboardPage.Overview, "", "dashboard.overview"),
        (DashboardPage.Sessions, "", "dashboard.sessions"),
        (DashboardPage.Projects, "", "dashboard.projects"),
        (DashboardPage.Branches, "", "dashboard.branches"),
        (DashboardPage.Settings, "", "dashboard.settings"),
    ];

    private readonly Dictionary<DashboardPage, RadioButton> navItems = [];
    private readonly Dictionary<DashboardPage, FrameworkElement> pages = [];
    private DashboardPage current = DashboardPage.Overview;
    private bool choosing;

    public DashboardWindow()
    {
        InitializeComponent();
        Title = Texts.Get("dashboard.windowTitle");
        ConfigureChrome();
        BuildNavigation();
        WidthSteps.Watch(Root, [FullNavigationWidth], level => SetNavigationCompact(level == 0));
    }

    /// <summary>A narrow window keeps only the icons, so the page has the room.</summary>
    private void SetNavigationCompact(bool compact)
    {
        NavigationColumn.Width = new GridLength(compact ? CompactNavigationWidth : NavigationWidth);
        foreach (var item in navItems.Values)
        {
            if (item.Content is Grid { Children.Count: 2 } row)
            {
                row.Children[1].Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            }
        }
    }

    public event EventHandler? LanguageChanged;

    private IntPtr WindowHandle => WinRT.Interop.WindowNative.GetWindowHandle(this);

    /// <summary>Shows the window on a page, or brings it forward and switches page.</summary>
    public void Present(DashboardPage page)
    {
        var firstShow = !AppWindow.IsVisible;
        if (firstShow)
        {
            PlaceOnPrimaryDisplay();
        }

        Activate();
        NativeFocus.BringToFront(WindowHandle);
        Show(page, firstShow);
    }

    private void BuildNavigation()
    {
        foreach (var (page, glyph, textKey) in Pages)
        {
            if (page == DashboardPage.Settings)
            {
                Navigation.Children.Add(new Border
                {
                    Height = 1,
                    Margin = DividerMargin,
                    Background = Brush("HakariLineBrush"),
                });
            }

            var item = new RadioButton
            {
                GroupName = NavGroup,
                Style = (Style)Application.Current.Resources["HakariNavItem"],
                Content = NavContent(glyph, Texts.Get(textKey)),
            };
            ToolTipService.SetToolTip(item, Texts.Get(textKey));
            item.Checked += (_, _) =>
            {
                if (!choosing)
                {
                    Show(page, animate: true);
                }
            };
            navItems[page] = item;
            Navigation.Children.Add(item);
        }
    }

    private static Grid NavContent(string glyph, string label)
    {
        var row = new Grid { ColumnSpacing = ItemSpacing };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(IconColumn) });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.Children.Add(new FontIcon
        {
            Glyph = glyph,
            FontSize = IconSize,
            FontFamily = (FontFamily)Application.Current.Resources["HakariIconFont"],
        });
        var text = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(text, 1);
        row.Children.Add(text);
        return row;
    }

    private void Show(DashboardPage page, bool animate)
    {
        current = page;
        choosing = true;
        navItems[page].IsChecked = true;
        choosing = false;

        var view = PageFor(page);
        PageHost.Content = view;
        if (view is SettingsPage settings)
        {
            settings.PlayEntrance();
        }
        else if (animate)
        {
            RiseIn(view);
        }
    }

    /// <summary>Pages are built on first visit and kept, so going back is instant.</summary>
    private FrameworkElement PageFor(DashboardPage page)
    {
        if (pages.TryGetValue(page, out var existing))
        {
            if (existing is OverviewPage overview)
            {
                overview.Refresh();
            }

            return existing;
        }

        FrameworkElement created = page switch
        {
            DashboardPage.Settings => CreateSettings(),
            DashboardPage.Sessions =>
                new BreakdownPage(GroupBy.ProjectSession, "dashboard.sessions"),
            DashboardPage.Projects => new BreakdownPage(GroupBy.Project, "dashboard.projects"),
            DashboardPage.Branches =>
                new BreakdownPage(GroupBy.ProjectBranch, "dashboard.branches"),
            _ => new OverviewPage(),
        };
        pages[page] = created;
        return created;
    }

    private SettingsPage CreateSettings()
    {
        var settings = new SettingsPage { HostWindowHandle = WindowHandle };
        settings.LanguageChanged += (_, _) => LanguageChanged?.Invoke(this, EventArgs.Empty);
        return settings;
    }

    public DashboardPage CurrentPage => current;

    private static void RiseIn(FrameworkElement view)
    {
        if (SurfaceMotion.Current() == AnimationSetting.Off)
        {
            return;
        }

        var offset = new TranslateTransform { Y = PageRise };
        view.RenderTransform = offset;
        view.Opacity = 0;
        SurfaceMotion.Settle(view, "Opacity", 1);
        SurfaceMotion.Settle(offset, "Y", 0);
    }

    private void ConfigureChrome()
    {
        SystemBackdrop = new MicaBackdrop();
        WindowIcon.ApplyTo(AppWindow);
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
    }

    private void PlaceOnPrimaryDisplay()
    {
        var display = DisplayArea.Primary;
        var scale = NativeDpi.ForDisplay(display) / DefaultDpi;
        var workArea = display.WorkArea;
        var margin = (int)(ScreenMargin * scale);
        var width = Math.Min((int)Math.Ceiling(LogicalWidth * scale), workArea.Width - margin);
        var height = Math.Min((int)Math.Ceiling(LogicalHeight * scale), workArea.Height - margin);
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = (int)Math.Ceiling(MinimumLogicalWidth * scale);
            presenter.PreferredMinimumHeight = (int)Math.Ceiling(MinimumLogicalHeight * scale);
        }

        AppWindow.MoveAndResize(new RectInt32(
            workArea.X + (workArea.Width - width) / 2,
            workArea.Y + (workArea.Height - height) / 2,
            width,
            height));
    }

    private static Brush Brush(string key) => (Brush)Application.Current.Resources[key];
}
