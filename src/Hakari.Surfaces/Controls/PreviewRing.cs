using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace Hakari.Surfaces.Controls;

/// <summary>
/// One ring of the preview, drawn like the taskbar's: a track with the share filled
/// clockwise from the top, and a solid disc with a bar across once full.
/// </summary>
internal sealed partial class PreviewRing : Grid
{
    private const double FullFraction = 0.999;
    private const double FullCircleDegrees = 360;
    private const double AlmostFullCircle = 359.9;
    private const double StartDegrees = -90;
    private const double StopBarShare = 0.5;

    private readonly double size;
    private readonly double stroke;
    private readonly Ellipse track;
    private readonly Path fill;
    private readonly Ellipse stopDisc = new() { Visibility = Visibility.Collapsed };
    private readonly Rectangle stopBar;

    public PreviewRing(double size, double stroke)
    {
        this.size = size;
        this.stroke = stroke;
        Width = size;
        Height = size;
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;

        track = new Ellipse { StrokeThickness = stroke, Stroke = Brush("HakariLineStrongBrush") };
        fill = new Path
        {
            StrokeThickness = stroke,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
        };
        stopBar = new Rectangle
        {
            Width = size * StopBarShare,
            Height = stroke,
            RadiusX = stroke / 2,
            RadiusY = stroke / 2,
            Fill = new SolidColorBrush(Microsoft.UI.Colors.White),
            Visibility = Visibility.Collapsed,
        };
        Children.Add(track);
        Children.Add(fill);
        Children.Add(stopDisc);
        Children.Add(stopBar);
    }

    public double Fraction { get; private set; }

    public void SetBrush(Brush brush)
    {
        fill.Stroke = brush;
        stopDisc.Fill = brush;
    }

    public void Draw(double fraction)
    {
        Fraction = Math.Clamp(fraction, 0, 1);
        var isFull = Fraction >= FullFraction;
        var ringVisibility = isFull ? Visibility.Collapsed : Visibility.Visible;
        var stopVisibility = isFull ? Visibility.Visible : Visibility.Collapsed;
        track.Visibility = ringVisibility;
        fill.Visibility = ringVisibility;
        stopDisc.Visibility = stopVisibility;
        stopBar.Visibility = stopVisibility;
        if (!isFull)
        {
            var degrees = Math.Min(Fraction * FullCircleDegrees, AlmostFullCircle);
            fill.Data = degrees <= 0 ? null : Arc(degrees);
        }
    }

    private PathGeometry Arc(double degrees)
    {
        var radius = (size - stroke) / 2;
        var center = size / 2;
        var radians = (degrees + StartDegrees) * Math.PI / (FullCircleDegrees / 2);
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

    private static Brush Brush(string key) => (Brush)Application.Current.Resources[key];
}
