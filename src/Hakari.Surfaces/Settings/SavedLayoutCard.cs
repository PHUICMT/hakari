using Hakari.Core.Localization;
using Hakari.Core.Presentation.Widget;
using Hakari.Surfaces.Controls;
using Hakari.Surfaces.Motion;
using Microsoft.UI.Input;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Hakari.Surfaces.Settings;

/// <summary>
/// One of the user's saved layouts: its name, the template it is built on, the widget it
/// makes, and a button to forget it. Clicking the card puts the layout back.
/// </summary>
internal sealed partial class SavedLayoutCard : UserControl
{
    private const double TitleSize = 13;
    private const double DetailSize = 12;
    private const string RemoveGlyph = "";
    private static readonly Thickness CardPadding = new(12, 10, 8, 10);

    private readonly Border frame = new();
    private readonly StackPanel preview = new()
    {
        Orientation = Orientation.Horizontal,
        Spacing = 4,
    };
    private bool isChosen;
    private bool isOver;

    public SavedLayoutCard(NamedLayout saved)
    {
        Saved = saved;
        var top = new Grid { ColumnSpacing = 6 };
        top.ColumnDefinitions.Add(new ColumnDefinition());
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var text = new StackPanel { Spacing = 2 };
        text.Children.Add(new TextBlock
        {
            Text = saved.Name,
            FontSize = TitleSize,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("HakariInkBrush"),
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        var template = saved.Layout.Template.ToString().ToLowerInvariant();
        var templateKey = $"settings.template.{template}";
        text.Children.Add(new TextBlock
        {
            Text = Texts.Get(templateKey),
            FontSize = DetailSize,
            Foreground = Brush("HakariInkFaintBrush"),
        });
        top.Children.Add(text);
        var remove = RemoveButton();
        Grid.SetColumn(remove, 1);
        top.Children.Add(remove);

        var body = new StackPanel { Spacing = 8 };
        body.Children.Add(top);
        body.Children.Add(new Viewbox
        {
            Child = preview,
            StretchDirection = StretchDirection.DownOnly,
            HorizontalAlignment = HorizontalAlignment.Left,
        });
        frame.Child = body;
        frame.Padding = CardPadding;
        frame.BorderThickness = new Thickness(1);
        frame.CornerRadius = new CornerRadius(6);
        frame.BackgroundTransition = new BrushTransition { Duration = SurfaceMotion.Normal };
        Content = frame;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
        Paint();
        PointerEntered += (_, _) => Over(true);
        PointerExited += (_, _) => Over(false);
        Tapped += (_, args) =>
        {
            if (!args.Handled)
            {
                Chosen?.Invoke(this, Saved);
            }
        };
    }

    public NamedLayout Saved { get; }

    public event EventHandler<NamedLayout>? Chosen;

    public event EventHandler<NamedLayout>? Removed;

    public void ShowPreview(IReadOnlyList<ComposedWidget> panels)
    {
        preview.Children.Clear();
        foreach (var panel in panels)
        {
            var block = new WidgetPreview();
            block.Show(panel);
            preview.Children.Add(block);
        }
    }

    public void Choose(bool chosen)
    {
        isChosen = chosen;
        Paint();
    }

    private Button RemoveButton()
    {
        var button = new Button
        {
            Style = (Style)Application.Current.Resources["HakariSubtleButton"],
            Padding = new Thickness(6, 4, 6, 4),
            VerticalAlignment = VerticalAlignment.Top,
            Content = new FontIcon
            {
                Glyph = RemoveGlyph,
                FontSize = 10,
                FontFamily = (FontFamily)Application.Current.Resources["HakariIconFont"],
            },
        };
        Accessible.Name(button, Texts.Get("settings.layout.forget"));
        button.Tapped += (_, args) => args.Handled = true;
        button.Click += (_, _) => AskBeforeRemoving(button);
        return button;
    }

    /// <summary>Forgetting cannot be undone, so it asks first, next to the button.</summary>
    private void AskBeforeRemoving(FrameworkElement anchor)
    {
        var question = new Microsoft.UI.Xaml.Controls.Flyout
        {
            Placement = Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.Bottom,
            FlyoutPresenterStyle = (Style)Application.Current.Resources["HakariListPresenter"],
        };
        SurfaceMotion.EnterOnOpen(question);
        var forget = new Button
        {
            Content = Texts.Get("settings.layout.forget.confirm"),
            Style = (Style)Application.Current.Resources["HakariPrimaryButton"],
            Background = Brush("HakariCriticalBrush"),
            BorderBrush = Brush("HakariCriticalBrush"),
        };
        forget.Click += (_, _) =>
        {
            question.Hide();
            Removed?.Invoke(this, Saved);
        };
        var keep = new Button
        {
            Content = Texts.Get("settings.layout.forget.cancel"),
            Style = (Style)Application.Current.Resources["HakariButton"],
        };
        keep.Click += (_, _) => question.Hide();
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        buttons.Children.Add(keep);
        buttons.Children.Add(forget);
        var body = new StackPanel { Spacing = 10, Padding = new Thickness(12), MaxWidth = 280 };
        body.Children.Add(new TextBlock
        {
            Text = Texts.Format("settings.layout.forget.question", Saved.Name),
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brush("HakariInkBrush"),
        });
        body.Children.Add(buttons);
        question.Content = body;
        question.ShowAt(anchor);
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
            : isOver ? Brush("HakariRaisedBrush") : Brush("HakariTileBrush");
    }

    private static Brush Brush(string key) => (Brush)Application.Current.Resources[key];
}
