using Hakari.Core.Presentation;

namespace Hakari.Core.Tests.Presentation;

public sealed class PercentTextTests
{
    [Theory]
    [InlineData(93.96, 0, "93%")]
    [InlineData(93.96, 1, "93.9%")]
    [InlineData(93.4, 2, "93.40%")]
    [InlineData(100, 1, "100.0%")]
    public void Cuts_instead_of_rounding_up(double percent, int decimals, string expected) =>
        Assert.Equal(expected, PercentText.Format(percent, decimals));
}
