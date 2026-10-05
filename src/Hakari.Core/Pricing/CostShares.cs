using Hakari.Core.Querying;

namespace Hakari.Core.Pricing;

/// <summary>
/// How a period's cost splits by token type: input, output, both cache writes and cache
/// reads. Each model's tokens are priced at that model's current price, and the result is
/// given as shares, which a total can then be multiplied by.
/// </summary>
public static class CostShares
{
    private const decimal TokensPerPriceUnit = 1_000_000m;

    public static IReadOnlyList<CostShare> Of(
        IEnumerable<UsageSummary> byModel,
        PricingTable pricing,
        DateOnly day)
    {
        var totals = new Dictionary<TokenKind, decimal>();
        foreach (var summary in byModel)
        {
            if (pricing.Find(summary.Key, day) is not { } price)
            {
                continue;
            }

            var tokens = summary.Tokens;
            Add(totals, TokenKind.CacheRead, tokens.CacheRead * price.CacheRead);
            Add(
                totals,
                TokenKind.CacheWriteOneHour,
                tokens.CacheWriteOneHour * price.CacheWriteOneHour);
            Add(totals, TokenKind.Output, tokens.Output * price.Output);
            Add(
                totals,
                TokenKind.CacheWriteFiveMinutes,
                tokens.CacheWriteFiveMinutes * price.CacheWriteFiveMinutes);
            Add(totals, TokenKind.Input, tokens.Input * price.Input);
        }

        var sum = totals.Values.Sum();
        return sum <= 0
            ? []
            : [.. totals
                .Select(entry => new CostShare(entry.Key, (double)(entry.Value / sum)))
                .OrderByDescending(share => share.Share)];
    }

    private static void Add(Dictionary<TokenKind, decimal> totals, TokenKind kind, decimal units) =>
        totals[kind] = totals.GetValueOrDefault(kind) + units / TokensPerPriceUnit;
}
