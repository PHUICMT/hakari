using Hakari.Core.Settings;
using Hakari.Core.Sources;

namespace Hakari.Core.Tests.Settings;

public sealed class HakariSettingsTests
{
    [Fact]
    public void Display_only_changes_keep_the_same_feed()
    {
        var before = new HakariSettings { ExtraConfigDirectories = [@"D:\Other"] };
        var after = before with
        {
            Animation = AnimationSetting.Off,
            Displays = Hakari.Core.Displays.TaskbarDisplays.Primary,
            ExtraConfigDirectories = [@"d:\other"],
        };

        Assert.True(after.FeedsSameDataAs(before));
    }

    [Fact]
    public void Source_and_currency_changes_need_a_new_feed()
    {
        var before = new HakariSettings();

        Assert.False((before with { WslMode = WslScanMode.Off }).FeedsSameDataAs(before));
        Assert.False((before with { Currency = "THB" }).FeedsSameDataAs(before));
        Assert.False((before with { Paused = true }).FeedsSameDataAs(before));
        Assert.False((before with { ExtraConfigDirectories = [@"D:\Other"] })
            .FeedsSameDataAs(before));
    }
}
