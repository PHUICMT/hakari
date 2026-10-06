namespace Hakari.Core.Settings;

/// <summary>
/// How the dashboard was left, so it opens the same way: the page, the filter and the
/// window's size in layout units (0 for the default).
/// </summary>
public sealed record DashboardMemory(
    string Page = "",
    string Period = "",
    string? AccountId = null,
    string? SourceId = null,
    string? Model = null,
    double Width = 0,
    double Height = 0);