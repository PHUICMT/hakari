namespace Hakari.Core.Presentation.Widget;

/// <param name="Fraction">0 to 1. At 1 the ring is drawn as a full "stop" disc.</param>
/// <param name="InnerFraction">A second, smaller ring inside, or null for one ring.</param>
public sealed record ComposedRing(
    double Fraction,
    LineTone Tone,
    double? InnerFraction = null,
    LineTone InnerTone = LineTone.Normal);
