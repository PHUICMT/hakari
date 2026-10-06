using Hakari.Core.Limits;
using Hakari.Core.Presentation;

namespace Hakari.Core.Tests.Presentation;

public sealed class LimitAlertTextTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

    private static LimitAlert Alert(LimitAlertKind kind, double percent) => new(
        "account",
        new UsageLimit("weekly_all", "weekly", (int)percent, "normal", Now.AddDays(2), null, true),
        kind,
        percent);

    [Fact]
    public void A_warning_says_when_it_fills_before_the_reset()
    {
        var message = LimitAlertText.Compose(
            Alert(LimitAlertKind.Warning, 82), string.Empty, Now, fullAt: Now.AddHours(5));

        Assert.Contains(Now.AddHours(5).ToLocalTime().ToString("HH:mm"), message.Detail);
        Assert.Equal(0.82, message.Progress, precision: 6);
    }

    [Fact]
    public void A_full_limit_points_to_an_account_with_room()
    {
        var message = LimitAlertText.Compose(
            Alert(LimitAlertKind.Critical, 100), "Work", Now, roomElsewhere: ("Home", 18));

        Assert.Contains("Home", message.Detail);
        Assert.Contains("82%", message.Detail);
    }

    [Fact]
    public void A_reset_is_no_warning()
    {
        Assert.False(LimitAlertText.Compose(Alert(LimitAlertKind.Reset, 3), "", Now).IsWarning);
    }
}
