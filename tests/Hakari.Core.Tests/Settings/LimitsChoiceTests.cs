using Hakari.Core.Settings;

namespace Hakari.Core.Tests.Settings;

public sealed class LimitsChoiceTests
{
    [Fact]
    public void A_new_account_is_not_read_until_the_user_says_yes()
    {
        var settings = new HakariSettings();

        Assert.False(settings.MayReadLimits("new", readBefore: false));
        Assert.True(settings.WithLimitsChoice("new", on: true).MayReadLimits("new", false));
    }

    [Fact]
    public void An_account_read_before_carries_on_until_turned_off()
    {
        var settings = new HakariSettings();

        Assert.True(settings.MayReadLimits("old", readBefore: true));
        Assert.False(settings.WithLimitsChoice("old", on: false).MayReadLimits("old", true));
    }
}
