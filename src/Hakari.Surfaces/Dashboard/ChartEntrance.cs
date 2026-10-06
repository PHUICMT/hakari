using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Animation;

namespace Hakari.Surfaces.Dashboard;

/// <summary>
/// A chart grows in once, the first time it appears. Coming back to the page attaches it again
/// without replaying, and a page updated quietly in place builds its charts already grown.
/// </summary>
internal static class ChartEntrance
{
    /// <summary>True while a page rebuilds in place; charts built then skip growing in.</summary>
    public static bool IsQuiet { get; private set; }

    public static bool ShouldPlay => !IsQuiet;

    public static void PlayOnce(FrameworkElement chart, Storyboard storyboard)
    {
        if (IsQuiet)
        {
            return;
        }

        void Begin(object sender, RoutedEventArgs args)
        {
            chart.Loaded -= Begin;
            storyboard.Begin();
        }

        chart.Loaded += Begin;
    }

    public static T Quietly<T>(Func<T> build)
    {
        IsQuiet = true;
        try
        {
            return build();
        }
        finally
        {
            IsQuiet = false;
        }
    }
}
