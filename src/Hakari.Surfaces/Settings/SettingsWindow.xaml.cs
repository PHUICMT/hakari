using Hakari.Core.Localization;
using Hakari.Core.Settings;
using Hakari.Surfaces.Flyout;
using Hakari.Surfaces.Motion;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Graphics;

namespace Hakari.Surfaces.Settings;

/// <summary>
/// Windows 11-style settings: grouped cards, one row per setting. Every change is saved at
/// once; Hakari.exe watches the file and applies it, so there is no Save button.
/// </summary>
public sealed partial class SettingsWindow : Window
{
    private const double LogicalWidth = 640;
    private const double LogicalHeight = 760;
    private const double MinimumLogicalWidth = 560;
    private const double MinimumLogicalHeight = 420;
    private const double ScreenMargin = 48;
    private const double DefaultDpi = 96;
    private const double RiseDistance = 16;

    private static readonly TimeSpan SectionStagger = TimeSpan.FromMilliseconds(45);

    private readonly SettingsStore store = SettingsStore.Default;

    /// <summary>True while controls are set from the file, so that is not saved back.</summary>
    private bool filling;

    public SettingsWindow()
    {
        InitializeComponent();
        Title = Texts.Get("settings.windowTitle");
        ConfigureChrome();
        BuildCurrencyChoices();
        Fill();
        MakeSectionsFoldable();
        AddEscapeToClose();
    }

    public event EventHandler? LanguageChanged;

    private IntPtr WindowHandle => WinRT.Interop.WindowNative.GetWindowHandle(this);

    /// <summary>Shows the window, or brings it forward when it is already open.</summary>
    public void Present()
    {
        if (!AppWindow.IsVisible)
        {
            PlaceOnPrimaryDisplay();
            PrepareEntrance();
            Activate();

            // Without this the first choice gets keyboard focus and its focus frame.
            Scroller.Focus(FocusState.Programmatic);
            PlayEntrance();
        }
        else
        {
            Activate();
        }

        NativeFocus.BringToFront(WindowHandle);
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
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = true;
        }
    }

    private void PlaceOnPrimaryDisplay()
    {
        var display = DisplayArea.Primary;
        var scale = NativeDpi.ForDisplay(display) / DefaultDpi;
        var workArea = display.WorkArea;
        var maximumHeight = workArea.Height - (int)(ScreenMargin * scale);
        var width = (int)Math.Ceiling(LogicalWidth * scale);
        var height = Math.Min((int)Math.Ceiling(LogicalHeight * scale), maximumHeight);
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

    private void AddEscapeToClose()
    {
        var escape = new KeyboardAccelerator { Key = Windows.System.VirtualKey.Escape };
        escape.Invoked += (_, args) =>
        {
            args.Handled = true;
            Close();
        };
        Root.KeyboardAccelerators.Add(escape);

        // Otherwise WinUI shows "Esc" as a tooltip on whatever the pointer rests on.
        Root.KeyboardAcceleratorPlacementMode = KeyboardAcceleratorPlacementMode.Hidden;
    }

    /// <summary>Hidden before the first frame, so nothing flashes before it rises in.</summary>
    private void PrepareEntrance()
    {
        var motion = SurfaceMotion.Current();
        foreach (var section in Sections.Children.OfType<UIElement>())
        {
            section.Opacity = motion == AnimationSetting.Off ? 1 : 0;
            section.RenderTransform = new TranslateTransform();
        }
    }

    /// <summary>Sections rise in one after another; Reduced keeps only the fade.</summary>
    private void PlayEntrance()
    {
        var motion = SurfaceMotion.Current();
        if (motion == AnimationSetting.Off)
        {
            return;
        }

        var storyboard = new Storyboard();
        var sections = Sections.Children.OfType<UIElement>().ToList();
        for (var index = 0; index < sections.Count; index++)
        {
            AddSectionEntrance(storyboard, sections[index], index, motion);
        }

        storyboard.Begin();
    }

    private static void AddSectionEntrance(
        Storyboard storyboard,
        UIElement section,
        int index,
        AnimationSetting motion)
    {
        if (motion != AnimationSetting.Full)
        {
            storyboard.Children.Add(SurfaceMotion.Animate(
                section, "Opacity", 0, 1, SurfaceMotion.ReducedFade));
            return;
        }

        var delay = SectionStagger * index;
        var duration = SurfaceMotion.Entrance;
        storyboard.Children.Add(SurfaceMotion.Animate(
            section, "Opacity", 0, 1, duration, delay));
        storyboard.Children.Add(SurfaceMotion.Animate(
            section.RenderTransform, "Y", RiseDistance, 0, duration, delay));
    }

    private void Save(Func<HakariSettings, HakariSettings> change)
    {
        if (!filling)
        {
            store.Update(change);
        }
    }
}
