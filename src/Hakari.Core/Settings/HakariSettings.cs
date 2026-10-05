using System.Text.Json.Serialization;
using Hakari.Core.Currency;
using Hakari.Core.Displays;
using Hakari.Core.Localization;
using Hakari.Core.Presentation.Widget;
using Hakari.Core.Sources;

namespace Hakari.Core.Settings;

/// <summary>Everything the user can change. Defaults follow the design spec.</summary>
public sealed record HakariSettings
{
    public AnimationSetting Animation { get; init; } = AnimationSetting.FollowWindows;

    /// <summary>"system", or a code from <see cref="Texts.Languages"/>.</summary>
    public string Language { get; init; } = Texts.FollowSystem;

    public WidgetLayout Widget { get; init; } = new();

    public MultiAccountMode AccountsMode { get; init; } = MultiAccountMode.SideBySide;

    /// <summary>How long each account is shown when taking turns.</summary>
    public int TurnSeconds { get; init; } = (int)WidgetPanels.DefaultTurnLength.TotalSeconds;

    [JsonIgnore]
    public TimeSpan TurnLength => TimeSpan.FromSeconds(Math.Max(1, TurnSeconds));

    /// <summary>Per-account layouts for side by side and take turns, keyed by account id.</summary>
    public IReadOnlyDictionary<string, WidgetLayout> AccountLayouts { get; init; } =
        new Dictionary<string, WidgetLayout>();

    public AccountOrder AccountOrdering { get; init; } = AccountOrder.MostPressing;

    /// <summary>Account ids in the user's order, used when the ordering is Custom.</summary>
    public IReadOnlyList<string> CustomAccountOrder { get; init; } = [];

    /// <summary>Accounts kept out of the taskbar and flyout, such as an old sign-in.</summary>
    public IReadOnlyList<string> HiddenAccounts { get; init; } = [];

    /// <summary>Names the user gave accounts, keyed by account id.</summary>
    public IReadOnlyDictionary<string, string> AccountNicknames { get; init; } =
        new Dictionary<string, string>();

    public TaskbarDisplays Displays { get; init; } = TaskbarDisplays.All;

    /// <summary><see cref="DisplayInfo.Id"/> values, used when Displays is Chosen.</summary>
    public IReadOnlyList<string> ChosenDisplays { get; init; } = [];

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

    /// <summary>True when the widget would word and lay out the same data the same way.</summary>
    public bool PresentsSameAs(HakariSettings other) =>
        Widget == other.Widget
        && AccountsMode == other.AccountsMode
        && TurnSeconds == other.TurnSeconds
        && AccountOrdering == other.AccountOrdering
        && CustomAccountOrder.SequenceEqual(other.CustomAccountOrder)
        && HiddenAccounts.SequenceEqual(other.HiddenAccounts)
        && AccountLayouts.Count == other.AccountLayouts.Count
        && AccountLayouts.All(entry =>
            other.AccountLayouts.TryGetValue(entry.Key, out var layout) && layout == entry.Value)
        && Language == other.Language
        && AccountNicknames.Count == other.AccountNicknames.Count
        && AccountNicknames.All(entry =>
            other.AccountNicknames.TryGetValue(entry.Key, out var name) && name == entry.Value);

    /// <summary>The account's own layout, else the shared one.</summary>
    public WidgetLayout LayoutOf(string accountId) =>
        AccountLayouts.TryGetValue(accountId, out var layout) ? layout : Widget;

    public string? NicknameOf(string accountId) =>
        AccountNicknames.TryGetValue(accountId, out var name) && name.Length > 0 ? name : null;
}
