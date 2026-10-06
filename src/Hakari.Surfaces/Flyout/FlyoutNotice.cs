using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Hakari.Surfaces.Flyout;

/// <summary>What a notice's button does.</summary>
public enum NoticeAction
{
    None,

    /// <summary>Opens Settings, where prices can be checked or set.</summary>
    OpenPrices,
}

/// <summary>
/// A bar at the top of the flyout that says something needs attention: a mark and a title
/// in the notice's tone, a line of detail, and sometimes a button.
/// </summary>
public sealed record FlyoutNotice(
    Tone Tone,
    string Title,
    string Detail = "",
    NoticeAction Action = NoticeAction.None,
    string ActionText = "")
{
    private const string InfoGlyph = "\uE946";
    private const string WarningGlyph = "\uE7BA";

    public string Glyph => Tone == Tone.Normal ? InfoGlyph : WarningGlyph;

    public Brush ToneBrush => FlyoutBrushes.ForTone(Tone);

    public Brush Ground => (Brush)Application.Current.Resources[Tone switch
    {
        Tone.Critical => "HakariCriticalSoftBrush",
        Tone.Warning => "HakariWarnSoftBrush",
        _ => "HakariAccentSoftBrush",
    }];

    public Visibility DetailVisibility =>
        Detail.Length == 0 ? Visibility.Collapsed : Visibility.Visible;

    public Visibility ActionVisibility =>
        Action == NoticeAction.None ? Visibility.Collapsed : Visibility.Visible;
}