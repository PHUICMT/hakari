using System.Text.Json.Serialization;
using Hakari.Core.Accounts;
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

    /// <summary>0 for "93%", 1 for "93.4%", 2 for "93.41%". Decimals are estimated.</summary>
    public int PercentDecimals { get; init; } = 1;

    /// <summary>How long each account is shown when taking turns.</summary>
    public int TurnSeconds { get; init; } = (int)WidgetPanels.DefaultTurnLength.TotalSeconds;

    [JsonIgnore]
    public TimeSpan TurnLength => TimeSpan.FromSeconds(Math.Max(1, TurnSeconds));

    /// <summary>Layouts the user saved under a name, newest first.</summary>
    public IReadOnlyList<NamedLayout> SavedLayouts { get; init; } = [];

    /// <summary>Per-account layouts for side by side and take turns, keyed by account id.</summary>
    public IReadOnlyDictionary<string, WidgetLayout> AccountLayouts { get; init; } =
        new Dictionary<string, WidgetLayout>();

    public AccountOrder AccountOrdering { get; init; } = AccountOrder.MostPressing;

    /// <summary>Account ids in the user's order, used when the ordering is Custom.</summary>
    public IReadOnlyList<string> CustomAccountOrder { get; init; } = [];

    /// <summary>Accounts folded in the flyout, showing only their summary line.</summary>
    public IReadOnlyList<string> CollapsedAccounts { get; init; } = [];

    /// <summary>The dashboard's menu folded down to its icons.</summary>
    public bool DashboardMenuFolded { get; init; }

    /// <summary>Settings sections folded shut, by their key such as "sources".</summary>
    public IReadOnlyList<string> CollapsedSettingsSections { get; init; } = [];

    /// <summary>Accounts kept out of the taskbar and flyout, such as an old sign-in.</summary>
    public IReadOnlyList<string> HiddenAccounts { get; init; } = [];

    /// <summary>
    /// What an account really pays per month in US dollars, keyed by account id, for when the
    /// plan's list price is not it, such as a discounted or annual price.
    /// </summary>
    public IReadOnlyDictionary<string, decimal> PlanPriceOverrides { get; init; } =
        new Dictionary<string, decimal>();

    /// <summary>Names the user gave accounts, keyed by account id.</summary>
    public IReadOnlyDictionary<string, string> AccountNicknames { get; init; } =
        new Dictionary<string, string>();

    public TaskbarDisplays Displays { get; init; } = TaskbarDisplays.All;

    public TrayIconStyle TrayIcon { get; init; } = TrayIconStyle.Automatic;

    /// <summary>
    /// Keep and show the short title Claude Code gives each session. Off by default: titles
    /// are written from the conversation, so reading them is the user's choice.
    /// </summary>
    public bool ShowSessionTitles { get; init; }

    /// <summary>Tell when a limit passes its warning or critical level or resets.</summary>
    public bool NotifyOnLimits { get; init; } = true;

    /// <summary>Limit alerts stay quiet until then, as "mute today" asks.</summary>
    public DateTimeOffset? AlertsMutedUntil { get; init; }

    public bool AlertsMutedAt(DateTimeOffset now) => AlertsMutedUntil > now;

    /// <summary><see cref="DisplayInfo.Id"/> values, used when Displays is Chosen.</summary>
    public IReadOnlyList<string> ChosenDisplays { get; init; } = [];

    public WslScanMode WslMode { get; init; } = WslScanMode.RunningOnly;

    public IReadOnlyList<string> ExtraConfigDirectories { get; init; } = [];

    public string Currency { get; init; } = CurrencyCodes.Dollar;

    public RateMode RateMode { get; init; } = RateMode.UsageDay;

    /// <summary>Off by default: a renewal writes to Claude Code's credentials file.</summary>
    public bool RefreshSignInAutomatically { get; init; }

    public bool Paused { get; init; }

    /// <summary>A pause that ends by itself, such as the menu's "pause for 1 hour".</summary>
    public DateTimeOffset? PausedUntil { get; init; }

    public bool IsPausedAt(DateTimeOffset now) => Paused || PausedUntil > now;

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
        && ShowSessionTitles == other.ShowSessionTitles
        && Paused == other.Paused
        && PausedUntil == other.PausedUntil;

    /// <summary>True when the widget would word and lay out the same data the same way.</summary>
    public bool PresentsSameAs(HakariSettings other) =>
        Widget == other.Widget
        && AccountsMode == other.AccountsMode
        && TurnSeconds == other.TurnSeconds
        && PercentDecimals == other.PercentDecimals
        && NotifyOnLimits == other.NotifyOnLimits
        && AlertsMutedUntil == other.AlertsMutedUntil
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

    /// <summary>What the account pays per month in dollars: its own price, else the list.</summary>
    public decimal? PlanPriceOf(string accountId, SubscriptionPlan plan) =>
        PlanPriceOverrides.TryGetValue(accountId, out var price) && price > 0
            ? price
            : PlanPrices.MonthlyDollars(plan);

    public string? NicknameOf(string accountId) =>
        AccountNicknames.TryGetValue(accountId, out var name) && name.Length > 0 ? name : null;
}
