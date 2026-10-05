using Hakari.Core.Currency;
using Hakari.Core.Sources;

namespace Hakari.Core.Settings;

/// <summary>Everything the user can change. Defaults follow the design spec.</summary>
public sealed record HakariSettings
{
    public AnimationSetting Animation { get; init; } = AnimationSetting.FollowWindows;

    public bool ShowOnSecondaryTaskbars { get; init; } = true;

    public WslScanMode WslMode { get; init; } = WslScanMode.RunningOnly;

    public IReadOnlyList<string> ExtraConfigDirectories { get; init; } = [];

    public string Currency { get; init; } = CurrencyCodes.Dollar;

    public RateMode RateMode { get; init; } = RateMode.UsageDay;

    /// <summary>Off by default: a renewal writes to Claude Code's credentials file.</summary>
    public bool RefreshSignInAutomatically { get; init; }

    public bool Paused { get; init; }

    /// <summary>
    /// True when the background feed would read and show the same data under both, so a
    /// change of animations or displays alone does not restart it.
    /// </summary>
    public bool FeedsSameDataAs(HakariSettings other) =>
        WslMode == other.WslMode
        && ExtraConfigDirectories.SequenceEqual(
            other.ExtraConfigDirectories,
            StringComparer.OrdinalIgnoreCase)
        && string.Equals(Currency, other.Currency, StringComparison.OrdinalIgnoreCase)
        && RateMode == other.RateMode
        && RefreshSignInAutomatically == other.RefreshSignInAutomatically
        && Paused == other.Paused;
}
