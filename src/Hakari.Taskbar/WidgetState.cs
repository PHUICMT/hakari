using Hakari.Taskbar.Motion;
using Hakari.Taskbar.Rendering;

namespace Hakari.Taskbar;

/// <summary>
/// Per-widget drawing state. The palette is sampled from the taskbar only on full renders, so
/// animation frames do no screen reads.
/// </summary>
internal sealed class WidgetState(WidgetContent initialContent)
{
    public WidgetAnimation Animation { get; } = new(initialContent);

    public WidgetPalette Palette { get; set; } = WidgetPalette.DarkTaskbar;

    /// <summary>Showing the compact form because the full one had no room on its taskbar.</summary>
    public bool IsCompact { get; set; }
}
