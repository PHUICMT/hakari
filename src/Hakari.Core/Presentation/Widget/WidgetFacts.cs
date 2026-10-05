using Hakari.Core.Limits;

namespace Hakari.Core.Presentation.Widget;

/// <summary>Everything the widget can show, gathered once per update.</summary>
/// <param name="Accounts">Accounts with known limits, the most pressing first.</param>
/// <param name="FullAt">When a limit fills at the recent pace; null when unknown.</param>
/// <param name="Nicknames">Names the user gave accounts, keyed by account id.</param>
public sealed record WidgetFacts(
    decimal CostToday,
    decimal CostThisMonth,
    decimal CostLastHour,
    string Currency,
    IReadOnlyList<WidgetAccount> Accounts,
    Func<string, UsageLimit, DateTimeOffset, DateTimeOffset?>? FullAt = null,
    IReadOnlyDictionary<string, string>? Nicknames = null)
{
    public string? NicknameOf(string accountId) =>
        Nicknames is not null && Nicknames.TryGetValue(accountId, out var name) ? name : null;
}
