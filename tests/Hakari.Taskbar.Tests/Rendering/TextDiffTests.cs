using Hakari.Taskbar.Rendering;

namespace Hakari.Taskbar.Tests.Rendering;

public sealed class TextDiffTests
{
    [Fact]
    public void Rolls_only_the_digits_that_changed()
    {
        var diff = TextDiff.Between("Week 93.4% · ↺ 1:48", "Week 93.5% · ↺ 1:48");

        Assert.Equal("Week 93.", diff.Prefix);
        Assert.Equal("4", diff.OldMiddle);
        Assert.Equal("5", diff.NewMiddle);
        Assert.Equal("% · ↺ 1:48", diff.Suffix);
    }

    [Fact]
    public void Handles_a_longer_number()
    {
        var diff = TextDiff.Between("฿9,999 today", "฿10,000 today");

        Assert.Equal("฿", diff.Prefix);
        Assert.Equal("9,999", diff.OldMiddle);
        Assert.Equal("10,000", diff.NewMiddle);
        Assert.Equal(" today", diff.Suffix);
    }

    [Fact]
    public void Knows_when_nothing_is_shared()
    {
        Assert.False(TextDiff.Between("abc", "xyz").SharesAnything);
    }
}
