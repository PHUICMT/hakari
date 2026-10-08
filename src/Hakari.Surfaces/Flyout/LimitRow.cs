using Microsoft.UI.Xaml;

namespace Hakari.Surfaces.Flyout;

/// <param name="Fraction">0 to 1, for the meter.</param>
/// <param name="Pace">
/// 0 to 1, where an even pace through a weekly limit would be by now; null for none.
/// </param>
/// <param name="PaceText">Says that mark in words, such as "Even pace 71%".</param>
/// <param name="FullAtText">When the recent pace fills it before it resets; empty if not.</param>
public sealed record LimitRow(
    string Name,
    string Value,
    string ResetText,
    double Fraction,
    Tone Tone,
    double? Pace = null,
    string PaceText = "",
    string FullAtText = "")
{
    public Visibility FullAtVisibility =>
        FullAtText.Length == 0 ? Visibility.Collapsed : Visibility.Visible;

    public Visibility PaceVisibility => Pace is null ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>The meter's share left of the mark, as a column width.</summary>
    public GridLength PaceBefore => new(Pace ?? 0, GridUnitType.Star);

    public GridLength PaceAfter => new(1 - (Pace ?? 0), GridUnitType.Star);
}
