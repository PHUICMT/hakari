using Hakari.Core.Settings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.UI.ViewManagement;

namespace Hakari.Surfaces.Motion;

/// <summary>
/// The animation choice and timing shared by every window, so Off and Reduced behave the
/// same everywhere. Read on demand, which picks up a change made in Settings right away.
/// </summary>
internal static class SurfaceMotion
{
    public static readonly TimeSpan Fast = TimeSpan.FromMilliseconds(120);
    public static readonly TimeSpan Normal = TimeSpan.FromMilliseconds(200);
    public static readonly TimeSpan Entrance = TimeSpan.FromMilliseconds(320);
    public static readonly TimeSpan ReducedFade = TimeSpan.FromMilliseconds(150);

    private const double EaseExponent = 6;

    /// <summary>
    /// Asked for on every animation, so the answer is kept for a moment instead of reading
    /// the settings file each time; a change in Settings still shows within a second.
    /// </summary>
    private static readonly TimeSpan KeepFor = TimeSpan.FromSeconds(1);
    private static readonly UISettings SystemSettings = new();
    private static AnimationSetting? kept;
    private static long keptAt;

    public static AnimationSetting Current()
    {
        var now = Environment.TickCount64;
        if (kept is { } recent && now - keptAt < KeepFor.TotalMilliseconds)
        {
            return recent;
        }

        // Always one of Full, Reduced or Off: callers compare against those.
        var setting = SettingsStore.Default.Load().Animation;
        var resolved = setting != AnimationSetting.FollowWindows
            ? setting
            : SystemSettings.AnimationsEnabled ? AnimationSetting.Full : AnimationSetting.Reduced;
        kept = resolved;
        keptAt = now;
        return resolved;
    }

    public static DoubleAnimation Animate(
        DependencyObject target,
        string property,
        double? from,
        double to,
        TimeSpan duration,
        TimeSpan delay = default)
    {
        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = duration,
            BeginTime = delay,
            EasingFunction = new ExponentialEase
            {
                EasingMode = EasingMode.EaseOut,
                Exponent = EaseExponent,
            },
            EnableDependentAnimation = true,
        };
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, property);
        return animation;
    }

    /// <summary>
    /// Moves a property to <paramref name="to"/>: animated under Full, a short ease under
    /// Reduced (state changes are not decoration), and instantly under Off.
    /// </summary>
    public static void Settle(DependencyObject target, string property, double to)
    {
        var dependencyProperty = PropertyOf(target, property);
        var motion = Current();
        if (motion == AnimationSetting.Off)
        {
            target.SetValue(dependencyProperty, to);
            return;
        }

        var duration = motion == AnimationSetting.Full ? Normal : Fast;
        var storyboard = new Storyboard();
        storyboard.Children.Add(Animate(target, property, from: null, to, duration));

        // A finished storyboard keeps holding its value; hand it back to the property so a
        // later instant change (motion off) is not overridden.
        storyboard.Completed += (_, _) =>
        {
            target.SetValue(dependencyProperty, to);
            storyboard.Stop();
        };
        storyboard.Begin();
    }

    /// <summary>
    /// Brings in something just shown: under Full it fades in while moving the given
    /// distance into place, under Reduced it only fades, under Off it is simply there.
    /// </summary>
    public static void Enter(UIElement element, double fromX = 0, double fromY = 0)
    {
        var motion = Current();
        element.Opacity = 1;
        if (motion == AnimationSetting.Off)
        {
            return;
        }

        var storyboard = new Storyboard();
        var full = motion == AnimationSetting.Full;
        var offset = new Microsoft.UI.Xaml.Media.TranslateTransform();
        if (full)
        {
            element.RenderTransform = offset;
            storyboard.Children.Add(Animate(offset, "X", fromX, 0, Entrance));
            storyboard.Children.Add(Animate(offset, "Y", fromY, 0, Entrance));
        }

        storyboard.Children.Add(
            Animate(element, "Opacity", 0, 1, full ? Entrance : ReducedFade));
        storyboard.Completed += (_, _) =>
        {
            element.Opacity = 1;
            offset.X = 0;
            offset.Y = 0;
            storyboard.Stop();
        };
        storyboard.Begin();
    }

    private const double FlyoutRise = 8;

    /// <summary>
    /// A flyout's content comes in the same way every time it opens, Hakari's motion setting
    /// deciding how, so it moves even where Windows' own popup animation is turned off.
    /// </summary>
    public static void EnterOnOpen(Microsoft.UI.Xaml.Controls.Primitives.FlyoutBase flyout)
    {
        flyout.AreOpenCloseAnimationsEnabled = Current() != AnimationSetting.Off;
        // Hidden while it opens and started once on screen, since a storyboard may not run
        // on an element that is not in the tree yet.
        flyout.Opening += (_, _) =>
        {
            if (flyout is Microsoft.UI.Xaml.Controls.Flyout { Content: { } content }
                && Current() != AnimationSetting.Off)
            {
                content.Opacity = 0;
            }
        };
        flyout.Opened += (_, _) =>
        {
            if (flyout is Microsoft.UI.Xaml.Controls.Flyout { Content: { } content })
            {
                // Comes in from the side of the button: down when below it, up when above.
                var above = flyout.Placement is FlyoutPlacementMode.Top
                    or FlyoutPlacementMode.TopEdgeAlignedLeft
                    or FlyoutPlacementMode.TopEdgeAlignedRight;
                Enter(content, fromY: above ? FlyoutRise : -FlyoutRise);
            }
        };
    }

    private static DependencyProperty PropertyOf(DependencyObject target, string property) =>
        (target, property) switch
        {
            (UIElement, "Opacity") => UIElement.OpacityProperty,
            (Microsoft.UI.Xaml.Media.TranslateTransform, "X") =>
                Microsoft.UI.Xaml.Media.TranslateTransform.XProperty,
            (Microsoft.UI.Xaml.Media.TranslateTransform, "Y") =>
                Microsoft.UI.Xaml.Media.TranslateTransform.YProperty,
            (Microsoft.UI.Xaml.Media.RotateTransform, "Angle") =>
                Microsoft.UI.Xaml.Media.RotateTransform.AngleProperty,
            (Microsoft.UI.Xaml.Media.ScaleTransform, "ScaleX") =>
                Microsoft.UI.Xaml.Media.ScaleTransform.ScaleXProperty,
            (Microsoft.UI.Xaml.Media.ScaleTransform, "ScaleY") =>
                Microsoft.UI.Xaml.Media.ScaleTransform.ScaleYProperty,
            _ => throw new ArgumentException($"Unsupported property {property}", nameof(property)),
        };
}
