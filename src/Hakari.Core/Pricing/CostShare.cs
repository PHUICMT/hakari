namespace Hakari.Core.Pricing;

/// <param name="Share">0 to 1 of the period's cost.</param>
public sealed record CostShare(TokenKind Kind, double Share);
