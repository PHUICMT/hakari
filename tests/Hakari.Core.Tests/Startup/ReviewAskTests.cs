using Hakari.Core.Startup;

namespace Hakari.Core.Tests.Startup;

public sealed class ReviewAskTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Asks_a_store_user_after_two_weeks() =>
        Assert.True(ReviewAsk.IsDue(InstallKind.Store, false, Now.AddDays(-15), Now));

    [Fact]
    public void Waits_out_the_first_two_weeks() =>
        Assert.False(ReviewAsk.IsDue(InstallKind.Store, false, Now.AddDays(-3), Now));

    [Fact]
    public void Asks_only_once() =>
        Assert.False(ReviewAsk.IsDue(InstallKind.Store, true, Now.AddDays(-30), Now));

    [Theory]
    [InlineData(InstallKind.Zip)]
    [InlineData(InstallKind.Winget)]
    public void Never_asks_outside_the_store(InstallKind install) =>
        Assert.False(ReviewAsk.IsDue(install, false, Now.AddDays(-30), Now));
}
