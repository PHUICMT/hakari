using Hakari.Core.Settings;
using Microsoft.UI.Xaml;
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

    public static AnimationSetting Current()
    {
        var setting = SettingsStore.Default.Load().Animation;
        if (setting != AnimationSetting.FollowWindows)
        {
            return setting;
        }

        var animationsEnabled = new UISettings().AnimationsEnabled;
        return animationsEnabled ? AnimationSetting.Full : AnimationSetting.Reduced;
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
