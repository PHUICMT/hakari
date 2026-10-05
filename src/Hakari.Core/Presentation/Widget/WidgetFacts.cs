using Hakari.Core.Limits;

namespace Hakari.Core.Presentation.Widget;

/// <summary>Everything the widget can show, gathered once per update.</summary>
/// <param name="Accounts">Accounts with known limits, in the order they are shown.</param>
/// <param name="FullAt">When a limit fills at the recent pace; null when unknown.</param>
/// <param name="Nicknames">Names the user gave accounts, keyed by account id.</param>
/// <param name="TokensToday">Every token today: input, output and cache.</param>
public sealed record WidgetFacts(
    decimal CostToday,
    decimal CostThisMonth,
    decimal CostLastHour,
    string Currency,
    IReadOnlyList<WidgetAccount> Accounts,
    Func<string, UsageLimit, DateTimeOffset, DateTimeOffset?>? FullAt = null,
    IReadOnlyDictionary<string, string>? Nicknames = null,
    long TokensToday = 0,
    long TokensThisMonth = 0,
    long RepliesToday = 0,
    int PercentDecimals = 0)
{
    /// <summary>"93%", or "93.4%" with one decimal; the decimals are an estimate.</summary>
    public string FormatPercent(double percent) => PercentText.Format(percent, PercentDecimals);

    public string? NicknameOf(string accountId) =>
        Nicknames is not null && Nicknames.TryGetValue(accountId, out var name) ? name : null;
}
