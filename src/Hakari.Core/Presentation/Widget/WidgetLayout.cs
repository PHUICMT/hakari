namespace Hakari.Core.Presentation.Widget;

/// <summary>
/// The user's taskbar layout. With no slots it is the original two lines and a ring: by
/// default the ring follows the most pressing limit, which is the one that blocks work (a full
/// weekly limit outweighs an empty 5-hour one). With slots, the template decides how they are
/// read, and a custom format overrides what the lines say.
/// </summary>
public sealed record WidgetLayout
{
    public const int MaximumSlots = 4;
    public const int DefaultWarnAt = 80;
    public const int DefaultCriticalAt = 95;

    public WidgetRingSource Ring { get; init; } = WidgetRingSource.MostPressing;

    public WidgetItem Top { get; init; } = WidgetItem.Automatic;

    public WidgetItem Bottom { get; init; } = WidgetItem.Automatic;

    public WidgetTemplate Template { get; init; } = WidgetTemplate.TwoLines;

    /// <summary>Empty keeps the Top, Bottom and Ring above.</summary>
    public IReadOnlyList<WidgetSlot> Slots { get; init; } = [];

    /// <summary>For example "{cost.today:$0.00} · {limit.5h:0%}"; replaces the text.</summary>
    public string? CustomFormat { get; init; }

    /// <summary>The percent from which a limit is drawn in the warning color.</summary>
    public int WarnAt { get; init; } = DefaultWarnAt;

    /// <summary>The percent from which a limit is drawn in the critical color.</summary>
    public int CriticalAt { get; init; } = DefaultCriticalAt;

    /// <summary>Seconds each slot is shown in turn in one-line layouts; 0 for none.</summary>
    public int CycleSeconds { get; init; }

    public bool UsesSlots => Slots.Count > 0;

    public bool Equals(WidgetLayout? other) =>
        other is not null
        && Ring == other.Ring
        && Top == other.Top
        && Bottom == other.Bottom
        && Template == other.Template
        && Slots.SequenceEqual(other.Slots)
        && CustomFormat == other.CustomFormat
        && WarnAt == other.WarnAt
        && CriticalAt == other.CriticalAt
        && CycleSeconds == other.CycleSeconds;

    public override int GetHashCode() =>
        HashCode.Combine(Ring, Top, Bottom, Template, Slots.Count, CustomFormat, CycleSeconds);
}
