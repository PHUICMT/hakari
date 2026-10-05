namespace Hakari.Core.Usage;

public readonly record struct TokenCounts(
    long Input,
    long Output,
    long CacheWriteFiveMinutes,
    long CacheWriteOneHour,
    long CacheRead)
{
    public long CacheWrite => CacheWriteFiveMinutes + CacheWriteOneHour;

    public long TotalInput => Input + CacheWrite + CacheRead;

    public double CacheHitRate => TotalInput == 0 ? 0 : (double)CacheRead / TotalInput;

    public static TokenCounts operator +(TokenCounts left, TokenCounts right) => new(
        Input: left.Input + right.Input,
        Output: left.Output + right.Output,
        CacheWriteFiveMinutes: left.CacheWriteFiveMinutes + right.CacheWriteFiveMinutes,
        CacheWriteOneHour: left.CacheWriteOneHour + right.CacheWriteOneHour,
        CacheRead: left.CacheRead + right.CacheRead);
}
