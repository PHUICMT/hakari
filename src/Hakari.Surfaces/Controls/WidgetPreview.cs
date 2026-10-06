using Hakari.Core.Presentation.Widget;
using Hakari.Core.Settings;
using Hakari.Surfaces.Motion;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

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
    private const double InnerRingSize = 13;
    private const double InnerRingStroke = 2.5;
    private const double FullFraction = 0.999;
    private const string OpacityPath = "Opacity";
    private const double SparkWidth = 36;
    private const double SparkHeight = 16;
    private const double SparkStroke = 1.6;
    private const double BarWidth = 22;
    private const double BarHeight = 3;
    private const double TopLineHeight = 17;
    private const double BottomLineHeight = 15;

    private readonly TextBlock topText = new()
    {
        FontSize = TopFontSize,
        FontWeight = FontWeights.Bold,
    };

    private readonly TextBlock bottomText = new() { FontSize = BottomFontSize };
    private readonly Grid ring = new() { Width = RingSize, Height = RingSize };
    private readonly PreviewRing outerRing = new(RingSize, RingStroke);
    private readonly PreviewRing innerRing = new(InnerRingSize, InnerRingStroke);
    private readonly StackPanel texts = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly Polyline spark = new()
    {
        StrokeThickness = SparkStroke,
        StrokeLineJoin = PenLineJoin.Round,
        StrokeStartLineCap = PenLineCap.Round,
        StrokeEndLineCap = PenLineCap.Round,
    };

    private readonly Canvas sparkHost = new()
    {
        Width = SparkWidth,
        Height = SparkHeight,
        VerticalAlignment = VerticalAlignment.Center,
        Visibility = Visibility.Collapsed,
    };

    private readonly StackPanel bars = new()
    {
        VerticalAlignment = VerticalAlignment.Center,
        Visibility = Visibility.Collapsed,
    };

    private (double Outer, double Inner) tweenFrom;
    private (double Outer, double Inner) tweenTo;
    private DateTimeOffset tweenStartedAt;

    public WidgetPreview()
    {
        Padding = new Thickness(HorizontalPadding, 0, HorizontalPadding, 0);
        Height = WidgetHeight;
        CornerRadius = new CornerRadius(WidgetRadius);
        HorizontalAlignment = HorizontalAlignment.Left;
        Background = Brush("HakariTaskbarBrush");
        ring.Children.Add(outerRing);
        ring.Children.Add(innerRing);
        texts.Children.Add(topText);
        texts.Children.Add(bottomText);

        sparkHost.Children.Add(spark);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = RingGap };
        row.Children.Add(ring);
        row.Children.Add(sparkHost);
        row.Children.Add(bars);
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
        ShowSpark(widget.Spark);
        ShowBars(widget.TopBar, widget.BottomBar);
        if (widget.Ring is { } value)
        {
            ShowRings(value);
        }

        if (textChanged && IsLoaded)
        {
            texts.Opacity = 0;
            SurfaceMotion.Settle(texts, OpacityPath, 1);
        }
    }

    /// <summary>Recent spending as a small line, scaled to its own highest point.</summary>
    private void ShowSpark(IReadOnlyList<double>? values)
    {
        if (values is not { Count: > 1 })
        {
            sparkHost.Visibility = Visibility.Collapsed;
            return;
        }

        var highest = values.Max();
        var step = SparkWidth / (values.Count - 1);
        spark.Points = [.. values.Select((value, index) => new Windows.Foundation.Point(
            index * step,
            SparkHeight - (highest <= 0 ? 0 : value / highest * SparkHeight)))];
        spark.Stroke = Brush(highest <= 0 ? "HakariLineStrongBrush" : "HakariAccentBrush");
        sparkHost.Visibility = Visibility.Visible;
    }

    /// <summary>A thin bar beside each line that has one, as tall as its line of text.</summary>
    private void ShowBars(ComposedBar? top, ComposedBar? bottom)
    {
        bars.Children.Clear();
        bars.Visibility = top is null && bottom is null ? Visibility.Collapsed : Visibility.Visible;
        bars.Children.Add(BarCell(top, TopLineHeight));
        bars.Children.Add(BarCell(bottom, BottomLineHeight));
    }

    private static Grid BarCell(ComposedBar? bar, double lineHeight)
    {
        var cell = new Grid { Width = BarWidth, Height = lineHeight };
        if (bar is null)
        {
            return cell;
        }

        var track = new Grid
        {
            Height = BarHeight,
            VerticalAlignment = VerticalAlignment.Center,
            Background = Brush("HakariLineStrongBrush"),
            CornerRadius = new CornerRadius(BarHeight / 2),
        };
        track.Children.Add(new Border
        {
            Width = Math.Max(BarHeight, BarWidth * Math.Clamp(bar.Fraction, 0, 1)),
            HorizontalAlignment = HorizontalAlignment.Left,
            CornerRadius = new CornerRadius(BarHeight / 2),
            Background = RingBrush(bar.Tone),
        });
        cell.Children.Add(track);
        return cell;
    }

    /// <summary>The inner 5-hour ring hides when there is none or the outer is full.</summary>
    private void ShowRings(ComposedRing value)
    {
        outerRing.SetBrush(RingBrush(value.Tone));
        innerRing.SetBrush(RingBrush(value.InnerTone));
        var hasInner = value.InnerFraction is not null && value.Fraction < FullFraction;
        innerRing.Visibility = hasInner ? Visibility.Visible : Visibility.Collapsed;
        MoveRings(value.Fraction, value.InnerFraction ?? 0);
    }

    /// <summary>Tweened frame by frame under Full; set at once otherwise.</summary>
    private void MoveRings(double outer, double inner)
    {
        CompositionTarget.Rendering -= OnRendering;
        if (!IsLoaded || SurfaceMotion.Current() != AnimationSetting.Full)
        {
            outerRing.Draw(outer);
            innerRing.Draw(inner);
            return;
        }

        tweenFrom = (outerRing.Fraction, innerRing.Fraction);
        tweenTo = (outer, inner);
        tweenStartedAt = DateTimeOffset.UtcNow;
        CompositionTarget.Rendering += OnRendering;
    }

    private void OnRendering(object? sender, object args)
    {
        var elapsed = DateTimeOffset.UtcNow - tweenStartedAt;
        var progress = Math.Min(1, elapsed / SurfaceMotion.Entrance);
        var eased = 1 - Math.Pow(1 - progress, EaseExponent);
        outerRing.Draw(tweenFrom.Outer + (tweenTo.Outer - tweenFrom.Outer) * eased);
        innerRing.Draw(tweenFrom.Inner + (tweenTo.Inner - tweenFrom.Inner) * eased);
        if (progress >= 1)
        {
            CompositionTarget.Rendering -= OnRendering;
        }
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
