using Hakari.Core.Presentation.Widget;

namespace Hakari.Core.Tests.Presentation;

public sealed class StaleWidgetTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Turns_stale_after_ten_minutes_without_news()
    {
        Assert.False(StaleWidget.IsStale(Now.AddMinutes(-9), Now));
        Assert.True(StaleWidget.IsStale(Now.AddMinutes(-10), Now));
    }

    [Fact]
    public void Marks_the_last_value_with_a_dot()
    {
        Assert.Equal("· $142.18 today", StaleWidget.Value("$142.18 today"));
        Assert.Equal(string.Empty, StaleWidget.Value(string.Empty));
    }
}
