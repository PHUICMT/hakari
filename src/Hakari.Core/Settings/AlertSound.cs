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

    /// <summary>A struck bell that rings on.</summary>
    Bell,

    /// <summary>Three quick wooden notes going up.</summary>
    Marimba,

    /// <summary>A short rising drop, like water.</summary>
    Droplet,

    /// <summary>Four fast bright notes going up.</summary>
    Sparkle,

    /// <summary>Ding-dong, two notes going down.</summary>
    Doorbell,

    /// <summary>Two soft low knocks.</summary>
    Knock,

    /// <summary>A sound file the user picked; Hakari keeps its own copy.</summary>
    Custom,

    /// <summary>The alert shows without any sound.</summary>
    None,
}
