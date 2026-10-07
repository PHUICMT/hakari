using Hakari.Core.Updates;

namespace Hakari.Core.Tests.Updates;

public sealed class UpdateCheckTests
{
    [Fact]
    public void Picks_the_highest_published_version_and_skips_drafts()
    {
        const string releases = """
            [
              { "tag_name": "v0.9.1", "draft": false, "html_url": "https://example.com/0.9.1" },
              { "tag_name": "v0.10.0", "draft": true, "html_url": "https://example.com/0.10.0" },
              { "tag_name": "v0.9.10", "draft": false, "html_url": "https://example.com/0.9.10" },
              { "tag_name": "nightly", "draft": false }
            ]
            """;

        var newest = UpdateCheck.Newest(releases);

        Assert.Equal(("0.9.10", "https://example.com/0.9.10"), newest);
    }

    [Theory]
    [InlineData("v0.9.2", "0.9.2")]
    [InlineData("1.0.0-beta", "1.0.0")]
    [InlineData("0.9.1", "0.9.1")]
    public void Reads_tags_with_or_without_prefix_and_suffix(string tag, string expected)
    {
        Assert.True(UpdateCheck.TryParse(tag, out var version));
        Assert.Equal(Version.Parse(expected), version);
    }

    [Fact]
    public void An_empty_or_odd_answer_finds_nothing()
    {
        Assert.Null(UpdateCheck.Newest("[]"));
        Assert.Null(UpdateCheck.Newest("""{ "message": "rate limited" }"""));
    }
}
