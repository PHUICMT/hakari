using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Hakari.Surfaces.Dashboard;

/// <summary>The dashboard's card: a title, a caption on the right, then content.</summary>
internal static class DashboardCard
{
    private const double TitleSize = 14;
    private const double CaptionSize = 12;
    private const double HeaderGap = 14;
    private static readonly Thickness CardPadding = new(16, 14, 16, 16);

    public static Border Create(string title, string? caption, UIElement content)
    {
        var header = new Grid { Margin = new Thickness(0, 0, 0, HeaderGap) };
        header.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = TitleSize,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("HakariInkBrush"),
        });
        if (caption is not null)
        {
            header.Children.Add(new TextBlock
            {
                Text = caption,
                FontSize = CaptionSize,
                Foreground = Brush("HakariInkFaintBrush"),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
            });
        }

        var body = new StackPanel();
        body.Children.Add(header);
        body.Children.Add(content);
        return new Border
        {
            Style = (Style)Application.Current.Resources["HakariCard"],
            Padding = CardPadding,
            Child = body,
        };
    }

    public static Brush Brush(string key) => (Brush)Application.Current.Resources[key];
}
