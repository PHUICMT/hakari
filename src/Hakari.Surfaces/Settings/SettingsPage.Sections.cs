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
public sealed partial class SettingsPage
{
    private const string ChevronGlyph = "";
    private const double ChevronSize = 11;
    private const double FoldedSectionAngle = -90;
    private const string AnglePath = "Angle";
    private const double SectionTitleSize = 17;
    private const double MarkWidth = 3;
    private const double MarkHeight = 18;
    private static readonly Thickness SectionHeaderMargin = new(0, 12, 0, 8);
    private static readonly Thickness DividerMargin = new(0, 28, 0, 4);
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
            if (!ReferenceEquals(section, Sections.Children.OfType<StackPanel>().First()))
            {
                section.Children.Insert(0, Divider());
            }
            card.Visibility = isFolded ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    /// <summary>
    /// A section's title: an accent mark, the name in a larger weight, and the fold chevron,
    /// with room above it so each section reads as its own group.
    /// </summary>
    private static Button SectionHeader(string title, FontIcon chevron)
    {
        var row = new Grid { ColumnSpacing = 10 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(new Border
        {
            Width = MarkWidth,
            Height = MarkHeight,
            CornerRadius = new CornerRadius(MarkWidth / 2),
            Background = (Brush)Application.Current.Resources["HakariAccentBrush"],
            VerticalAlignment = VerticalAlignment.Center,
        });
        var text = new TextBlock
        {
            Text = title,
            FontSize = SectionTitleSize,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = (Brush)Application.Current.Resources["HakariInkBrush"],
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(text, 1);
        row.Children.Add(text);
        Grid.SetColumn(chevron, 2);
        row.Children.Add(chevron);
        return new Button
        {
            Content = row,
            Style = (Style)Application.Current.Resources["HakariCardHeaderButton"],
            Margin = SectionHeaderMargin,
            Padding = SectionHeaderPadding,
        };
    }

    /// <summary>A hairline between two sections.</summary>
    private static Border Divider() => new()
    {
        Height = 1,
        Margin = DividerMargin,
        Background = (Brush)Application.Current.Resources["HakariLineBrush"],
    };

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
