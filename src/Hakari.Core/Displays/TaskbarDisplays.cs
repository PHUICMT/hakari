namespace Hakari.Core.Displays;

public enum TaskbarDisplays
{
    /// <summary>Only the main display's taskbar.</summary>
    Primary,

    /// <summary>Every taskbar.</summary>
    All,

    /// <summary>The displays the user picked; the main one when none is connected.</summary>
    Chosen,
}
