using Hakari.Core.Presentation;

namespace Hakari.Core.Tests.Presentation;

public sealed class TokenTextTests
{
    [Theory]
    [InlineData(812, "812")]
    [InlineData(12_400, "12.4K")]
    [InlineData(4_200_000, "4.2M")]
    [InlineData(1_300_000_000, "1.3B")]
    public void Shortens_big_counts(long count, string expected) =>
        Assert.Equal(expected, TokenText.Format(count));
}
