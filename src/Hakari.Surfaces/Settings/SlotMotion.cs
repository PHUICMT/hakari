namespace Hakari.Surfaces.Settings;

/// <summary>What just happened to the slot list, so the rebuilt rows can show it moving.</summary>
internal enum SlotMotion
{
    None,
    Added,
    MovedUp,
    MovedDown,
    Changed,
}
