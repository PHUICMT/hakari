using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace Hakari.Surfaces.Controls;

/// <summary>
/// An icon from the design, drawn as strokes in whatever foreground it inherits, so it takes
/// the nav item's muted, hover and chosen colors without any extra wiring.
/// </summary>
internal sealed partial class DesignIcon : UserControl
{
    private const double DrawingSize = 16;
    private const double StrokeWidth = 1.3;
    private const string PathXaml =
        "<Path xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' Data='{0}'/>";

    private readonly List<(Path Path, bool IsFilled)> paths = [];

    public DesignIcon(IReadOnlyList<IconShape> shapes, double size)
    {
        var canvas = new Canvas { Width = DrawingSize, Height = DrawingSize };
        foreach (var shape in shapes)
        {
            var path = (Path)XamlReader.Load(string.Format(PathXaml, shape.Data));
            path.StrokeThickness = StrokeWidth;
            path.StrokeStartLineCap = PenLineCap.Round;
            path.StrokeEndLineCap = PenLineCap.Round;
            path.StrokeLineJoin = PenLineJoin.Round;
            paths.Add((path, shape.IsFilled));
            canvas.Children.Add(path);
        }

        Content = new Viewbox { Width = size, Height = size, Child = canvas };
        IsTabStop = false;
        RegisterPropertyChangedCallback(ForegroundProperty, (_, _) => Paint());
        Loaded += (_, _) => Paint();
    }

    private void Paint()
    {
        foreach (var (path, isFilled) in paths)
        {
            path.Stroke = Foreground;
            path.Fill = isFilled ? Foreground : null;
        }
    }
}
