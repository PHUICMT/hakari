namespace Hakari.Core.Limits;

public sealed record LimitSample(DateTimeOffset At, int Percent, DateTimeOffset? ResetsAt);
