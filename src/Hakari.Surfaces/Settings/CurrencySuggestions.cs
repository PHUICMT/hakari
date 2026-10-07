using Hakari.Core.Currency;
using Hakari.Surfaces.Motion;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace Hakari.Surfaces.Settings;

/// <summary>
/// A list under the currency field that fills in as the user types a code, a currency's name
/// or a country, in English or the app's language: each row says the code, what it is, where
/// it is used and its symbol. Arrow keys move, Enter or a click picks, Escape closes.
/// </summary>
internal sealed class CurrencySuggestions
{
    private const double ListWidth = 340;
    private const double DropDistance = 6;
    private const double CodeWidth = 44;
    private const double CodeSize = 13;
    private const double DetailSize = 11;
    private static readonly Thickness RowPadding = new(10, 6, 10, 6);

    private readonly TextBox box;
    private readonly Action<string> chosen;
    private readonly Popup popup = new();
    private readonly StackPanel rows = new() { Spacing = 2 };
    private IReadOnlyList<CurrencyEntry> shown = [];
    private int highlighted = -1;
    private bool picking;

    private CurrencySuggestions(TextBox box, Action<string> chosen)
    {
        this.box = box;
        this.chosen = chosen;
        popup.Child = new Border
        {
            Width = ListWidth,
            Padding = new Thickness(4),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = Brush("HakariLineStrongBrush"),
            Background = Brush("HakariTileBrush"),
            Child = rows,
        };
        popup.DesiredPlacement = PopupPlacementMode.BottomEdgeAlignedLeft;
        popup.PlacementTarget = box;
        box.TextChanged += (_, _) => Fill();
        box.PreviewKeyDown += OnKey;
        box.LostFocus += (_, _) => popup.IsOpen = false;
    }

    public static void Attach(TextBox box, Action<string> chosen) =>
        _ = new CurrencySuggestions(box, chosen);

    private void Fill()
    {
        if (picking || box.FocusState == FocusState.Unfocused)
        {
            popup.IsOpen = false;
            return;
        }

        shown = CurrencyCatalog.Search(box.Text);
        highlighted = shown.Count > 0 ? 0 : -1;
        rows.Children.Clear();
        for (var index = 0; index < shown.Count; index++)
        {
            rows.Children.Add(Row(shown[index], index));
        }

        Paint();
        var open = shown.Count > 0;
        if (open && !popup.IsOpen)
        {
            popup.XamlRoot = box.XamlRoot;
            popup.IsOpen = true;
            SurfaceMotion.Enter(popup.Child, fromY: -DropDistance);
        }
        else if (!open)
        {
            popup.IsOpen = false;
        }
    }

    private Grid Row(CurrencyEntry entry, int index)
    {
        var row = new Grid
        {
            Padding = RowPadding,
            ColumnSpacing = 10,
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
        };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(CodeWidth) });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(new TextBlock
        {
            Text = entry.Code,
            FontSize = CodeSize,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("HakariInkBrush"),
            VerticalAlignment = VerticalAlignment.Center,
        });
        var text = new StackPanel();
        text.Children.Add(new TextBlock
        {
            Text = entry.Name,
            FontSize = CodeSize,
            Foreground = Brush("HakariInkBrush"),
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        text.Children.Add(new TextBlock
        {
            Text = string.Join(", ", entry.Places),
            FontSize = DetailSize,
            Foreground = Brush("HakariInkFaintBrush"),
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        Grid.SetColumn(text, 1);
        row.Children.Add(text);
        var symbol = new TextBlock
        {
            Text = entry.Symbol,
            FontSize = CodeSize,
            Foreground = Brush("HakariInkMutedBrush"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(symbol, 2);
        row.Children.Add(symbol);
        row.PointerEntered += (_, _) =>
        {
            highlighted = index;
            Paint();
        };
        row.PointerPressed += (_, args) =>
        {
            args.Handled = true;
            Pick(entry);
        };
        ToolTipService.SetToolTip(row, string.Join(", ", entry.Places));
        return row;
    }

    private void OnKey(object sender, KeyRoutedEventArgs args)
    {
        if (!popup.IsOpen || shown.Count == 0)
        {
            return;
        }

        switch (args.Key)
        {
            case VirtualKey.Down:
                highlighted = (highlighted + 1) % shown.Count;
                Paint();
                args.Handled = true;
                break;
            case VirtualKey.Up:
                highlighted = (highlighted - 1 + shown.Count) % shown.Count;
                Paint();
                args.Handled = true;
                break;
            case VirtualKey.Enter when highlighted >= 0:
                Pick(shown[highlighted]);
                args.Handled = true;
                break;
            case VirtualKey.Escape:
                popup.IsOpen = false;
                args.Handled = true;
                break;
        }
    }

    private void Pick(CurrencyEntry entry)
    {
        popup.IsOpen = false;
        picking = true;
        box.Text = entry.Code;
        box.SelectionStart = box.Text.Length;
        picking = false;
        chosen(entry.Code);
    }

    private void Paint()
    {
        for (var index = 0; index < rows.Children.Count; index++)
        {
            ((Grid)rows.Children[index]).Background = index == highlighted
                ? Brush("HakariHoverBrush")
                : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        }
    }

    private static Brush Brush(string key) => (Brush)Application.Current.Resources[key];
}
