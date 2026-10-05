using Microsoft.UI.Xaml;

namespace Hakari.Surfaces.Dashboard;

/// <summary>
/// Runs a layout change whenever an element crosses a width, so the dashboard rearranges
/// itself in a narrow window instead of cutting things off. Only crossings do work.
/// </summary>
internal static class WidthSteps
{
    /// <param name="steps">Widths, smallest first; the callback gets how many are exceeded.</param>
    public static void Watch(FrameworkElement element, double[] steps, Action<int> apply)
    {
        var current = -1;
        element.SizeChanged += (_, args) =>
        {
            var reached = steps.Count(step => args.NewSize.Width >= step);
            if (reached != current)
            {
                current = reached;
                apply(reached);
            }
        };
    }
}
