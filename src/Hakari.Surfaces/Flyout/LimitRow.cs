namespace Hakari.Surfaces.Flyout;

/// <param name="Fraction">0 to 1, for the meter.</param>
public sealed record LimitRow(
    string Name,
    string Value,
    string ResetText,
    double Fraction,
    Tone Tone);
