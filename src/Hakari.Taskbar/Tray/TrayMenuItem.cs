namespace Hakari.Taskbar.Tray;

/// <summary>One entry of the tray and widget menu. A null action marks a separator.</summary>
public sealed record TrayMenuItem(string Label, Action? OnSelect, bool IsChecked = false)
{
    public static TrayMenuItem Separator { get; } = new(string.Empty, null);

    public bool IsSeparator => OnSelect is null;
}
