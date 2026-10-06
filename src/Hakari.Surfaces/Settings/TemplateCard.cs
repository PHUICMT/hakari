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
/// One template to pick: its name and what it is for. The chosen one is outlined in the
/// accent color on a soft accent ground; the others lift a little under the pointer.
/// </summary>
internal sealed partial class TemplateCard : UserControl
{
    private const double TitleSize = 13;
    private const double DescriptionSize = 12;
    private static readonly Thickness CardPadding = new(12, 10, 12, 10);

    private readonly Border frame = new();
    private readonly StackPanel preview = new()
    {
        Orientation = Orientation.Horizontal,
        Spacing = 4,
    };
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
        body.Children.Add(new Viewbox
        {
            Child = preview,
            StretchDirection = StretchDirection.DownOnly,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 8, 0, 0),
        });
        frame.Child = body;
        frame.Padding = CardPadding;
        frame.BorderThickness = new Thickness(1);
        frame.CornerRadius = new CornerRadius(6);
        Content = frame;
        VerticalAlignment = VerticalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
        Paint();
        PointerEntered += (_, _) => Over(true);
        PointerExited += (_, _) => Over(false);
        Tapped += (_, _) => Chosen?.Invoke(this, Kind);
    }

    public WidgetTemplate Kind { get; }

    public event EventHandler<WidgetTemplate>? Chosen;

    /// <summary>What the widget looks like with this template, from the real numbers.</summary>
    public void ShowPreview(IReadOnlyList<ComposedWidget> panels)
    {
        while (preview.Children.Count > panels.Count)
        {
            preview.Children.RemoveAt(preview.Children.Count - 1);
        }

        for (var index = 0; index < panels.Count; index++)
        {
            if (index >= preview.Children.Count)
            {
                preview.Children.Add(new WidgetPreview());
            }

            ((WidgetPreview)preview.Children[index]).Show(panels[index]);
        }
    }

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
        isOver = over;
        Paint();
    }

    private void Paint()
    {
        frame.BorderBrush = Brush(isChosen ? "HakariAccentBrush" : "HakariLineBrush");
        frame.Background = isChosen
            ? Brush("HakariAccentSoftBrush")
            : isOver ? Brush("HakariHoverBrush") : Brush("HakariTileBrush");
    }

    private static Microsoft.UI.Xaml.Media.Brush Brush(string key) =>
        (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[key];
}
