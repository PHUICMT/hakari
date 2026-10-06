using Hakari.Core.Limits;

namespace Hakari.Core.Tests.Limits;

public sealed class SpendWatchTests
{
    [Fact]
    public void The_usual_session_is_the_middle_one()
    {
        Assert.Equal(3m, SpendWatch.Median([5m, 1m, 3m]));
        Assert.Equal(2.5m, SpendWatch.Median([4m, 1m, 2m, 3m]));
        Assert.Equal(0m, SpendWatch.Median([]));
    }
}
