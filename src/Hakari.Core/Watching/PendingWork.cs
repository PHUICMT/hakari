using Hakari.Core.Sources;

namespace Hakari.Core.Watching;

public sealed record PendingWork(
    IReadOnlyList<UsageSource> FullScans,
    IReadOnlyDictionary<UsageSource, IReadOnlyCollection<string>> ChangedFiles)
{
    public bool IsEmpty => FullScans.Count == 0 && ChangedFiles.Count == 0;

    public static PendingWork FullScansOf(IEnumerable<UsageSource> sources) =>
        new([.. sources], new Dictionary<UsageSource, IReadOnlyCollection<string>>());
}
