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
}
