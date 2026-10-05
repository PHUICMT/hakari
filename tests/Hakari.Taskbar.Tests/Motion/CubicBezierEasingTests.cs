using Hakari.Taskbar.Motion;

namespace Hakari.Taskbar.Tests.Motion;

public class CubicBezierEasingTests
{
    private const double Tolerance = 1e-4;

    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(1.0, 1.0)]
    public void Starts_and_ends_exactly(double progress, double expected) =>
        Assert.Equal(expected, CubicBezierEasing.Decelerate.Evaluate(progress), Tolerance);

    [Fact]
    public void Linear_curve_returns_its_input()
    {
        var linear = new CubicBezierEasing(0, 0, 1, 1);

        Assert.Equal(0.3, linear.Evaluate(0.3), Tolerance);
    }

    [Fact]
    public void Decelerate_is_mostly_done_early()
    {
        Assert.True(CubicBezierEasing.Decelerate.Evaluate(0.25) > 0.6);
    }

    [Fact]
    public void Never_runs_backwards()
    {
        var previous = 0.0;
        for (var step = 1; step <= 100; step++)
        {
            var value = CubicBezierEasing.Standard.Evaluate(step / 100.0);
            Assert.True(value >= previous - Tolerance);
            previous = value;
        }
    }
}
