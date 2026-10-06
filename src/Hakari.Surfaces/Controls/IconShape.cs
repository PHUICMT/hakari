namespace Hakari.Surfaces.Controls;

/// <summary>One stroked or filled outline of an icon, as SVG path data in a 16 by 16 box.</summary>
internal sealed record IconShape(string Data, bool IsFilled = false)
{
    /// <summary>A circle written as path data, since a path is all the icon draws.</summary>
    public static IconShape Circle(
        double centerX,
        double centerY,
        double radius,
        bool filled = false)
    {
        var diameter = radius * 2;
        return new IconShape(
            $"M{Number(centerX - radius)} {Number(centerY)}"
            + $"a{Number(radius)} {Number(radius)} 0 1 0 {Number(diameter)} 0"
            + $"a{Number(radius)} {Number(radius)} 0 1 0 {Number(-diameter)} 0",
            filled);
    }

    /// <summary>A rectangle with rounded corners written as path data.</summary>
    public static IconShape Box(double x, double y, double width, double height, double corner)
    {
        var straightWidth = width - corner * 2;
        var straightHeight = height - corner * 2;
        var arc = $"a{Number(corner)} {Number(corner)} 0 0 1";
        return new IconShape(
            $"M{Number(x + corner)} {Number(y)}h{Number(straightWidth)}"
            + $"{arc} {Number(corner)} {Number(corner)}v{Number(straightHeight)}"
            + $"{arc} {Number(-corner)} {Number(corner)}h{Number(-straightWidth)}"
            + $"{arc} {Number(-corner)} {Number(-corner)}v{Number(-straightHeight)}"
            + $"{arc} {Number(corner)} {Number(-corner)}z");
    }

    private static string Number(double value) =>
        value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
}
