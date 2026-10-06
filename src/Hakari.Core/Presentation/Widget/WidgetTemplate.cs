namespace Hakari.Core.Presentation.Widget;

/// <summary>The shape of the widget. Each template reads the layout's slots its own way.</summary>
public enum WidgetTemplate
{
    /// <summary>Money on top, the limit and when it resets below.</summary>
    TwoLines,

    /// <summary>One metric, the smallest footprint.</summary>
    SingleLine,

    /// <summary>Up to four labelled values side by side.</summary>
    Columns,

    /// <summary>Limits as thin bars with the value beside each.</summary>
    Bars,

    /// <summary>A limit ring with money and burn rate.</summary>
    RingText,

    /// <summary>The last 12 hours of spend, with today's total.</summary>
    Sparkline,

    /// <summary>One value per account.</summary>
    Accounts,

    /// <summary>A ring and one number, for when space runs out.</summary>
    Minimal,
}
