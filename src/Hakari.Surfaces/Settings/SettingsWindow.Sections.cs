using Hakari.Surfaces.Flyout;
using Hakari.Surfaces.Motion;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Hakari.Surfaces.Settings;

/// <summary>
/// Each section's title folds its card away, like the flyout's account cards. Which
/// sections are folded is remembered.
/// </summary>
public sealed partial class SettingsWindow
{
    private const string ChevronGlyph = "";
    private const double ChevronSize = 11;
    private const double FoldedSectionAngle = -90;
    private const string AnglePath = "Angle";
    private static readonly Thickness SectionHeaderMargin = new(0, 8, 0, 4);
    private static readonly Thickness SectionHeaderPadding = new(4, 6, 8, 6);

    private void MakeSectionsFoldable()
    {
        var folded = store.Load().CollapsedSettingsSections;
        foreach (var section in Sections.Children.OfType<StackPanel>())
        {
            if (section is not { Tag: string key, Children.Count: >= 2 }
                || section.Children[0] is not TextBlock title
                || section.Children[1] is not FrameworkElement card)
            {
                continue;
            }

            var isFolded = folded.Contains(key);
            var chevron = Chevron(isFolded);
            var header = SectionHeader(title.Text, chevron);
            header.Click += (_, _) => ToggleSection(key, card, chevron);
            section.Children[0] = header;
            card.Visibility = isFolded ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    private static Button SectionHeader(string title, FontIcon chevron)
    {
        var row = new Grid { ColumnSpacing = 8 };
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var text = new TextBlock
        {
            Text = title,
            Style = (Style)Application.Current.Resources["HakariSectionText"],
            Margin = new Thickness(0),
        };
        row.Children.Add(text);
        Grid.SetColumn(chevron, 1);
        row.Children.Add(chevron);
        return new Button
        {
            Content = row,
            Style = (Style)Application.Current.Resources["HakariCardHeaderButton"],
            Margin = SectionHeaderMargin,
            Padding = SectionHeaderPadding,
        };
    }

    private static FontIcon Chevron(bool isFolded) => new()
    {
        Glyph = ChevronGlyph,
        FontSize = ChevronSize,
        FontFamily = (FontFamily)Application.Current.Resources["HakariIconFont"],
        Foreground = (Brush)Application.Current.Resources["HakariInkMutedBrush"],
        VerticalAlignment = VerticalAlignment.Center,
        RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5),
        RenderTransform = new RotateTransform { Angle = isFolded ? FoldedSectionAngle : 0 },
    };

    private void ToggleSection(string key, FrameworkElement card, FontIcon chevron)
    {
        var folding = card.Visibility == Visibility.Visible;
        CardFold.Run(card, folding, fitWindow: () => { });
        if (chevron.RenderTransform is RotateTransform turn)
        {
            SurfaceMotion.Settle(turn, AnglePath, folding ? FoldedSectionAngle : 0);
        }

        store.Update(current => current with
        {
            CollapsedSettingsSections = folding
                ? [.. current.CollapsedSettingsSections.Append(key).Distinct()]
                : [.. current.CollapsedSettingsSections.Where(section => section != key)],
        });
    }
}
