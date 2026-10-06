using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Hakari.Surfaces.Dashboard;

/// <summary>What every dashboard page shares: a title, the filter bar, then the page.</summary>
internal static class DashboardPageFrame
{
    private const double PageTitleSize = 26;
    private const double SectionSpacing = 16;
    private static readonly Thickness PagePadding = new(24, 20, 24, 28);

    public static ScrollViewer Create(string title, DashboardFilterBar filterBar, UIElement body)
    {
        var header = new Grid();
        header.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = PageTitleSize,
            FontWeight = FontWeights.SemiBold,
            Foreground = DashboardCard.Brush("HakariInkBrush"),
            VerticalAlignment = VerticalAlignment.Center,
        });
        header.Children.Add(filterBar);
        DashboardHeader.WrapWhenNarrow(header, filterBar);

        var content = new StackPanel { Spacing = SectionSpacing, Padding = PagePadding };
        content.Children.Add(header);
        content.Children.Add(body);
        return new ScrollViewer { Content = content };
    }
}
