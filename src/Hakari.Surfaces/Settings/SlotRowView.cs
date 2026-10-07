using Hakari.Core.Localization;
using Hakari.Core.Presentation.Widget;
using Hakari.Surfaces.Controls;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Hakari.Surfaces.Settings;

/// <summary>
/// One slot of the layout: its number, what it shows, how it is drawn, and buttons to move or
/// remove it.
/// </summary>
internal sealed partial class SlotRowView : UserControl
{
    private const double NumberWidth = 80;
    private const double StyleWidth = 128;
    private const double HiddenOpacity = 0.55;
    private const double IconSize = 11;
    private const double RowSpacing = 8;
    private const string UpGlyph = "\uE70E";
    private const string DownGlyph = "\uE70D";
    private const string RemoveGlyph = "\uE711";
    private static readonly Thickness RowPadding = new(12, 6, 6, 6);

    /// <param name="isShown">False when the template has no room for it; it is dimmed then.</param>
    /// <param name="metricSample">What each metric would show now, for the metric list.</param>
    /// <param name="styles">The styles this slot may take, given what the others use.</param>
    public SlotRowView(
        int number,
        WidgetSlot slot,
        bool canMoveUp,
        bool canMoveDown,
        bool canRemove,
        bool isShown,
        Func<object, FrameworkElement?> metricSample,
        IEnumerable<(object Value, string Text)> styles)
    {
        var metric = new HakariSelect
        {
            Sample = metricSample,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        metric.SetChoices(WidgetItemChoices.ForSlotStyle(slot.Style), slot.Item);
        metric.Selected += (_, value) => ItemChanged?.Invoke(this, (WidgetItem)value);
        var style = new HakariSelect
        {
            Sample = StyleSamples.Of,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        style.SetChoices(styles, slot.Style);
        style.Selected += (_, value) => StyleChanged?.Invoke(this, (WidgetSlotStyle)value);

        var row = new Grid { ColumnSpacing = RowSpacing };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(NumberWidth) });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(StyleWidth) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var name = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        name.Children.Add(new TextBlock
        {
            Text = Texts.Format("settings.slot", number),
            FontWeight = FontWeights.SemiBold,
            FontSize = 13,
            Foreground = Brush("HakariInkBrush"),
        });
        if (!isShown)
        {
            name.Children.Add(new TextBlock
            {
                Text = Texts.Get("settings.slot.hidden"),
                FontSize = 11,
                Foreground = Brush("HakariInkFaintBrush"),
                TextWrapping = TextWrapping.Wrap,
            });
        }

        row.Children.Add(name);
        Place(row, metric, 1);
        Place(row, style, 2);
        Place(row, Buttons(canMoveUp, canMoveDown, canRemove), 3);

        Content = new Border
        {
            Child = row,
            Padding = RowPadding,
            BorderThickness = new Thickness(1),
            BorderBrush = Brush("HakariLineBrush"),
            Background = Brush("HakariTileBrush"),
            CornerRadius = new CornerRadius(6),
        };
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        Opacity = isShown ? 1 : HiddenOpacity;
        ToolTipService.SetToolTip(this, isShown ? null : Texts.Get("settings.slot.hidden.why"));
    }

    public event EventHandler<WidgetItem>? ItemChanged;

    public event EventHandler<WidgetSlotStyle>? StyleChanged;

    public event EventHandler? MovedUp;

    public event EventHandler? MovedDown;

    public event EventHandler? Removed;

    private StackPanel Buttons(bool canMoveUp, bool canMoveDown, bool canRemove)
    {
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
        buttons.Children.Add(Icon(UpGlyph, "settings.slot.moveUp", canMoveUp, RaiseUp));
        buttons.Children.Add(Icon(DownGlyph, "settings.slot.moveDown", canMoveDown, RaiseDown));
        buttons.Children.Add(Icon(RemoveGlyph, "settings.slot.remove", canRemove, RaiseRemoved));
        return buttons;
    }

    private void RaiseUp() => MovedUp?.Invoke(this, EventArgs.Empty);

    private void RaiseDown() => MovedDown?.Invoke(this, EventArgs.Empty);

    private void RaiseRemoved() => Removed?.Invoke(this, EventArgs.Empty);

    private static Button Icon(string glyph, string tooltipKey, bool enabled, Action click)
    {
        var button = new Button
        {
            Style = (Style)Application.Current.Resources["HakariSubtleButton"],
            IsEnabled = enabled,
            Padding = new Thickness(8, 6, 8, 6),
            Content = new FontIcon
            {
                Glyph = glyph,
                FontSize = IconSize,
                FontFamily = (FontFamily)Application.Current.Resources["HakariIconFont"],
            },
        };
        Accessible.Name(button, Texts.Get(tooltipKey));
        button.Click += (_, _) => click();
        return button;
    }

    private static void Place(Grid row, FrameworkElement element, int column)
    {
        Grid.SetColumn(element, column);
        row.Children.Add(element);
    }

    private static Brush Brush(string key) => (Brush)Application.Current.Resources[key];
}
