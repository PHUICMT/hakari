namespace Hakari.Core.Pricing;

/// <summary>
/// Prices per million tokens for one model, valid from <see cref="EffectiveFrom"/> (UTC day)
/// until the next price entry of the same model starts. A null start means "since launch".
/// </summary>
/// <param name="LongPrompt">Higher prices for a request whose prompt is longer than its
/// threshold, for models priced that way; null for one price at every length.</param>
public sealed record ModelPrice(
    decimal Input,
    decimal Output,
    decimal CacheWriteFiveMinutes,
    decimal CacheWriteOneHour,
    decimal CacheRead,
    FastModePrice? Fast = null,
    DateOnly? EffectiveFrom = null,
    LongPromptPrice? LongPrompt = null)
{
    /// <summary>
    /// The prices for a request whose prompt is over <paramref name="promptOver"/> tokens,
    /// as <see cref="PromptSize"/> sorts them.
    /// </summary>
    public ModelPrice ForPrompt(long promptOver) =>
        LongPrompt is { } longer && promptOver >= longer.Above
            ? this with
            {
                Input = longer.Input,
                Output = longer.Output,
                CacheWriteFiveMinutes = longer.CacheWriteFiveMinutes,
                CacheWriteOneHour = longer.CacheWriteOneHour,
                CacheRead = longer.CacheRead,
                LongPrompt = null,
            }
            : this;
}

/// <summary>Prices once a prompt, cache writes and reads included, passes the threshold.</summary>
/// <param name="Above">One of <see cref="PromptSize.Thresholds"/>, such as 100000.</param>
public sealed record LongPromptPrice(
    long Above,
    decimal Input,
    decimal Output,
    decimal CacheWriteFiveMinutes,
    decimal CacheWriteOneHour,
    decimal CacheRead);
