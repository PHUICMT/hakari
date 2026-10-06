using Hakari.Core.Localization;
using Hakari.Core.Settings;
using Hakari.Surfaces.Flyout;
using Hakari.Surfaces.Motion;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace Hakari.Surfaces.Onboarding;

/// <summary>
/// The first run, in three skippable steps: the usage Hakari found, names for the accounts,
/// and a look for the taskbar. Indexing has already started behind it, so the numbers are
/// ready by the end. Closing it at any step counts as done; it never comes back by itself.
/// </summary>
internal sealed partial class OnboardingWindow : Window
{
    private const double LogicalWidth = 560;
    private const double LogicalHeight = 600;
    private const double DefaultDpi = 96;
    private const double StepTitleSize = 22;
    private const double DotSize = 8;
    private const double SlideDistance = 24;
    private const int StepCount = 3;
    private static readonly Thickness PagePadding = new(32, 24, 32, 24);

    private readonly SettingsStore store = SettingsStore.Default;
    private readonly StackPanel dots = new() { Orientation = Orientation.Horizontal, Spacing = 6 };
    private readonly TextBlock stepCount = new();
    private readonly TextBlock stepTitle = new();
    private readonly ContentControl stepBody = new()
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch,
        IsTabStop = false,
    };
    private readonly Button back = new();
    private readonly Button next = new();
    private readonly Button skip = new();
    private readonly OnboardingSteps steps;
    private int step;

    public OnboardingWindow()
    {
        steps = new OnboardingSteps(store);
        Title = "Hakari";
        SystemBackdrop = new MicaBackdrop();
        WindowIcon.ApplyTo(AppWindow);
        ExtendsContentIntoTitleBar = true;
        AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        Content = Page();
        Closed += (_, _) => MarkDone();
        Show(0, forward: true);
        PlaceOnPrimaryDisplay();
    }

    private Grid Page()
    {
        var page = new Grid { Padding = PagePadding, RowSpacing = 12 };
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        page.RowDefinitions.Add(new RowDefinition());
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        stepCount.FontSize = 12;
        stepCount.Foreground = Brush("HakariInkFaintBrush");
        stepTitle.FontSize = StepTitleSize;
        stepTitle.FontWeight = FontWeights.SemiBold;
        stepTitle.Foreground = Brush("HakariInkBrush");
        stepTitle.TextWrapping = TextWrapping.Wrap;
        dots.Margin = new Thickness(0, 16, 0, 0);
        Add(page, dots, 0);
        Add(page, stepCount, 1);
        Add(page, stepTitle, 2);
        Add(page, new ScrollViewer { Content = stepBody, Margin = new Thickness(0, 8, 0, 0) }, 3);
        Add(page, Buttons(), 4);
        return page;
    }

    private Grid Buttons()
    {
        skip.Style = Style("HakariSubtleButton");
        skip.Content = Texts.Get("onboarding.skip");
        skip.Click += (_, _) => Close();
        back.Style = Style("HakariButton");
        back.Content = Texts.Get("onboarding.back");
        back.Click += (_, _) => Show(step - 1, forward: false);
        next.Style = Style("HakariPrimaryButton");
        next.Click += (_, _) => Advance();

        var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        right.Children.Add(back);
        right.Children.Add(next);
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(skip);
        Grid.SetColumn(right, 1);
        row.Children.Add(right);
        return row;
    }

    private void Advance()
    {
        if (step < StepCount - 1)
        {
            Show(step + 1, forward: true);
            return;
        }

        steps.Finish();
        Close();
    }

    /// <summary>The new step slides in from the side it comes from and fades up.</summary>
    private void Show(int index, bool forward)
    {
        step = Math.Clamp(index, 0, StepCount - 1);
        stepCount.Text = Texts.Format("onboarding.stepOf", step + 1, StepCount);
        stepTitle.Text = Texts.Get($"onboarding.step{step + 1}.title");
        stepBody.Content = step switch
        {
            0 => steps.Sources(),
            1 => steps.Accounts(),
            _ => steps.Look(),
        };
        back.Visibility = step == 0 ? Visibility.Collapsed : Visibility.Visible;
        next.Content = Texts.Get(step == StepCount - 1 ? "onboarding.finish" : "onboarding.next");
        skip.Content = Texts.Get(step == StepCount - 1 ? "onboarding.later" : "onboarding.skip");
        PaintDots();
        Slide(forward);
    }

    private void Slide(bool forward)
    {
        if (SurfaceMotion.Current() == AnimationSetting.Off)
        {
            return;
        }

        var offset = new TranslateTransform { X = forward ? SlideDistance : -SlideDistance };
        stepBody.RenderTransform = offset;
        stepBody.Opacity = 0;
        SurfaceMotion.Settle(offset, "X", 0);
        SurfaceMotion.Settle(stepBody, "Opacity", 1);
    }

    private void PaintDots()
    {
        dots.Children.Clear();
        for (var index = 0; index < StepCount; index++)
        {
            dots.Children.Add(new Border
            {
                Width = index == step ? DotSize * 3 : DotSize,
                Height = DotSize,
                CornerRadius = new CornerRadius(DotSize / 2),
                Background = Brush(index <= step ? "HakariAccentBrush" : "HakariLineStrongBrush"),
            });
        }
    }

    private void MarkDone() =>
        store.Update(current => current with { OnboardingDone = true });

    private void PlaceOnPrimaryDisplay()
    {
        var display = DisplayArea.Primary;
        var scale = NativeDpi.ForDisplay(display) / DefaultDpi;
        var area = display.WorkArea;
        var width = Math.Min((int)Math.Ceiling(LogicalWidth * scale), area.Width);
        var height = Math.Min((int)Math.Ceiling(LogicalHeight * scale), area.Height);
        AppWindow.MoveAndResize(new RectInt32(
            area.X + (area.Width - width) / 2,
            area.Y + (area.Height - height) / 2,
            width,
            height));
    }

    private static void Add(Grid grid, FrameworkElement element, int row)
    {
        Grid.SetRow(element, row);
        grid.Children.Add(element);
    }

    private static Style Style(string key) => (Style)Application.Current.Resources[key];

    private static Brush Brush(string key) => (Brush)Application.Current.Resources[key];
}
