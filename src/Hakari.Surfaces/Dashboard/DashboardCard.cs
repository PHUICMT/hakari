using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Hakari.Surfaces.Dashboard;

/// <summary>
/// The dashboard's card: a chevron and title, a caption on the right, then content that
/// folds away under the header when clicked.
/// </summary>
internal static class DashboardCard
{
    private const double TitleSize = 14;
    private const double CaptionSize = 12;
    private const double HeaderGap = 14;
    private const double CaptionGap = 120;
    private static readonly Thickness CardPadding = new(16, 14, 16, 16);

    public static Border Create(string title, string? caption, UIElement content)
    {
        var header = new Grid
        {
            ColumnSpacing = 4,
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
        };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition());
        var titleText = new TextBlock
        {
            Text = title,
            FontSize = TitleSize,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("HakariInkBrush"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(titleText, 1);
        header.Children.Add(titleText);
        if (caption is not null)
        {
            header.Children.Add(new TextBlock
            {
                Text = caption,
                FontSize = CaptionSize,
                Foreground = Brush("HakariInkFaintBrush"),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(CaptionGap, 0, 0, 0),
            });
            Grid.SetColumn((FrameworkElement)header.Children[^1], 1);
        }

        // The gap under the header folds away with the content.
        var foldable = new ContentControl
        {
            Content = content,
            Margin = new Thickness(0, HeaderGap, 0, 0),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            IsTabStop = false,
        };
        header.Children.Add(DashboardFold.Attach(header, foldable, title));
        var body = new StackPanel();
        body.Children.Add(header);
        body.Children.Add(foldable);
        return new Border
        {
            Style = (Style)Application.Current.Resources["HakariCard"],
            Padding = CardPadding,
            Child = body,
        };
    }

    public static Brush Brush(string key) => (Brush)Application.Current.Resources[key];
}
