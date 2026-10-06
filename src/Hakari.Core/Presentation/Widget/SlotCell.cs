namespace Hakari.Core.Presentation.Widget;

/// <summary>A metric as a short value over a short label, as in a column.</summary>
/// <param name="Fraction">For a limit, how full it is from 0 to 1; otherwise null.</param>
public sealed record SlotCell(string Value, string Label, LineTone Tone, double? Fraction = null);
