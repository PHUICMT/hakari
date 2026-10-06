using Hakari.Core.Settings;
using Hakari.Surfaces.Motion;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using ListFlyout = Microsoft.UI.Xaml.Controls.Flyout;

namespace Hakari.Surfaces.Controls;

/// <summary>
/// A drop-down choice in Hakari's own style: a button showing the current choice, opening a
/// list where the selected entry carries a check mark.
/// </summary>
public sealed partial class HakariSelect : Button
{
    private const string ChevronGlyph = "";
    private const string CheckGlyph = "";
    private const double GlyphSize = 10;
    private const double CheckSize = 12;
    private const double ContentSpacing = 10;
    private const double ListMinimumWidth = 180;
    private const double SampleGap = 24;

    private readonly TextBlock label = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly List<(object Value, string Text)> choices = [];

    public HakariSelect()
    {
        Style = (Style)Application.Current.Resources["HakariButton"];
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = ContentSpacing };
        row.Children.Add(label);
        row.Children.Add(new FontIcon
        {
            Glyph = ChevronGlyph,
            FontSize = GlyphSize,
            FontFamily = (FontFamily)Application.Current.Resources["HakariIconFont"],
            Foreground = Brush("HakariInkMutedBrush"),
        });
        Content = row;
        Click += (_, _) => OpenList();
    }

    public event EventHandler<object>? Selected;

    public object? SelectedValue { get; private set; }

    /// <summary>
    /// What a choice looks like, drawn at the end of its row in the list, such as the value it
    /// would show; null for none.
    /// </summary>
    public Func<object, FrameworkElement?>? Sample { get; set; }

    public void SetChoices(IEnumerable<(object Value, string Text)> newChoices, object selected)
    {
        choices.Clear();
        choices.AddRange(newChoices);
        Select(selected, notify: false);
    }

    private void Select(object value, bool notify)
    {
        SelectedValue = value;
        label.Text = choices.FirstOrDefault(choice => Equals(choice.Value, value)).Text ?? "";
        if (notify)
        {
            Selected?.Invoke(this, value);
        }
    }

    private void OpenList()
    {
        var list = new StackPanel { MinWidth = ListMinimumWidth };
        var flyout = new ListFlyout
        {
            Content = list,
            Placement = FlyoutPlacementMode.Bottom,
            FlyoutPresenterStyle = (Style)Application.Current.Resources["HakariListPresenter"],
            AreOpenCloseAnimationsEnabled = SurfaceMotion.Current() != AnimationSetting.Off,
        };

        foreach (var (value, text) in choices)
        {
            list.Children.Add(ChoiceButton(value, text, flyout));
        }

        flyout.ShowAt(this);
    }

    private Button ChoiceButton(object value, string text, ListFlyout flyout)
    {
        var isSelected = Equals(value, SelectedValue);
        var row = new Grid { ColumnSpacing = ContentSpacing };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(CheckSize) });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        if (Sample?.Invoke(value) is { } sample)
        {
            sample.Margin = new Thickness(SampleGap, 0, 0, 0);
            sample.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(sample, 2);
            row.Children.Add(sample);
        }

        row.Children.Add(new FontIcon
        {
            Glyph = CheckGlyph,
            FontSize = CheckSize,
            FontFamily = (FontFamily)Application.Current.Resources["HakariIconFont"],
            Foreground = Brush("HakariAccentBrush"),
            Opacity = isSelected ? 1 : 0,
        });
        var textBlock = new TextBlock
        {
            Text = text,
            Foreground = Brush(isSelected ? "HakariInkBrush" : "HakariInkMutedBrush"),
        };
        Grid.SetColumn(textBlock, 1);
        row.Children.Add(textBlock);

        // With samples the row spans the list, so every sample lines up at the right edge.
        var button = new Button
        {
            Content = row,
            Style = (Style)Application.Current.Resources["HakariListItemButton"],
            HorizontalContentAlignment = Sample is null
                ? HorizontalAlignment.Left
                : HorizontalAlignment.Stretch,
        };
        button.Click += (_, _) =>
        {
            flyout.Hide();
            Select(value, notify: true);
        };
        return button;
    }

    private static Brush Brush(string key) => (Brush)Application.Current.Resources[key];
}
