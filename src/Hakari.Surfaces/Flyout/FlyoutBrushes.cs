using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Hakari.Surfaces.Flyout;

/// <summary>Theme brushes by meaning, for x:Bind function bindings in the flyout.</summary>
public static class FlyoutBrushes
{
    private const string AccentKey = "HakariAccentBrush";
    private const string WarnKey = "HakariWarnBrush";
    private const string CriticalKey = "HakariCriticalBrush";
    private const string FaintKey = "HakariInkFaintBrush";
    private const string OkKey = "HakariOkBrush";
    private const string InkKey = "HakariInkBrush";

    public static Brush ForTone(Tone tone) => Lookup(tone switch
    {
        Tone.Warning => WarnKey,
        Tone.Critical => CriticalKey,
        Tone.Muted => FaintKey,
        _ => AccentKey,
    });

    /// <summary>Values read in ink; only a full limit turns critical, as in the design.</summary>
    public static Brush ForValue(Tone tone) =>
        Lookup(tone == Tone.Critical ? CriticalKey : InkKey);

    public static Brush ForSource(bool isRecent) => Lookup(isRecent ? OkKey : FaintKey);

    private static Brush Lookup(string key) => (Brush)Application.Current.Resources[key];
}
