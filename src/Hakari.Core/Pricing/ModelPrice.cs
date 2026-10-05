namespace Hakari.Core.Pricing;

/// <summary>
/// Prices per million tokens for one model, valid from <see cref="EffectiveFrom"/> (UTC day)
/// until the next price entry of the same model starts. A null start means "since launch".
/// </summary>
public sealed record ModelPrice(
    decimal Input,
    decimal Output,
    decimal CacheWriteFiveMinutes,
    decimal CacheWriteOneHour,
    decimal CacheRead,
    FastModePrice? Fast = null,
    DateOnly? EffectiveFrom = null);
