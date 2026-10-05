using Hakari.Core.Settings;
using Hakari.Surfaces.Motion;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace Hakari.Surfaces.Controls;

/// <summary>
/// A row of <see cref="HakariSegment"/> choices with one accent indicator behind them that
/// slides to the selected choice. Under Reduced it fades in at the new place instead, and
/// under Off it simply jumps.
/// </summary>
public sealed partial class HakariSegmented : Grid
{
    private const string OffsetPath = "X";
    private const string WidthPath = "Width";
    private const string OpacityPath = "Opacity";
    private const double IndicatorRadius = 3;
    private const double ChoiceSpacing = 2;
    private const double SnapTolerance = 0.5;

    private readonly Border indicator = new()
    {
        HorizontalAlignment = HorizontalAlignment.Left,
        CornerRadius = new CornerRadius(IndicatorRadius),
        Opacity = 0,
        IsHitTestVisible = false,
    };

    private readonly StackPanel choices = new()
    {
        Orientation = Orientation.Horizontal,
        Spacing = ChoiceSpacing,
    };

    private readonly TranslateTransform indicatorOffset = new();
    private Storyboard? running;

    public HakariSegmented()
    {
        indicator.RenderTransform = indicatorOffset;
        indicator.Background = (Brush)Application.Current.Resources["HakariAccentBrush"];
        Loaded += (_, _) => Adopt();
        LayoutUpdated += (_, _) => SnapIfMoved();
    }

    /// <summary>Every choice, whether or not it has moved into the inner row yet.</summary>
    public IEnumerable<HakariSegment> Choices =>
        Children.OfType<HakariSegment>().Concat(choices.Children.OfType<HakariSegment>());

    /// <summary>
    /// Choices written inside this control in XAML (or added in code) land in its own
    /// children; they move into the inner row so the indicator can sit behind them.
    /// </summary>
    private void Adopt()
    {
        var newChoices = Children.OfType<HakariSegment>().ToList();
        foreach (var choice in newChoices)
        {
            Children.Remove(choice);
            choices.Children.Add(choice);
            choice.Checked += (_, _) => MoveIndicator(animate: true);
        }

        if (!Children.Contains(indicator))
        {
            Children.Add(indicator);
            Children.Add(choices);
        }
    }

    private (double Offset, double Width)? SelectedBounds()
    {
        var offset = 0.0;
        foreach (var choice in choices.Children.OfType<HakariSegment>())
        {
            if (choice.IsChecked == true)
            {
                return choice.ActualWidth > 0 ? (offset, choice.ActualWidth) : null;
            }

            offset += choice.ActualWidth + ChoiceSpacing;
        }

        return null;
    }

    /// <summary>Keeps the indicator under its choice through layout changes, unanimated.</summary>
    private void SnapIfMoved()
    {
        if (running is not null || SelectedBounds() is not var (offset, width))
        {
            return;
        }

        var moved = Math.Abs(indicatorOffset.X - offset) > SnapTolerance
            || double.IsNaN(indicator.Width)
            || Math.Abs(indicator.Width - width) > SnapTolerance
            || indicator.Opacity == 0;
        if (moved)
        {
            Place(offset, width, AnimationSetting.Off);
        }
    }

    private void MoveIndicator(bool animate)
    {
        if (SelectedBounds() is not var (offset, width))
        {
            return;
        }

        var motion = animate && indicator.Opacity > 0
            ? SurfaceMotion.Current()
            : AnimationSetting.Off;
        Place(offset, width, motion);
    }

    private void Place(double offset, double width, AnimationSetting motion)
    {
        if (motion == AnimationSetting.Full)
        {
            Slide(offset, width);
            return;
        }

        StopAnimations();
        indicatorOffset.X = offset;
        indicator.Width = width;
        indicator.Opacity = 1;
        if (motion == AnimationSetting.Reduced)
        {
            Begin(SurfaceMotion.Animate(
                indicator, OpacityPath, 0, 1, SurfaceMotion.ReducedFade));
        }
    }

    private void Slide(double offset, double width)
    {
        var duration = SurfaceMotion.Normal;
        Begin(
            SurfaceMotion.Animate(indicatorOffset, OffsetPath, null, offset, duration),
            SurfaceMotion.Animate(indicator, WidthPath, null, width, duration));
    }

    private void Begin(params Timeline[] animations)
    {
        StopAnimations();
        var storyboard = new Storyboard();
        foreach (var animation in animations)
        {
            storyboard.Children.Add(animation);
        }

        storyboard.Completed += (_, _) =>
        {
            if (running == storyboard)
            {
                StopAnimations();
            }
        };
        running = storyboard;
        storyboard.Begin();
    }

    /// <summary>Hands values back from the storyboard so a later jump is not overridden.</summary>
    private void StopAnimations()
    {
        if (running is null)
        {
            return;
        }

        var offset = indicatorOffset.X;
        var width = indicator.ActualWidth;
        var opacity = indicator.Opacity;
        running.Stop();
        running = null;
        indicatorOffset.X = offset;
        indicator.Width = width;
        indicator.Opacity = opacity;
    }
}
