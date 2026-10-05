using Hakari.Core.Presentation.Widget;
using Hakari.Core.Settings;
using Hakari.Surfaces.Motion;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace Hakari.Surfaces.Controls;

/// <summary>
/// A copy of the taskbar widget drawn with XAML, fed by the same composer as the real one,
/// so a layout change shows here exactly as it will on the taskbar.
/// </summary>
public sealed partial class WidgetPreview : Grid
{
    private const double WidgetHeight = 40;
    private const double WidgetRadius = 4;
    private const double HorizontalPadding = 10;
    private const double EaseExponent = 3;
    private const double RingSize = 22;
    private const double RingStroke = 3;
    private const double RingGap = 8;
    private const double TopFontSize = 13;
    private const double BottomFontSize = 11;
    private const double FullCircleDegrees = 360;
    private const double AlmostFullCircle = 359.9;
    private const string OpacityPath = "Opacity";

    private readonly TextBlock topText = new()
    {
        FontSize = TopFontSize,
        FontWeight = FontWeights.Bold,
    };

    private readonly TextBlock bottomText = new() { FontSize = BottomFontSize };
    private readonly Grid ring = new() { Width = RingSize, Height = RingSize };
    private readonly Path ringFill = new() { StrokeThickness = RingStroke };
    private readonly StackPanel texts = new() { VerticalAlignment = VerticalAlignment.Center };

    private double shownFraction;
    private double tweenFrom;
    private double tweenTo;
    private DateTimeOffset tweenStartedAt;

    public WidgetPreview()
    {
        Padding = new Thickness(HorizontalPadding, 0, HorizontalPadding, 0);
        Height = WidgetHeight;
        CornerRadius = new CornerRadius(WidgetRadius);
        HorizontalAlignment = HorizontalAlignment.Left;
        Background = Brush("HakariTaskbarBrush");
        BuildRing();
        texts.Children.Add(topText);
        texts.Children.Add(bottomText);

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = RingGap };
        row.Children.Add(ring);
        row.Children.Add(texts);
        Children.Add(row);
    }

    public void Show(ComposedWidget widget)
    {
        var textChanged = topText.Text != widget.Top.Text
            || bottomText.Text != widget.Bottom.Text;
        topText.Text = widget.Top.Text;
        topText.Foreground = TextBrush(widget.Top.Tone, isTop: true);
        bottomText.Text = widget.Bottom.Text;
        bottomText.Foreground = TextBrush(widget.Bottom.Tone, isTop: false);
        bottomText.Visibility = widget.Bottom.Text.Length == 0
            ? Visibility.Collapsed
            : Visibility.Visible;

        ring.Visibility = widget.Ring is null ? Visibility.Collapsed : Visibility.Visible;
        if (widget.Ring is { } value)
        {
            ringFill.Stroke = RingBrush(value.Tone);
            MoveRing(value.Fraction);
        }

        if (textChanged && IsLoaded)
        {
            texts.Opacity = 0;
            SurfaceMotion.Settle(texts, OpacityPath, 1);
        }
    }

    private void BuildRing()
    {
        ring.Children.Add(new Ellipse
        {
            Stroke = Brush("HakariLineStrongBrush"),
            StrokeThickness = RingStroke,
        });
        ringFill.StrokeStartLineCap = PenLineCap.Round;
        ringFill.StrokeEndLineCap = PenLineCap.Round;
        ring.Children.Add(ringFill);
    }

    /// <summary>Tweened frame by frame under Full; set at once otherwise.</summary>
    private void MoveRing(double fraction)
    {
        CompositionTarget.Rendering -= OnRendering;
        if (!IsLoaded || SurfaceMotion.Current() != AnimationSetting.Full)
        {
            DrawRing(fraction);
            return;
        }

        tweenFrom = shownFraction;
        tweenTo = fraction;
        tweenStartedAt = DateTimeOffset.UtcNow;
        CompositionTarget.Rendering += OnRendering;
    }

    private void OnRendering(object? sender, object args)
    {
        var elapsed = DateTimeOffset.UtcNow - tweenStartedAt;
        var progress = Math.Min(1, elapsed / SurfaceMotion.Entrance);
        var eased = 1 - Math.Pow(1 - progress, EaseExponent);
        DrawRing(tweenFrom + (tweenTo - tweenFrom) * eased);
        if (progress >= 1)
        {
            CompositionTarget.Rendering -= OnRendering;
        }
    }

    private void DrawRing(double fraction)
    {
        shownFraction = Math.Clamp(fraction, 0, 1);
        var degrees = Math.Min(shownFraction * FullCircleDegrees, AlmostFullCircle);
        ringFill.Data = degrees <= 0 ? null : Arc(degrees);
    }

    private static PathGeometry Arc(double degrees)
    {
        var radius = (RingSize - RingStroke) / 2;
        var center = RingSize / 2;
        var radians = (degrees - 90) * Math.PI / 180;
        var figure = new PathFigure { StartPoint = new Point(center, center - radius) };
        figure.Segments.Add(new ArcSegment
        {
            Point = new Point(
                center + radius * Math.Cos(radians),
                center + radius * Math.Sin(radians)),
            Size = new Size(radius, radius),
            IsLargeArc = degrees > FullCircleDegrees / 2,
            SweepDirection = SweepDirection.Clockwise,
        });
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }

    private static Brush TextBrush(LineTone tone, bool isTop) => tone switch
    {
        LineTone.Warning => Brush("HakariWarnBrush"),
        LineTone.Critical => Brush("HakariCriticalBrush"),
        LineTone.Muted => Brush("HakariInkFaintBrush"),
        _ => Brush(isTop ? "HakariInkBrush" : "HakariInkMutedBrush"),
    };

    private static Brush RingBrush(LineTone tone) =>
        tone == LineTone.Normal ? Brush("HakariAccentBrush") : TextBrush(tone, isTop: true);

    private static Brush Brush(string key) => (Brush)Application.Current.Resources[key];
}
