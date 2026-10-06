using Hakari.Core.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Hakari.Surfaces.Dashboard;

/// <summary>
/// A dashboard page that answers the shared filter: it reads its data off the UI thread,
/// shows a skeleton the first time, and reads again when the filter changes. A page only says
/// what to read and how to draw it.
/// </summary>
internal abstract partial class LoadedPage<T> : UserControl
{
    private readonly DashboardFilterBar filterBar = new();
    private readonly PageLoader<T> loader;

    protected LoadedPage(string titleKey)
    {
        var body = new ContentControl
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            IsTabStop = false,
        };
        loader = new PageLoader<T>(
            body,
            filter => Read(filter),
            data => Build(data),
            () => Skeleton());
        Content = DashboardPageFrame.Create(Texts.Get(titleKey), filterBar, body);
        filterBar.Changed += (_, _) => loader.Load(force: true);
        Loaded += (_, _) => loader.Load();
    }

    /// <summary>Runs off the UI thread.</summary>
    protected abstract T Read(DashboardFilter filter);

    protected abstract UIElement Build(T data);

    protected virtual UIElement Skeleton() => LoadingSkeleton.Table();
}
