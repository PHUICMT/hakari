using Hakari.Core.Settings;
using Hakari.Surfaces.Motion;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Hakari.Surfaces.Flyout;

/// <summary>
/// Folds or opens a card's body by easing its height, and has the window follow in the
/// same frame, so the window never shows a gap or cuts content (which read as a blink).
/// </summary>
internal sealed class CardFold
{
    private const double EaseExponent = 3;

    private readonly FrameworkElement body;
    private readonly bool folding;
    private readonly Action fitWindow;
    private readonly double fullHeight;
    private readonly TimeSpan duration;
    private readonly DateTimeOffset startedAt = DateTimeOffset.UtcNow;

    private CardFold(FrameworkElement body, bool folding, Action fitWindow, TimeSpan duration)
    {
        this.body = body;
        this.folding = folding;
        this.fitWindow = fitWindow;
        this.duration = duration;
        fullHeight = MeasureFullHeight(body);
    }

    public static void Run(FrameworkElement body, bool folding, Action fitWindow)
    {
        var motion = SurfaceMotion.Current();
        var duration = motion switch
        {
            AnimationSetting.Full => SurfaceMotion.Normal,
            AnimationSetting.Reduced => SurfaceMotion.Fast,
            _ => TimeSpan.Zero,
        };
        var fold = new CardFold(body, folding, fitWindow, duration);
        if (duration == TimeSpan.Zero)
        {
            fold.Finish();
            return;
        }

        body.Visibility = Visibility.Visible;
        fold.Apply(0);
        CompositionTarget.Rendering += fold.OnRendering;
    }

    private static double MeasureFullHeight(FrameworkElement body)
    {
        body.MaxHeight = double.PositiveInfinity;
        body.Visibility = Visibility.Visible;
        body.Measure(new Windows.Foundation.Size(WidthFor(body), double.PositiveInfinity));
        return body.DesiredSize.Height;
    }

    /// <summary>
    /// A folded body has no width of its own yet; measured at an endless width its text
    /// would fit on one line and the opening would jump at the end. The nearest laid-out
    /// parent's width stands in.
    /// </summary>
    private static double WidthFor(FrameworkElement body)
    {
        DependencyObject? current = body;
        while (current is FrameworkElement element)
        {
            if (element.ActualWidth > 0)
            {
                return element.ActualWidth - element.Margin.Left - element.Margin.Right;
            }

            current = VisualTreeHelper.GetParent(element);
        }

        return double.PositiveInfinity;
    }

    private void OnRendering(object? sender, object args)
    {
        var progress = Math.Min(1, (DateTimeOffset.UtcNow - startedAt) / duration);
        if (progress >= 1)
        {
            CompositionTarget.Rendering -= OnRendering;
            Finish();
            return;
        }

        Apply(1 - Math.Pow(1 - progress, EaseExponent));
    }

    /// <summary>Opened share 0 to 1 of the body, and the window fitted to it.</summary>
    private void Apply(double eased)
    {
        var opened = folding ? 1 - eased : eased;
        body.MaxHeight = fullHeight * opened;
        body.Opacity = opened;
        fitWindow();
    }

    private void Finish()
    {
        body.MaxHeight = double.PositiveInfinity;
        body.Opacity = 1;
        body.Visibility = folding ? Visibility.Collapsed : Visibility.Visible;
        fitWindow();
    }
}
