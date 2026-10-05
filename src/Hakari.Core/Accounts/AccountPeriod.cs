namespace Hakari.Core.Accounts;

/// <summary>From <see cref="StartedAt"/> on, the source's usage belongs to this account.</summary>
public sealed record AccountPeriod(
    string SourceId,
    string AccountId,
    DateTimeOffset StartedAt);
