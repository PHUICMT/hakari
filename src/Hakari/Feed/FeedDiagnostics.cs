namespace Hakari.Feed;

public sealed class FeedDiagnostics
{
    public TimeSpan LastIndexDuration { get; set; }

    public int IndexPasses { get; set; }

    public int LimitPolls { get; set; }
}
