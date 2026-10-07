namespace Hakari.Core.Watching;

public sealed record ChangeTrackerOptions(
    TimeSpan Debounce,
    TimeSpan RemotePollInterval,
    TimeSpan LocalSafetyScanInterval)
{
    /// <summary>
    /// Local folders are watched, so they only need a rare safety scan. Network paths such as
    /// WSL send no change notifications and are polled instead.
    /// </summary>
    public static ChangeTrackerOptions Default { get; } = new(
        Debounce: TimeSpan.FromMilliseconds(500),
        RemotePollInterval: TimeSpan.FromSeconds(60),
        LocalSafetyScanInterval: TimeSpan.FromMinutes(10));
}
