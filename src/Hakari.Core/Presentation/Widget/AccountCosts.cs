namespace Hakari.Core.Presentation.Widget;

/// <summary>One account's own usage, for a block of its own.</summary>
public sealed record AccountCosts(
    decimal Today,
    decimal ThisMonth,
    decimal LastHour,
    long TokensToday = 0,
    long TokensThisMonth = 0,
    long RepliesToday = 0);
