namespace Hakari.Core.Parsing;

/// <summary>What one log line turned out to be, to notice when the log format changes.</summary>
public enum LineKind
{
    /// <summary>Not a response, or one left out on purpose such as an error reply.</summary>
    Other,

    /// <summary>A response whose usage was read.</summary>
    Usage,

    /// <summary>A response whose usage read as all zeros.</summary>
    Empty,

    /// <summary>A response line whose fields could not be read.</summary>
    Unreadable,

    /// <summary>Mentions a response and usage, but not in the shape Hakari looks for.</summary>
    Missed,
}
