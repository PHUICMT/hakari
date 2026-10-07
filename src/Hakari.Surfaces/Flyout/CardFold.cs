using Hakari.Core.Settings;
using Hakari.Surfaces.Motion;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Hakari.Surfaces.Flyout;

/// <summary>
/// Folds or opens a card's body by easing its height, and has the window follow in the
/// same frame, so the window never shows a gap or cuts content (which read as a blink).
/// A second fold on the same body takes over from where the first one is, instead of the
/// two fighting over its height.
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
    private readonly double startShare;

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<
        FrameworkElement, CardFold> Running = new();

    private CardFold(
        FrameworkElement body,
        bool folding,
        Action fitWindow,
        TimeSpan duration,
        double startShare)
    {
        this.body = body;
        this.folding = folding;
        this.fitWindow = fitWindow;
        this.duration = duration;
        this.startShare = startShare;
        fullHeight = MeasureFullHeight(body);
    }

    /// <summary>
    /// Whether the body is folded or on its way there; a body still folding is not yet
    /// Collapsed, so its Visibility alone would say "open" to a quick second click.
    /// </summary>
    public static bool IsFoldedOrFolding(FrameworkElement body) =>
        Running.TryGetValue(body, out var fold)
            ? fold.folding
            : body.Visibility == Visibility.Collapsed;

    public static void Run(FrameworkElement body, bool folding, Action fitWindow)
    {
        var motion = SurfaceMotion.Current();
        var duration = motion switch
        {
            AnimationSetting.Full => SurfaceMotion.Normal,
            AnimationSetting.Reduced => SurfaceMotion.Fast,
            _ => TimeSpan.Zero,
        };
        // Where the body is now, as an opened share, so a reversed fold starts from there.
        var startShare = folding ? 1.0 : 0.0;
        if (Running.TryGetValue(body, out var running))
        {
            CompositionTarget.Rendering -= running.OnRendering;
            Running.Remove(body);
            startShare = running.fullHeight > 0
                ? Math.Clamp(body.MaxHeight / running.fullHeight, 0, 1)
                : startShare;
        }

        var fold = new CardFold(body, folding, fitWindow, duration, startShare);
        if (duration == TimeSpan.Zero)
        {
            fold.Finish();
            return;
        }

        body.Visibility = Visibility.Visible;
        fold.Apply(0);
        Running.AddOrUpdate(body, fold);
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
        // ActualWidth already leaves out an element's own margin; only the body's margin
        // has to come off its parent's width.
        var bodyMargin = body.Margin.Left + body.Margin.Right;
        DependencyObject? current = body;
        while (current is FrameworkElement element)
        {
            if (element.ActualWidth > 0)
            {
                return element == body
                    ? element.ActualWidth
                    : Math.Max(0, element.ActualWidth - bodyMargin);
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
            Running.Remove(body);
            Finish();
            return;
        }

        Apply(1 - Math.Pow(1 - progress, EaseExponent));
    }

    /// <summary>Opened share 0 to 1 of the body, and the window fitted to it.</summary>
    private void Apply(double eased)
    {
        var target = folding ? 0.0 : 1.0;
        var opened = startShare + (target - startShare) * eased;
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
