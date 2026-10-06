namespace Hakari.Core.Interprocess;

/// <summary>What the window process asks of Hakari.exe, such as from the widget's menu.</summary>
public enum ResidentCommand
{
    RefreshNow,
    PauseForAnHour,
    Quit,

    /// <summary>A notification's "Open Hakari": the flyout, at the widget.</summary>
    OpenFlyout,

    /// <summary>A notification's "Mute today": no more limit alerts until midnight.</summary>
    MuteAlertsToday,
}
