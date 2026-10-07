namespace Hakari.Core.Sources;

/// <summary>
/// Whether a source may be touched right now. A WSL source's files live inside its
/// distribution, and opening them while it is stopped would start it, so they are only
/// touched while it runs. Which distributions run is asked right before, at most every few
/// seconds: WSL stops an idle distribution within seconds, and an older answer could wake it.
/// Callers touch WSL folders rarely (a full scan a minute, a sign-in check every 30 s).
/// </summary>
public sealed class WslReach(TimeProvider timeProvider, WslScanMode mode)
{
    private const char IdSeparator = ':';
    private static readonly TimeSpan Fresh = TimeSpan.FromSeconds(2);

    private DateTimeOffset checkedAt = DateTimeOffset.MinValue;
    private IReadOnlySet<string> running = new HashSet<string>();

    public bool CanTouch(UsageSource source)
    {
        if (source.Kind != SourceKind.Wsl)
        {
            return true;
        }

        if (mode == WslScanMode.Off)
        {
            return false;
        }

        // Starting stopped distributions is what "all" mode is for.
        if (mode == WslScanMode.All)
        {
            return true;
        }

        var now = timeProvider.GetUtcNow();
        if (now - checkedAt > Fresh)
        {
            checkedAt = now;
            running = WslLocator.FindRunningDistributions()
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        var parts = source.Id.Split(IdSeparator);
        return parts.Length > 1 && running.Contains(parts[1]);
    }
}