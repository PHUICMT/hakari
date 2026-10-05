namespace Hakari.Core.Sources;

public sealed record SourceDiscoveryOptions(
    WslScanMode WslMode,
    IReadOnlyList<string> ExtraConfigDirectories)
{
    public static SourceDiscoveryOptions Default { get; } = new(WslScanMode.RunningOnly, []);
}
