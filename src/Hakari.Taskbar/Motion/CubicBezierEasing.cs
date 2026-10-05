namespace Hakari.Taskbar.Motion;

/// <summary>CSS-style cubic-bezier timing function, matching the design handoff curves.</summary>
public sealed class CubicBezierEasing(double x1, double y1, double x2, double y2)
{
    private const int NewtonIterations = 8;
    private const int BisectionIterations = 24;
    private const double Precision = 1e-6;

    /// <summary>Things arriving: cubic-bezier(0, 0, 0, 1).</summary>
    public static CubicBezierEasing Decelerate { get; } = new(0, 0, 0, 1);

    /// <summary>Color and in-place changes: cubic-bezier(.2, 0, 0, 1).</summary>
    public static CubicBezierEasing Standard { get; } = new(0.2, 0, 0, 1);

    public double Evaluate(double progress)
    {
        if (progress <= 0)
        {
            return 0;
        }

        if (progress >= 1)
        {
            return 1;
        }

        return Coordinate(SolveCurveParameter(progress), y1, y2);
    }

    private double SolveCurveParameter(double targetX)
    {
        var parameter = targetX;
        for (var iteration = 0; iteration < NewtonIterations; iteration++)
        {
            var error = Coordinate(parameter, x1, x2) - targetX;
            if (Math.Abs(error) < Precision)
            {
                return parameter;
            }

            var slope = Derivative(parameter, x1, x2);
            if (Math.Abs(slope) < Precision)
            {
                break;
            }

            parameter -= error / slope;
        }

        return Bisect(targetX);
    }

    private double Bisect(double targetX)
    {
        double low = 0, high = 1, parameter = targetX;
        for (var iteration = 0; iteration < BisectionIterations; iteration++)
        {
            parameter = (low + high) / 2;
            if (Coordinate(parameter, x1, x2) < targetX)
            {
                low = parameter;
            }
            else
            {
                high = parameter;
            }
        }

        return parameter;
    }

    private static double Coordinate(double parameter, double first, double second)
    {
        var inverse = 1 - parameter;
        return 3 * inverse * inverse * parameter * first
            + 3 * inverse * parameter * parameter * second
            + parameter * parameter * parameter;
    }

    private static double Derivative(double parameter, double first, double second)
    {
        var inverse = 1 - parameter;
        return 3 * inverse * inverse * first
            + 6 * inverse * parameter * (second - first)
            + 3 * parameter * parameter * (1 - second);
    }
}
