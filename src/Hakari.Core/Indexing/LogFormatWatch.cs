using System.Globalization;
using Hakari.Core.Parsing;

namespace Hakari.Core.Indexing;

/// <summary>How many lines of each kind a scan met, for <see cref="LogFormatWatch"/>.</summary>
public sealed record LineKindCounts(long Usage, long Empty, long Unreadable, long Missed)
{
    public static LineKindCounts None { get; } = new(0, 0, 0, 0);

    /// <summary>Lines that were, or looked like, responses with usage.</summary>
    public long Responses => Usage + Empty + Unreadable + Missed;

    /// <summary>Of those, the ones whose usage did not come through.</summary>
    public long Lost => Empty + Unreadable + Missed;

    public LineKindCounts Add(LineKind kind) => kind switch
    {
        LineKind.Usage => this with { Usage = Usage + 1 },
        LineKind.Empty => this with { Empty = Empty + 1 },
        LineKind.Unreadable => this with { Unreadable = Unreadable + 1 },
        LineKind.Missed => this with { Missed = Missed + 1 },
        _ => this,
    };

    public static LineKindCounts operator +(LineKindCounts left, LineKindCounts right) => new(
        left.Usage + right.Usage,
        left.Empty + right.Empty,
        left.Unreadable + right.Unreadable,
        left.Missed + right.Missed);
}

/// <summary>
/// Notices when Claude Code writes its logs in a shape Hakari no longer reads, so the numbers
/// are not quietly short. Scans add their line counts until there are enough responses to
/// judge; then, if most of them lost their usage, the format is marked as changed, until a
/// later sample reads fine again.
/// </summary>
public static class LogFormatWatch
{
    private const string CountsOption = "logFormat.counts";
    private const string ChangedOption = "logFormat.changed";
    private const char Separator = ',';

    /// <summary>Enough responses that a few odd lines cannot tip the answer.</summary>
    public const long SampleSize = 200;

    /// <summary>Lost lines in a healthy log are rare; this many means the format moved.</summary>
    public const double LostShare = 0.5;

    public static void Record(IndexStore store, LineKindCounts scanned)
    {
        if (scanned.Responses == 0)
        {
            return;
        }

        var total = Load(store) + scanned;
        if (total.Responses < SampleSize)
        {
            store.SetOption(CountsOption, Format(total));
            return;
        }

        var changed = (double)total.Lost / total.Responses > LostShare;
        store.SetOption(ChangedOption, changed.ToString());
        store.SetOption(CountsOption, Format(LineKindCounts.None));
    }

    /// <summary>True when the last full sample mostly could not be read.</summary>
    public static bool SeemsChanged(IndexStore store) =>
        store.GetOption(ChangedOption) == bool.TrueString;

    private static LineKindCounts Load(IndexStore store)
    {
        var parts = (store.GetOption(CountsOption) ?? string.Empty).Split(Separator);
        long Part(int index) => parts.Length > index
            && long.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture,
                out var value)
            ? value
            : 0;
        return new LineKindCounts(Part(0), Part(1), Part(2), Part(3));
    }

    private static string Format(LineKindCounts counts) => string.Join(
        Separator,
        new[] { counts.Usage, counts.Empty, counts.Unreadable, counts.Missed }
            .Select(value => value.ToString(CultureInfo.InvariantCulture)));
}
