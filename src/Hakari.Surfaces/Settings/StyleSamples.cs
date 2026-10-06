using Hakari.Core.Presentation.Widget;
using Hakari.Surfaces.Controls;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;

namespace Hakari.Surfaces.Settings;

/// <summary>Small drawings of each slot style, for the style list: text, a ring, a line.</summary>
internal static class StyleSamples
{
    private const double Size = 16;
    private const double RingStroke = 2.5;
    private const double SampleFraction = 0.62;
    private static readonly double[] SparkPoints = [0.2, 0.5, 0.35, 0.9, 0.6, 1, 0.7];

    public static FrameworkElement? Of(object value) => value switch
    {
        WidgetSlotStyle.Text => Text(),
        WidgetSlotStyle.Ring => Ring(),
        WidgetSlotStyle.Sparkline => Spark(),
        _ => null,
    };

    private static TextBlock Text() => new()
    {
        Text = "Aa",
        FontSize = 12,
        FontWeight = FontWeights.SemiBold,
        Foreground = Brush("HakariInkMutedBrush"),
    };

    private static PreviewRing Ring()
    {
        var ring = new PreviewRing(Size, RingStroke);
        ring.SetBrush(Brush("HakariAccentBrush"));
        ring.Draw(SampleFraction);
        return ring;
    }

    private static Polyline Spark()
    {
        var width = Size * 2;
        var step = width / (SparkPoints.Length - 1);
        return new Polyline
        {
            Width = width,
            Height = Size,
            Stroke = Brush("HakariAccentBrush"),
            StrokeThickness = 1.6,
            StrokeLineJoin = PenLineJoin.Round,
            Points = [.. SparkPoints.Select((point, index) =>
                new Point(index * step, Size - point * (Size - 2)))],
        };
    }

    private static Brush Brush(string key) => (Brush)Application.Current.Resources[key];
}
