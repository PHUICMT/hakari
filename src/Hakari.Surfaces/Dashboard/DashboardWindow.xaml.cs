using Hakari.Core.Localization;
using Hakari.Core.Querying;
using Hakari.Core.Settings;
using Hakari.Surfaces.Controls;
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
    /// <summary>Wide enough for Settings to preview three accounts side by side.</summary>
    private const double LogicalWidth = 1340;
    private const double LogicalHeight = 860;
    private const double MinimumLogicalWidth = 640;
    private const double MinimumLogicalHeight = 520;
    private const double ScreenMargin = 48;
    private const double DefaultDpi = 96;
    private const double IconSize = 16;
    private const double IconColumn = 22;
    private const double ItemSpacing = 10;
    private const double PageRise = 10;
    private const double NavigationWidth = 220;
    private const double CompactNavigationWidth = 60;
    private const double FullNavigationWidth = 1040;
    private const string NavGroup = "DashboardPages";
    private static readonly Thickness DividerMargin = new(8, 8, 8, 8);

    private static readonly NavEntry[] Pages =
    [
        new(DashboardPage.Overview, DesignIcons.Home, "dashboard.overview"),
        new(DashboardPage.Sessions, DesignIcons.List, "dashboard.sessions"),
        new(DashboardPage.Projects, DesignIcons.Folder, "dashboard.projects"),
        new(DashboardPage.Branches, DesignIcons.Branch, "dashboard.branches"),
        new(DashboardPage.Workflows, DesignIcons.Flow, "dashboard.workflows"),
        new(DashboardPage.Accounts, DesignIcons.User, "dashboard.accounts", SetApart: true),
        new(DashboardPage.Charts, DesignIcons.Chart, "dashboard.charts"),
        new(DashboardPage.PlanValue, DesignIcons.Coin, "dashboard.planValue"),
        new(DashboardPage.Settings, DesignIcons.Gear, "dashboard.settings", SetApart: true),
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
        var settings = SettingsStore.Default.Load();
        menuFolded = settings.DashboardMenuFolded;
        DashboardFilter.Current = RememberedFilter(settings.Dashboard);
        DashboardFilter.Changed += RememberFilter;
        Closed += (_, _) =>
        {
            DashboardFilter.Changed -= RememberFilter;
            RememberSize();
        };
        BuildNavigation();
        WidthSteps.Watch(Root, [FullNavigationWidth], level =>
        {
            narrow = level == 0;
            SetNavigationCompact(menuFolded || narrow, animate: IsShown);
        });
    }

    private bool IsShown => AppWindow.IsVisible;

    /// <summary>Folded by the user's button, or by a window too narrow for the labels.</summary>
    private bool menuFolded;
    private bool narrow;
    private bool compactNow;
    private DateTimeOffset widthTweenStart;
    private (double From, double To) widthTween;

    /// <summary>
    /// Only the icons stay when compact. The menu's width eases between the two and the
    /// labels fade, so the page slides over instead of jumping. Off changes it at once.
    /// </summary>
    private void SetNavigationCompact(bool compact, bool animate)
    {
        if (compact == compactNow && NavigationColumn.Width.Value > 0)
        {
            return;
        }

        compactNow = compact;
        var labels = navItems.Values
            .Select(item => item.Content)
            .OfType<Grid>()
            .Where(row => row.Children.Count == 2)
            .Select(row => row.Children[1])
            .ToList();
        var target = compact ? CompactNavigationWidth : NavigationWidth;
        if (!animate || SurfaceMotion.Current() == AnimationSetting.Off)
        {
            NavigationColumn.Width = new GridLength(target);
            labels.ForEach(label =>
            {
                label.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
                label.Opacity = 1;
            });
            return;
        }

        foreach (var label in labels)
        {
            label.Visibility = Visibility.Visible;
            if (!compact)
            {
                label.Opacity = 0;
            }

            SurfaceMotion.Settle(label, "Opacity", compact ? 0 : 1);
        }

        widthTween = (NavigationColumn.Width.Value, target);
        widthTweenStart = DateTimeOffset.UtcNow;
        CompositionTarget.Rendering -= OnWidthFrame;
        CompositionTarget.Rendering += OnWidthFrame;
    }

    private void OnWidthFrame(object? sender, object args)
    {
        var elapsed = DateTimeOffset.UtcNow - widthTweenStart;
        var progress = Math.Min(1, elapsed / SurfaceMotion.Normal);
        var eased = 1 - Math.Pow(1 - progress, WidthEase);
        var (from, to) = widthTween;
        NavigationColumn.Width = new GridLength(from + (to - from) * eased);
        if (progress < 1)
        {
            return;
        }

        CompositionTarget.Rendering -= OnWidthFrame;
        if (compactNow)
        {
            foreach (var item in navItems.Values)
            {
                if (item.Content is Grid { Children.Count: 2 } row)
                {
                    row.Children[1].Visibility = Visibility.Collapsed;
                }
            }
        }
    }

    private const double WidthEase = 3;

    /// <summary>The menu button folds the menu to its icons or opens it, remembered.</summary>
    private Button MenuButton()
    {
        var button = new Button
        {
            Style = (Style)Application.Current.Resources["HakariSubtleButton"],
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 0, 0, 4),
            Content = new FontIcon
            {
                Glyph = MenuGlyph,
                FontSize = IconSize,
                FontFamily = (FontFamily)Application.Current.Resources["HakariIconFont"],
            },
        };
        ToolTipService.SetToolTip(button, Texts.Get("dashboard.menu.fold"));
        button.Click += (_, _) =>
        {
            menuFolded = !menuFolded;
            SettingsStore.Default.Update(current => current with
            {
                DashboardMenuFolded = menuFolded,
            });
            SetNavigationCompact(menuFolded || narrow, animate: true);
        };
        return button;
    }

    private const string MenuGlyph = "\uE700";

    public event EventHandler? LanguageChanged;

    private IntPtr WindowHandle => WinRT.Interop.WindowNative.GetWindowHandle(this);

    /// <summary>The page the dashboard was left on, else the overview.</summary>
    public static DashboardPage RememberedPage() =>
        Enum.TryParse<DashboardPage>(SettingsStore.Default.Load().Dashboard.Page, out var page)
            ? page
            : DashboardPage.Overview;

    /// <summary>The filter the dashboard was left with, for its first page.</summary>
    private static DashboardFilter RememberedFilter(DashboardMemory memory) => new(
        Enum.TryParse<DashboardPeriod>(memory.Period, out var period)
            ? period
            : DashboardPeriod.ThirtyDays,
        memory.AccountId,
        memory.SourceId,
        memory.Model);

    private static void Remember(Func<DashboardMemory, DashboardMemory> change) =>
        SettingsStore.Default.Update(current => current with
        {
            Dashboard = change(current.Dashboard),
        });

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
        Navigation.Children.Add(MenuButton());
        foreach (var (page, icon, textKey, setApart) in Pages)
        {
            if (setApart)
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
                Content = NavContent(icon, Texts.Get(textKey)),
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

    private static Grid NavContent(IReadOnlyList<IconShape> icon, string label)
    {
        var row = new Grid { ColumnSpacing = ItemSpacing };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(IconColumn) });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.Children.Add(new DesignIcon(icon, IconSize)
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
        });
        var text = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(text, 1);
        row.Children.Add(text);
        return row;
    }

    private static void RememberFilter(object? sender, DashboardFilter filter) =>
        Remember(memory => memory with
        {
            Period = filter.Period.ToString(),
            AccountId = filter.AccountId,
            SourceId = filter.SourceId,
            Model = filter.Model,
        });

    private void Show(DashboardPage page, bool animate)
    {
        Remember(memory => memory with { Page = page.ToString() });
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
            return existing;
        }

        FrameworkElement created = page switch
        {
            DashboardPage.Settings => CreateSettings(),
            DashboardPage.Sessions => new SessionsPage(),
            DashboardPage.Projects => new ProjectsPage(),
            DashboardPage.Branches => new BranchesPage(),
            DashboardPage.Workflows => new WorkflowsPage(),
            DashboardPage.Accounts => new AccountsPage(),
            DashboardPage.Charts => new ChartsPage(),
            DashboardPage.PlanValue => new PlanValuePage(),
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

    /// <summary>In layout units, so it comes back the same on a screen of another scale.</summary>
    private void RememberSize()
    {
        var scale = NativeDpi.ForDisplay(DisplayArea.Primary) / DefaultDpi;
        var size = AppWindow.Size;
        Remember(memory => memory with
        {
            Width = size.Width / scale,
            Height = size.Height / scale,
        });
    }

    private void PlaceOnPrimaryDisplay()
    {
        var display = DisplayArea.Primary;
        var scale = NativeDpi.ForDisplay(display) / DefaultDpi;
        var workArea = display.WorkArea;
        var margin = (int)(ScreenMargin * scale);
        var memory = SettingsStore.Default.Load().Dashboard;
        var logicalWidth = memory.Width >= MinimumLogicalWidth ? memory.Width : LogicalWidth;
        var logicalHeight = memory.Height >= MinimumLogicalHeight
            ? memory.Height
            : LogicalHeight;
        var width = Math.Min((int)Math.Ceiling(logicalWidth * scale), workArea.Width - margin);
        var height = Math.Min((int)Math.Ceiling(logicalHeight * scale), workArea.Height - margin);
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
