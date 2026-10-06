namespace Hakari.Core.Presentation.Widget;

/// <summary>How a slot's metric is drawn.</summary>
public enum WidgetSlotStyle
{
    Text,

    /// <summary>A meter left of the text; for the limits.</summary>
    Ring,

    /// <summary>The last 12 hours of spending as a small line.</summary>
    Sparkline,
}
