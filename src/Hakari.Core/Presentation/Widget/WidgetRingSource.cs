namespace Hakari.Core.Presentation.Widget;

/// <summary>Which limit fills the ring left of the text.</summary>
public enum WidgetRingSource
{
    Off,
    Session,
    Weekly,
    MostPressing,

    /// <summary>Two rings: the weekly limit outside, the 5-hour one inside.</summary>
    SessionAndWeekly,
}
