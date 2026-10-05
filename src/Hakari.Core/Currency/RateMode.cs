namespace Hakari.Core.Currency;

public enum RateMode
{
    /// <summary>Convert every amount with the most recent rate.</summary>
    Latest,

    /// <summary>Convert each day's amount with the rate of that day.</summary>
    UsageDay,
}
