namespace Hakari.Core.Limits;

/// <summary>How full a limit was at one moment, as kept in the history.</summary>
public sealed record LimitReading(DateTimeOffset At, double Percent);
