namespace Hakari.Core.Limits;

/// <summary>
/// Estimates when a limit reaches 100% from how fast its percentage rose recently. Kept in
/// memory from live readings only; after a restart it needs a few readings before it answers.
/// </summary>
public sealed class LimitForecaster
{
    public const int FullPercent = 100;

    /// <summary>Readings older than this say little about the current pace.</summary>
    private static readonly TimeSpan PaceWindow = TimeSpan.FromMinutes(30);

    /// <summary>Two readings closer than this make a noisy pace.</summary>
    private static readonly TimeSpan MinimumSpan = TimeSpan.FromMinutes(5);

    private const int MinimumRise = 1;

    private readonly Dictionary<string, List<LimitSample>> samplesByLimit = [];

    public void Record(string accountId, LimitSnapshot snapshot)
    {
        if (snapshot.Freshness != LimitFreshness.Live)
        {
            return;
        }

        foreach (var limit in snapshot.Limits)
        {
            Record(KeyOf(accountId, limit), limit, snapshot.FetchedAt);
        }
    }

    /// <summary>
    /// Null when there is not enough history, usage is not rising, the limit is already
    /// full, or it resets before it would fill.
    /// </summary>
    public DateTimeOffset? FullAt(string accountId, UsageLimit limit, DateTimeOffset now) =>
        samplesByLimit.TryGetValue(KeyOf(accountId, limit), out var samples)
            ? Project(
                [.. samples.Select(sample => (sample.At, (double)sample.Percent))],
                limit,
                now)
            : null;

    /// <summary>
    /// The same estimate from the saved history, for a window that did not watch the
    /// readings arrive. Readings before the latest drop belong to an earlier window.
    /// </summary>
    public static DateTimeOffset? FullAt(
        LimitHistory history,
        string accountId,
        UsageLimit limit,
        DateTimeOffset now)
    {
        if (limit.Percent >= FullPercent)
        {
            return null;
        }

        var readings = history.Load(accountId, limit.Kind, now - PaceWindow);
        var start = 0;
        for (var index = 1; index < readings.Count; index++)
        {
            if (readings[index].Percent < readings[index - 1].Percent)
            {
                start = index;
            }
        }

        return Project(
            [.. readings.Skip(start).Select(reading => (reading.At, reading.Percent))],
            limit,
            now);
    }

    private static DateTimeOffset? Project(
        IReadOnlyList<(DateTimeOffset At, double Percent)> samples,
        UsageLimit limit,
        DateTimeOffset now)
    {
        if (limit.Percent >= FullPercent || samples.Count == 0)
        {
            return null;
        }

        var latest = samples[^1];
        var baselineIndex = -1;
        for (var index = 0; index < samples.Count; index++)
        {
            if (latest.At - samples[index].At >= MinimumSpan
                && now - samples[index].At <= PaceWindow)
            {
                baselineIndex = index;
                break;
            }
        }

        if (baselineIndex < 0 || latest.Percent - samples[baselineIndex].Percent < MinimumRise)
        {
            return null;
        }

        var baseline = samples[baselineIndex];
        var percentPerSecond = (latest.Percent - baseline.Percent)
            / (latest.At - baseline.At).TotalSeconds;
        var fullAt = latest.At.AddSeconds((FullPercent - latest.Percent) / percentPerSecond);
        return limit.ResetsAt is { } resetsAt && fullAt >= resetsAt ? null : fullAt;
    }

    private void Record(string key, UsageLimit limit, DateTimeOffset at)
    {
        if (!samplesByLimit.TryGetValue(key, out var samples))
        {
            samples = [];
            samplesByLimit[key] = samples;
        }

        // A new reset time is a new window: the old readings belong to the previous one.
        if (samples.Count > 0 && samples[^1].ResetsAt != limit.ResetsAt)
        {
            samples.Clear();
        }

        if (samples.Count > 0 && samples[^1].At >= at)
        {
            return;
        }

        samples.Add(new LimitSample(at, limit.Percent, limit.ResetsAt));
        samples.RemoveAll(sample => at - sample.At > PaceWindow);
    }

    private static string KeyOf(string accountId, UsageLimit limit) =>
        $"{accountId}|{limit.Kind}|{limit.ScopeName}";
}
