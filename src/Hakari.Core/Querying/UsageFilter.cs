namespace Hakari.Core.Querying;

public sealed record UsageFilter(
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    IReadOnlyCollection<string>? SourceIds = null,
    string? Model = null,
    string? Project = null)
{
    public static UsageFilter Everything { get; } = new();
}
