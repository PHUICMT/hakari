namespace Hakari.Core.Pricing;

public sealed record ModelPrice(
    decimal Input,
    decimal Output,
    decimal CacheWriteFiveMinutes,
    decimal CacheWriteOneHour,
    decimal CacheRead,
    FastModePrice? Fast = null);
