using Hakari.Core.Accounts;
using Hakari.Core.Limits;
using Hakari.Core.Presentation;

namespace Hakari.Core.Tests.Presentation;

public sealed class LimitTextTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Counts_down_to_the_reset_once_full()
    {
        var text = LimitText.Compact(Session(100), fullAt: null, Now);

        Assert.Equal("5h full · ↺ 1:42", text);
    }

    [Fact]
    public void Shows_when_the_pace_fills_the_limit()
    {
        var fullAt = new DateTimeOffset(2026, 10, 5, 12, 40, 0, TimeSpan.Zero);

        var text = LimitText.Compact(Session(82), fullAt, Now);

        Assert.Equal($"5h 82% · full ~{fullAt.ToLocalTime():HH:mm}", text);
    }

    [Fact]
    public void Shows_percent_and_reset_otherwise()
    {
        Assert.Equal("5h 67% · ↺ 1:42", LimitText.Compact(Session(67), null, Now));
    }

    [Theory]
    [InlineData("someone.long@example.com", null, "someone.")]
    [InlineData("work@example.com", null, "work")]
    [InlineData(null, "Alex Kim", "Alex")]
    [InlineData(null, null, "Account")]
    public void Labels_accounts_briefly(string? email, string? displayName, string expected)
    {
        var account = new AccountInfo("id", email, displayName, null, SubscriptionPlan.Pro);

        Assert.Equal(expected, AccountLabels.Short(account));
    }

    private static UsageLimit Session(int percent) => new(
        "session",
        "session",
        percent,
        "normal",
        Now.AddHours(1).AddMinutes(42),
        null,
        IsActive: true);
}
