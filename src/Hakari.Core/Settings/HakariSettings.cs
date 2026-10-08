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

    /// <summary>The page, filter and size the dashboard was left with.</summary>
    public DashboardMemory Dashboard { get; init; } = new();

    /// <summary>Settings sections folded shut, by their key such as "sources".</summary>
    public IReadOnlyList<string> CollapsedSettingsSections { get; init; } = [];

    /// <summary>Dashboard cards and sections folded shut, by their title.</summary>
    public IReadOnlyList<string> FoldedDashboardSections { get; init; } = [];

    /// <summary>Accounts kept out of the taskbar and flyout, such as an old sign-in.</summary>
    public IReadOnlyList<string> HiddenAccounts { get; init; } = [];

    /// <summary>
    /// What an account really pays per month in US dollars, keyed by account id, for when the
    /// plan's list price is not it, such as a discounted or annual price.
    /// </summary>
    public IReadOnlyDictionary<string, decimal> PlanPriceOverrides { get; init; } =
        new Dictionary<string, decimal>();

    /// <summary>
    /// Project folders joined into another, such as a project moved or copied: each joined
    /// path keyed to the path it is counted under. Only the dashboard reads it.
    /// </summary>
    public IReadOnlyDictionary<string, string> ProjectMerges { get; init; } =
        new Dictionary<string, string>();

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

    /// <summary>
    /// Note which responses had a thinking part, by the kind of their parts alone, for the
    /// share of responses that thought. Off unless the user asked.
    /// </summary>
    public bool CountThinking { get; init; }

    /// <summary>
    /// Look once a day at the project's public release list and say in the flyout when a
    /// newer version is out. Nothing about the user is sent.
    /// </summary>
    public bool CheckForUpdates { get; init; } = true;

    /// <summary>
    /// The last version whose "what's new" was seen, as "1.2.3"; empty on a first install,
    /// which shows none.
    /// </summary>
    public string LastSeenVersion { get; init; } = string.Empty;

    /// <summary>The Store copy asked once for a rating; it does not ask again.</summary>
    public bool ReviewAsked { get; init; }

    /// <summary>Tell when a limit passes its warning or critical level or resets.</summary>
    public bool NotifyOnLimits { get; init; } = true;

    /// <summary>The first-run steps were finished or closed; they never show again.</summary>
    public bool OnboardingDone { get; init; }

    /// <summary>Accounts whose limits are not asked for: their sign-in is left alone.</summary>
    public IReadOnlyList<string> LimitsOffAccounts { get; init; } = [];

    /// <summary>Accounts the user has answered "show limits?" for, either way.</summary>
    public IReadOnlyList<string> LimitsAskedAccounts { get; init; } = [];

    /// <summary>
    /// Whether Hakari may read this account's sign-in to ask for its limits: not when turned
    /// off, and for an account never read before only once the user said yes. Accounts read
    /// before this choice existed carry on as they were.
    /// </summary>
    public bool MayReadLimits(string accountId, bool readBefore) =>
        !LimitsOffAccounts.Contains(accountId)
        && (readBefore || LimitsAskedAccounts.Contains(accountId));

    /// <summary>The account answered: limits on, or estimates only.</summary>
    public HakariSettings WithLimitsChoice(string accountId, bool on) => this with
    {
        LimitsAskedAccounts = [.. LimitsAskedAccounts.Append(accountId).Distinct()],
        LimitsOffAccounts = on
            ? [.. LimitsOffAccounts.Where(id => id != accountId)]
            : [.. LimitsOffAccounts.Append(accountId).Distinct()],
    };

    /// <summary>The note that the meter moved to the tray, told once and never again.</summary>
    public bool TrayFallbackTold { get; init; }

    /// <summary>Limit alerts stay quiet until then, as "mute today" asks.</summary>
    public DateTimeOffset? AlertsMutedUntil { get; init; }

    public bool AlertsMutedAt(DateTimeOffset now) => AlertsMutedUntil > now;

    /// <summary>What a day may cost, in the shown currency; null for no budget.</summary>
    public decimal? DailyBudget { get; init; }

    /// <summary>What a month may cost, in the shown currency; null for no budget.</summary>
    public decimal? MonthlyBudget { get; init; }

    /// <summary>
    /// What a project may cost per month, in the shown currency, keyed by its folder as the
    /// dashboard lists it (with joined folders counted in).
    /// </summary>
    public IReadOnlyDictionary<string, decimal> ProjectBudgets { get; init; } =
        new Dictionary<string, decimal>();

    /// <summary>Tell when one session costs far more than sessions usually do.</summary>
    public bool NotifyOnUnusualSessions { get; init; } = true;

    /// <summary>Tell when a limit resets, apart from the warnings as it fills.</summary>
    public bool NotifyOnResets { get; init; } = true;

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
        && CountThinking == other.CountThinking
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
        && DailyBudget == other.DailyBudget
        && MonthlyBudget == other.MonthlyBudget
        && SameEntries(ProjectBudgets, other.ProjectBudgets)
        && SameEntries(ProjectMerges, other.ProjectMerges)
        && NotifyOnUnusualSessions == other.NotifyOnUnusualSessions
        && NotifyOnResets == other.NotifyOnResets
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

    private static bool SameEntries<TValue>(
        IReadOnlyDictionary<string, TValue> left,
        IReadOnlyDictionary<string, TValue> right) =>
        left.Count == right.Count
        && left.All(entry => right.TryGetValue(entry.Key, out var value)
            && EqualityComparer<TValue>.Default.Equals(value, entry.Value));

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
