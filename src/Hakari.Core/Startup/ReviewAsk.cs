namespace Hakari.Core.Startup;

/// <summary>
/// When to ask, once, for a rating in the Store: only in the Store copy, after two weeks of
/// use, so the answer comes from someone who has lived with Hakari, and never again after any
/// answer.
/// </summary>
public static class ReviewAsk
{
    public static readonly TimeSpan After = TimeSpan.FromDays(14);

    public static bool IsDue(
        InstallKind install,
        bool alreadyAsked,
        DateTimeOffset? firstRun,
        DateTimeOffset now) =>
        install == InstallKind.Store
        && !alreadyAsked
        && firstRun is { } since
        && now - since >= After;
}
