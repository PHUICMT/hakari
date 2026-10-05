namespace Hakari.Core.Displays;

/// <param name="Id">The monitor's device path: stays the same across restarts and reorders.</param>
/// <param name="DeviceName">The current GDI name, such as \\.\DISPLAY2; can change.</param>
/// <param name="Name">The monitor's own name, such as "DELL U2720Q", or "Display 2".</param>
public sealed record DisplayInfo(string Id, string DeviceName, string Name, bool IsPrimary);
