namespace Hakari.Core.Settings;

public enum AccountOrder
{
    /// <summary>The account closest to a limit first, so trouble is always on top.</summary>
    MostPressing,

    /// <summary>The order the user arranged in Settings.</summary>
    Custom,
}
