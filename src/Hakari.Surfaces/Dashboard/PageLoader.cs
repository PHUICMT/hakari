using Hakari.Core.Settings;
using Hakari.Surfaces.Motion;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace Hakari.Surfaces.Dashboard;

/// <summary>
/// Reads a page's data off the UI thread and swaps it in when ready, so switching pages never
/// waits on the database. The first read shows skeleton cards; when data arrives its sections
/// rise in one after another. Coming back to a page shows what it had at once; it reads again
/// only when the filter changed, the result is a minute old, or refresh was pressed.
/// </summary>
internal sealed class PageLoader<T>(
    ContentControl body,
    Func<DashboardFilter, T> read,
    Func<T, UIElement> build)
{
    private const double LoadingOpacity = 0.55;
    private const double RiseDistance = 12;
    private static readonly TimeSpan FreshFor = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan SectionStagger = TimeSpan.FromMilliseconds(60);

    private DashboardFilter? loadedFilter;
    private DateTimeOffset loadedAt = DateTimeOffset.MinValue;
    private int generation;
    private bool hasData;

    public async void Load(bool force = false)
    {
        var filter = DashboardFilter.Current;
        var fresh = DateTimeOffset.UtcNow - loadedAt < FreshFor;
        if (!force && filter == loadedFilter && fresh)
        {
            return;
        }

        var mine = ++generation;
        if (hasData)
        {
            SurfaceMotion.Settle(body, "Opacity", LoadingOpacity);
        }
        else
        {
            body.Content = LoadingSkeleton.Create();
        }

        var data = await Task.Run(() => read(filter));

        // A newer request started while this one read; its result wins.
        if (mine != generation)
        {
            return;
        }

        var view = build(data);
        body.Content = view;
        body.Opacity = 1;
        Reveal(view);
        hasData = true;
        loadedFilter = filter;
        loadedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Each section of the page rises in shortly after the one above it.</summary>
    private static void Reveal(UIElement view)
    {
        var motion = SurfaceMotion.Current();
        if (motion == AnimationSetting.Off)
        {
            return;
        }

        var sections = view is Panel panel ? [.. panel.Children] : new List<UIElement> { view };
        var storyboard = new Storyboard();
        for (var index = 0; index < sections.Count; index++)
        {
            var section = sections[index];
            section.Opacity = 0;
            var delay = SectionStagger * index;
            if (motion == AnimationSetting.Full)
            {
                var offset = new TranslateTransform { Y = RiseDistance };
                section.RenderTransform = offset;
                storyboard.Children.Add(SurfaceMotion.Animate(
                    offset, "Y", RiseDistance, 0, SurfaceMotion.Entrance, delay));
            }

            var duration = motion == AnimationSetting.Full
                ? SurfaceMotion.Entrance
                : SurfaceMotion.ReducedFade;
            storyboard.Children.Add(
                SurfaceMotion.Animate(section, "Opacity", 0, 1, duration, delay));
        }

        storyboard.Begin();
    }
}
