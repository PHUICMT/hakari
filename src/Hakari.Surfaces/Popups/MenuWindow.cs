using Hakari.Core.Interprocess;
using Hakari.Core.Localization;
using Hakari.Surfaces.Flyout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace Hakari.Surfaces.Popups;

/// <summary>
/// The widget's right-click menu in Hakari's own style. It closes on a choice, on Esc, or
/// when anything else is clicked. Actions for Hakari.exe go back over its command pipe.
/// </summary>
public sealed partial class MenuWindow : PopupWindow
{
    private const double MenuWidth = 240;
    private const double MenuRise = 12;
    private const double IconSize = 14;
    private const double IconColumn = 20;
    private const double ItemSpacing = 10;
    private const double DividerHeight = 1;
    private static readonly Thickness ItemPadding = new(10, 6, 10, 6);
    private static readonly Thickness DividerMargin = new(6, 4, 6, 4);

    private readonly Action openSettings;
    private readonly Action openDashboard;

    public MenuWindow(Action openSettings, Action openDashboard)
        : base(MenuWidth)
    {
        this.openSettings = openSettings;
        this.openDashboard = openDashboard;
        Activated += (_, args) =>
        {
            if (args.WindowActivationState == WindowActivationState.Deactivated && IsShowing)
            {
                HidePopup();
            }
        };
    }

    protected override double ContentPadding => 4;

    public void ShowAt(int anchorX, int anchorY)
    {
        var items = new StackPanel();
        items.Children.Add(Item("", "menu.dashboard", openDashboard));
        items.Children.Add(Item("", "menu.refresh", () => Send(ResidentCommand.RefreshNow)));
        items.Children.Add(Item("", "menu.changeLayout", openSettings));
        items.Children.Add(Item("", "menu.pauseHour", () => Send(ResidentCommand.PauseForAnHour)));
        items.Children.Add(Divider());
        items.Children.Add(Item("", "menu.settingsPlain", openSettings));
        items.Children.Add(Item("", "menu.quit", () => Send(ResidentCommand.Quit)));
        AddEscape(items);
        Body = items;
        ShowAbove(anchorX, anchorY, activate: true, AnchorSide.Start);
        Motion.SurfaceMotion.Enter(items, fromY: MenuRise);
    }

    private static void Send(ResidentCommand command) =>
        Task.Run(() => ResidentChannel.TrySend(command));

    private Button Item(string glyph, string textKey, Action action)
    {
        var row = new Grid { ColumnSpacing = ItemSpacing };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(IconColumn) });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.Children.Add(new FontIcon
        {
            Glyph = glyph,
            FontSize = IconSize,
            FontFamily = (FontFamily)Application.Current.Resources["HakariIconFont"],
            Foreground = Brush("HakariInkMutedBrush"),
        });
        var label = new TextBlock
        {
            Text = Texts.Get(textKey),
            Foreground = Brush("HakariInkBrush"),
        };
        Grid.SetColumn(label, 1);
        row.Children.Add(label);

        var button = new Button
        {
            Content = row,
            Style = (Style)Application.Current.Resources["HakariCardHeaderButton"],
            Padding = ItemPadding,
        };
        button.Click += (_, _) =>
        {
            HidePopup();
            action();
        };
        return button;
    }

    private static Border Divider() => new()
    {
        Height = DividerHeight,
        Margin = DividerMargin,
        Background = Brush("HakariLineBrush"),
    };

    private void AddEscape(UIElement items)
    {
        var escape = new KeyboardAccelerator { Key = Windows.System.VirtualKey.Escape };
        escape.Invoked += (_, args) =>
        {
            args.Handled = true;
            HidePopup();
        };
        items.KeyboardAccelerators.Add(escape);
        items.KeyboardAcceleratorPlacementMode = KeyboardAcceleratorPlacementMode.Hidden;
    }
}
