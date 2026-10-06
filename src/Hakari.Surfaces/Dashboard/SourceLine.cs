namespace Hakari.Surfaces.Dashboard;

/// <summary>One place usage is read from, as a row of the sources table.</summary>
/// <param name="Status">"Live" while it was used a moment ago, else when it last was.</param>
internal sealed record SourceLine(
    string Name,
    string Account,
    string Status,
    bool IsLive,
    long Files,
    long Bytes,
    string AllTime);
