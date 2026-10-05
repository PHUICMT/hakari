namespace Hakari.Core.Limits;

/// <param name="Kind">For example "session" (5 hours), "weekly_all", "weekly_scoped".</param>
/// <param name="Group">"session" or "weekly".</param>
/// <param name="Severity">"normal", "warning", or a stronger level as reported.</param>
/// <param name="ScopeName">The model a scoped limit applies to, such as "Fable".</param>
public sealed record UsageLimit(
    string Kind,
    string Group,
    int Percent,
    string Severity,
    DateTimeOffset? ResetsAt,
    string? ScopeName,
    bool IsActive);
