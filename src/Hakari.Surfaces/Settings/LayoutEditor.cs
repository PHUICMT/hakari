using Hakari.Core.Localization;
using Hakari.Core.Presentation.Widget;
using Hakari.Surfaces.Controls;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Hakari.Surfaces.Settings;

/// <summary>
/// Builds the taskbar widget from templates and slots: pick a template, then change what each
/// slot shows and how it is drawn, reorder, add or remove slots, write a custom format, set
/// where limits turn warning and critical, and cycle through the slots. Every change goes to
/// the layout at once and shows in the preview.
/// </summary>
internal sealed partial class LayoutEditor : StackPanel
{
    private const double Gutter = 16;
    private const double Gap = 8;
    private const double TitleSize = 14;
    private const double HintSize = 12;
    private const double DotSize = 9;
    private const int FormatDebounceMilliseconds = 300;
    private static readonly int[] WarnChoices = [50, 60, 70, 75, 80, 85, 90];
    private static readonly int[] CriticalChoices = [80, 85, 90, 95, 98, 100];
    private static readonly int[] CycleChoices = [4, 8, 15, 30];
    private const int DefaultCycleSeconds = 8;

    private readonly Func<WidgetLayout> currentLayout;
    private readonly Action<Func<WidgetLayout, WidgetLayout>> change;
    private readonly StackPanel previewRow = new()
    {
        Orientation = Orientation.Horizontal,
        Spacing = 6,
    };
    private readonly List<TemplateCard> templateCards = [];
    private readonly StackPanel slotList = new() { Spacing = Gap };
    private readonly Button addSlot = new();
    private readonly TextBox formatBox = new();
    private readonly HakariSelect warnSelect = new();
    private readonly HakariSelect criticalSelect = new();
    private readonly HakariToggle cycleToggle = new();
    private readonly HakariSelect cycleSelect = new();
    private readonly SettingRow simpleRow = new();
    private readonly DispatcherQueueTimer formatTimer;
    private bool refreshing;

    public LayoutEditor(
        Func<WidgetLayout> currentLayout,
        Action<Func<WidgetLayout, WidgetLayout>> change)
    {
        this.currentLayout = currentLayout;
        this.change = change;
        formatTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        formatTimer.Interval = TimeSpan.FromMilliseconds(FormatDebounceMilliseconds);
        formatTimer.IsRepeating = false;
        formatTimer.Tick += (_, _) => ApplyFormat();

        Children.Add(Header());
        Children.Add(PreviewStrip());
        Children.Add(Templates());
        Children.Add(Slots());
        Children.Add(FormatBlock());
        Children.Add(Thresholds());
        Children.Add(Cycle());
        Children.Add(SimpleRow());
    }

    /// <summary>Shows the layout's state in every control, without changing it.</summary>
    public void Refresh(WidgetLayout layout, IReadOnlyList<ComposedWidget> panels)
    {
        refreshing = true;
        try
        {
            ShowPreview(panels);
            foreach (var card in templateCards)
            {
                card.Choose(layout.UsesSlots && card.Kind == layout.Template);
            }

            RebuildSlots(layout);
            if (formatBox.FocusState == FocusState.Unfocused)
            {
                formatBox.Text = layout.CustomFormat ?? string.Empty;
            }

            warnSelect.SetChoices(
                WarnChoices.Select(percent => ((object)percent, $"{percent}%")),
                layout.WarnAt);
            criticalSelect.SetChoices(
                CriticalChoices.Select(percent => ((object)percent, $"{percent}%")),
                layout.CriticalAt);
            cycleToggle.IsChecked = layout.CycleSeconds > 0;
            cycleSelect.Visibility = layout.CycleSeconds > 0
                ? Visibility.Visible
                : Visibility.Collapsed;
            cycleSelect.SetChoices(
                CycleChoices.Select(seconds => ((object)seconds, $"{seconds} s")),
                layout.CycleSeconds > 0 ? layout.CycleSeconds : DefaultCycleSeconds);
            simpleRow.Visibility = layout.UsesSlots ? Visibility.Visible : Visibility.Collapsed;
        }
        finally
        {
            refreshing = false;
        }
    }

    private static StackPanel Header()
    {
        var header = new StackPanel
        {
            Padding = new Thickness(Gutter, Gutter, Gutter, 4),
            Spacing = 2,
        };
        header.Children.Add(new TextBlock
        {
            Text = Texts.Get("settings.layout.title"),
            FontSize = TitleSize,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("HakariInkBrush"),
        });
        header.Children.Add(new TextBlock
        {
            Text = Texts.Get("settings.layout.description"),
            FontSize = HintSize,
            Foreground = Brush("HakariInkFaintBrush"),
            TextWrapping = TextWrapping.Wrap,
        });
        return header;
    }

    private Border PreviewStrip() => new()
    {
        Margin = new Thickness(Gutter, Gap, Gutter, 0),
        Padding = new Thickness(Gutter),
        CornerRadius = new CornerRadius(6),
        Background = Brush("HakariGroundBrush"),
        Child = previewRow,
    };

    private void ShowPreview(IReadOnlyList<ComposedWidget> panels)
    {
        while (previewRow.Children.Count > panels.Count)
        {
            previewRow.Children.RemoveAt(previewRow.Children.Count - 1);
        }

        for (var index = 0; index < panels.Count; index++)
        {
            if (index >= previewRow.Children.Count)
            {
                previewRow.Children.Add(new WidgetPreview());
            }

            ((WidgetPreview)previewRow.Children[index]).Show(panels[index]);
        }
    }

    private Grid Templates()
    {
        var grid = new Grid
        {
            Margin = new Thickness(Gutter, Gutter, Gutter, 0),
            ColumnSpacing = Gap,
            RowSpacing = Gap,
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        var templates = WidgetTemplates.All;
        for (var index = 0; index < templates.Count; index++)
        {
            if (index % 2 == 0)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            var card = new TemplateCard(templates[index]);
            card.Chosen += (_, template) => change(layout => Pick(layout, template));
            Grid.SetColumn(card, index % 2);
            Grid.SetRow(card, index / 2);
            grid.Children.Add(card);
            templateCards.Add(card);
        }

        return grid;
    }

    /// <summary>The accounts template cycles one account's value at a time.</summary>
    private static WidgetLayout Pick(WidgetLayout layout, WidgetTemplate template) =>
        WidgetTemplates.Apply(layout, template);

    private StackPanel Slots()
    {
        addSlot.Style = (Style)Application.Current.Resources["HakariButton"];
        addSlot.HorizontalAlignment = HorizontalAlignment.Stretch;
        addSlot.Click += (_, _) => change(AddSlot);
        var block = new StackPanel
        {
            Margin = new Thickness(Gutter, Gutter, Gutter, 0),
            Spacing = Gap,
        };
        block.Children.Add(slotList);
        block.Children.Add(addSlot);
        return block;
    }

    private static WidgetLayout AddSlot(WidgetLayout layout) =>
        layout.Slots.Count >= WidgetLayout.MaximumSlots
            ? layout
            : layout with { Slots = [.. layout.Slots, new WidgetSlot(WidgetItem.BurnRate)] };

    private void RebuildSlots(WidgetLayout layout)
    {
        slotList.Children.Clear();
        for (var index = 0; index < layout.Slots.Count; index++)
        {
            var position = index;
            var row = new SlotRowView(
                position + 1,
                layout.Slots[position],
                canMoveUp: position > 0,
                canMoveDown: position < layout.Slots.Count - 1,
                canRemove: layout.Slots.Count > 1);
            row.ItemChanged += (_, item) => change(current => WithItem(current, position, item));
            row.StyleChanged += (_, style) =>
                change(current => WithStyle(current, position, style));
            row.MovedUp += (_, _) => change(current => Moved(current, position, -1));
            row.MovedDown += (_, _) => change(current => Moved(current, position, 1));
            row.Removed += (_, _) => change(current => Without(current, position));
            slotList.Children.Add(row);
        }

        addSlot.Content = Texts.Format("settings.slot.add", WidgetLayout.MaximumSlots);
        addSlot.IsEnabled = layout.UsesSlots && layout.Slots.Count < WidgetLayout.MaximumSlots;
        slotList.Visibility = layout.UsesSlots ? Visibility.Visible : Visibility.Collapsed;
        addSlot.Visibility = layout.UsesSlots ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Changing what a slot shows keeps its style when it still makes sense.</summary>
    private static WidgetLayout WithItem(WidgetLayout layout, int index, WidgetItem item)
    {
        var style = layout.Slots[index].Style;
        var allowed = WidgetItemChoices.StylesFor(item)
            .Select(choice => (WidgetSlotStyle)choice.Value);
        return Replace(layout, index, new WidgetSlot(
            item,
            allowed.Contains(style) ? style : WidgetSlotStyle.Text));
    }

    private static WidgetLayout WithStyle(WidgetLayout layout, int index, WidgetSlotStyle style) =>
        Replace(layout, index, layout.Slots[index] with { Style = style });

    private static WidgetLayout Replace(WidgetLayout layout, int index, WidgetSlot slot)
    {
        var slots = layout.Slots.ToList();
        slots[index] = slot;
        return layout with { Slots = slots };
    }

    private static WidgetLayout Moved(WidgetLayout layout, int index, int by)
    {
        var target = index + by;
        if (target < 0 || target >= layout.Slots.Count)
        {
            return layout;
        }

        var slots = layout.Slots.ToList();
        (slots[index], slots[target]) = (slots[target], slots[index]);
        return layout with { Slots = slots };
    }

    private static WidgetLayout Without(WidgetLayout layout, int index) =>
        layout.Slots.Count <= 1
            ? layout
            : layout with { Slots = [.. layout.Slots.Where((_, position) => position != index)] };

    private StackPanel FormatBlock()
    {
        formatBox.Style = (Style)Application.Current.Resources["HakariTextBox"];
        formatBox.FontFamily = (FontFamily)Application.Current.Resources["HakariMonoFont"];
        formatBox.PlaceholderText = Texts.Get("settings.format.placeholder");
        formatBox.TextChanged += (_, _) =>
        {
            if (!refreshing)
            {
                formatTimer.Stop();
                formatTimer.Start();
            }
        };

        var block = new StackPanel
        {
            Margin = new Thickness(Gutter, Gutter, Gutter, 0),
            Spacing = 4,
        };
        block.Children.Add(new TextBlock
        {
            Text = Texts.Get("settings.format"),
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("HakariInkBrush"),
        });
        block.Children.Add(Hint(Texts.Get("settings.format.description")));
        block.Children.Add(formatBox);
        block.Children.Add(Hint(Texts.Format(
            "settings.format.names",
            string.Join(", ", WidgetValues.Names))));
        return block;
    }

    private void ApplyFormat()
    {
        var text = formatBox.Text.Trim();
        change(layout => layout with { CustomFormat = text.Length == 0 ? null : text });
    }

    private StackPanel Thresholds()
    {
        warnSelect.Selected += (_, value) => change(layout => WithWarn(layout, (int)value));
        criticalSelect.Selected += (_, value) => change(layout => layout with
        {
            CriticalAt = (int)value,
            WarnAt = Math.Min(layout.WarnAt, (int)value - 5),
        });
        var controls = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Gap };
        controls.Children.Add(Dot("HakariWarnBrush"));
        controls.Children.Add(warnSelect);
        controls.Children.Add(Dot("HakariCriticalBrush"));
        controls.Children.Add(criticalSelect);
        var row = new SettingRow
        {
            Glyph = "\uE790",
            Title = Texts.Get("settings.thresholds"),
            Description = Texts.Get("settings.thresholds.description"),
            Content = controls,
            Margin = new Thickness(0, Gutter, 0, 0),
        };
        return new StackPanel { Children = { row } };
    }

    private static WidgetLayout WithWarn(WidgetLayout layout, int warnAt) => layout with
    {
        WarnAt = warnAt,
        CriticalAt = Math.Max(layout.CriticalAt, warnAt + 5),
    };

    private StackPanel Cycle()
    {
        cycleToggle.Click += (_, _) =>
            ChangeCycle(cycleToggle.IsChecked == true ? DefaultCycleSeconds : 0);
        cycleSelect.Selected += (_, value) => ChangeCycle((int)value);
        var controls = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Gap };
        controls.Children.Add(cycleSelect);
        controls.Children.Add(cycleToggle);
        var row = new SettingRow
        {
            Glyph = "\uE895",
            Title = Texts.Get("settings.cycle"),
            Description = Texts.Get("settings.cycle.description"),
            Content = controls,
        };
        return new StackPanel { Children = { row } };
    }

    private void ChangeCycle(int seconds)
    {
        if (!refreshing)
        {
            change(layout => layout with { CycleSeconds = seconds });
        }
    }

    private SettingRow SimpleRow()
    {
        var button = new Button
        {
            Content = Texts.Get("settings.layout.simple.action"),
            Style = (Style)Application.Current.Resources["HakariButton"],
        };
        button.Click += (_, _) => change(layout => layout with
        {
            Template = WidgetTemplate.TwoLines,
            Slots = [],
            CustomFormat = null,
        });
        simpleRow.Glyph = "\uE8FD";
        simpleRow.Title = Texts.Get("settings.layout.simple");
        simpleRow.Description = Texts.Get("settings.layout.simple.description");
        simpleRow.Content = button;
        return simpleRow;
    }

    private static Border Dot(string brushKey) => new()
    {
        Width = DotSize,
        Height = DotSize,
        CornerRadius = new CornerRadius(DotSize / 2),
        Background = Brush(brushKey),
        VerticalAlignment = VerticalAlignment.Center,
    };

    private static TextBlock Hint(string text) => new()
    {
        Text = text,
        FontSize = HintSize,
        Foreground = Brush("HakariInkFaintBrush"),
        TextWrapping = TextWrapping.Wrap,
    };

    private static Brush Brush(string key) => (Brush)Application.Current.Resources[key];
}
