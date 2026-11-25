namespace Hakari.Core.Settings;

/// <summary>What plays when a limit or spending alert shows.</summary>
public enum AlertSound
{
    /// <summary>The notification sound Windows plays, at Windows' own volume.</summary>
    Windows,

    /// <summary>Two soft rising notes.</summary>
    Chime,

    /// <summary>One low, rounded note.</summary>
    Soft,

    /// <summary>One short high note.</summary>
    Ping,

    /// <summary>A sound file the user picked; Hakari keeps its own copy.</summary>
    Custom,

    /// <summary>The alert shows without any sound.</summary>
    None,
}
