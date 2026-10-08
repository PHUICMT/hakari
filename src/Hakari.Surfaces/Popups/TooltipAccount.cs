using Hakari.Surfaces.Flyout;

namespace Hakari.Surfaces.Popups;

/// <summary>
/// One account in the hover card: its name and plan, a row per limit, money lines, and how
/// fresh the limits are. A note such as "paused" is a title with nothing else.
/// </summary>
public sealed record TooltipAccount(
    string Title,
    IReadOnlyList<string> Lines,
    string Updated,
    string Plan = "",
    IReadOnlyList<TooltipLimit>? Limits = null);

/// <summary>A limit as the hover card draws it: name, meter, value and when it resets.</summary>
/// <param name="Fraction">0 to 1, for the small meter.</param>
/// <param name="Reset">"Resets 12:50", or empty when it has no reset yet.</param>
/// <param name="FullAt">"Full around 06:57" when the recent pace fills it first.</param>
public sealed record TooltipLimit(
    string Name,
    string Value,
    double Fraction,
    Tone Tone,
    string Reset,
    string FullAt);
