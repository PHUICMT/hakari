using Hakari.Core.Localization;
using Hakari.Core.Presentation.Widget;
using Hakari.Surfaces.Controls;
using Hakari.Surfaces.Motion;
using Microsoft.UI.Input;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Hakari.Surfaces.Settings;

/// <summary>
/// One template in the picker: its name and what it is for. The chosen one is outlined in
/// the accent color; the others lift a little under the pointer, and the editor's preview
/// shows the widget in that template while the pointer rests on it.
/// </summary>
internal sealed partial class TemplateCard : UserControl
{
    private const double TitleSize = 13;
    private const double DescriptionSize = 11;
    private const double ChosenBorder = 2;
    private const double PlainBorder = 1;
    private static readonly Thickness CardPadding = new(10, 8, 10, 8);

    /// <summary>The thicker outline takes its pixel from the padding; nothing moves.</summary>
    private static readonly Thickness ChosenPadding = new(9, 7, 9, 7);

    private readonly Border frame = new();
    private bool isChosen;
    private bool isOver;

    public TemplateCard(WidgetTemplate template)
    {
        Kind = template;
        var key = $"settings.template.{template.ToString().ToLowerInvariant()}";
        var body = new StackPanel { Spacing = 2 };
        body.Children.Add(new TextBlock
        {
            Text = Texts.Get(key),
            FontSize = TitleSize,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("HakariInkBrush"),
        });
        body.Children.Add(new TextBlock
        {
            Text = Texts.Get($"{key}.description"),
            FontSize = DescriptionSize,
            Foreground = Brush("HakariInkFaintBrush"),
            TextWrapping = TextWrapping.Wrap,
        });
        frame.Child = body;
        frame.Padding = CardPadding;
        frame.CornerRadius = new CornerRadius(6);
        frame.BackgroundTransition = new BrushTransition { Duration = SurfaceMotion.Normal };
        Content = frame;
        VerticalAlignment = VerticalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
        Paint();
        PointerEntered += (_, _) => Over(true);
        PointerExited += (_, _) => Over(false);
        PointerCanceled += (_, _) => Over(false);
        Tapped += (_, _) => Chosen?.Invoke(this, Kind);
    }

    public WidgetTemplate Kind { get; }

    public event EventHandler<WidgetTemplate>? Chosen;

    /// <summary>The pointer came to rest on this template (true) or left it (false).</summary>
    public event EventHandler<bool>? Hovered;

    public void Choose(bool chosen)
    {
        if (isChosen == chosen)
        {
            return;
        }

        isChosen = chosen;
        Paint();
    }

    private void Over(bool over)
    {
        if (isOver == over)
        {
            return;
        }

        isOver = over;
        Paint();
        Hovered?.Invoke(this, over);
    }

    private void Paint()
    {
        frame.BorderBrush = Brush(isChosen ? "HakariAccentBrush" : "HakariLineBrush");
        frame.BorderThickness = new Thickness(isChosen ? ChosenBorder : PlainBorder);
        frame.Padding = isChosen ? ChosenPadding : CardPadding;
        frame.Background = isOver && !isChosen
            ? Brush("HakariRaisedBrush")
            : Brush("HakariTileBrush");
    }

    private static Microsoft.UI.Xaml.Media.Brush Brush(string key) =>
        (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[key];
}
