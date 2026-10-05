namespace Hakari.Core.Interprocess;

/// <summary>What the window process asks of Hakari.exe, such as from the widget's menu.</summary>
public enum ResidentCommand
{
    RefreshNow,
    PauseForAnHour,
    Quit,
}
