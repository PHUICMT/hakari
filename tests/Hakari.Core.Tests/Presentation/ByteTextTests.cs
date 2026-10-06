using Hakari.Core.Presentation;

namespace Hakari.Core.Tests.Presentation;

public sealed class ByteTextTests
{
    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(900, "900 B")]
    [InlineData(12 * 1024, "12 KB")]
    [InlineData(380L * 1024 * 1024, "380 MB")]
    [InlineData(1_503_238_554L, "1.4 GB")]
    public void Reads_a_size_in_the_largest_fitting_unit(long bytes, string expected) =>
        Assert.Equal(expected, ByteText.Format(bytes));
}
