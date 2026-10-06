using Hakari.Core.Localization;
using Hakari.Core.Presentation.Widget;
using Hakari.Surfaces.Motion;
using Hakari.Core.Settings;
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
    private const double ChangedDip = 0.35;
    private bool refreshing;
    private (SlotMotion Motion, int Index) pendingMotion = (SlotMotion.None, 0);
    private double rowStep;
    private WidgetFacts? previewFacts;

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
    public void Refresh(
        WidgetLayout layout,
        IReadOnlyList<ComposedWidget> panels,
        WidgetFacts facts,
        MultiAccountMode mode)
    {
        refreshing = true;
        previewFacts = facts;
        try
        {
            ShowPreview(panels);
            var now = DateTimeOffset.Now;
            foreach (var card in templateCards)
            {
                card.Choose(layout.UsesSlots && card.Kind == layout.Template);
                var sample = WidgetTemplates.Apply(layout, card.Kind);
                card.ShowPreview(WidgetPanels.Compose(mode, sample, _ => sample, facts, now));
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

    /// <summary>A wide widget scrolls sideways here instead of being cut off.</summary>
    private Border PreviewStrip() => new()
    {
        Margin = new Thickness(Gutter, Gap, Gutter, 0),
        Padding = new Thickness(Gutter, Gutter, Gutter, ScrollBarRoom / 3),
        CornerRadius = new CornerRadius(6),
        Background = Brush("HakariGroundBrush"),
        Child = SideScroller(previewRow),
    };

    /// <summary>The scroll bar floats over the bottom, so the blocks keep clear of it.</summary>
    private const double ScrollBarRoom = 14;

    internal static ScrollViewer SideScroller(FrameworkElement content) => new()
    {
        Content = WithRoomBelow(content),
        HorizontalScrollMode = ScrollMode.Enabled,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        VerticalScrollMode = ScrollMode.Disabled,
        VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
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
                previewRow.Children.Add(FadingIn(new WidgetPreview()));
            }

            ((WidgetPreview)previewRow.Children[index]).Show(panels[index]);
        }
    }

    /// <summary>A block that appears fades in once it is on screen.</summary>
    internal static WidgetPreview FadingIn(WidgetPreview preview)
    {
        if (SurfaceMotion.Current() == AnimationSetting.Off)
        {
            return preview;
        }

        preview.Opacity = 0;
        preview.Loaded += (_, _) => SurfaceMotion.Settle(preview, "Opacity", 1);
        return preview;
    }

    private static FrameworkElement WithRoomBelow(FrameworkElement content)
    {
        content.Margin = new Thickness(0, 0, 0, ScrollBarRoom);
        return content;
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
        addSlot.Click += (_, _) =>
            ChangeSlot(SlotMotion.Added, WidgetLayout.MaximumSlots, AddSlot);
        var block = new StackPanel
        {
            Margin = new Thickness(Gutter, Gutter, Gutter, 0),
            Spacing = Gap,
        };
        block.Children.Add(slotList);
        block.Children.Add(addSlot);
        return block;
    }

    /// <summary>A new slot that the template can show, or the layout unchanged.</summary>
    private static WidgetLayout AddSlot(WidgetLayout layout) =>
        NewSlot(layout) is { } slot ? layout with { Slots = [.. layout.Slots, slot] } : layout;

    /// <summary>
    /// The first kind of slot that still fits: a line of text, else a ring, else a sparkline,
    /// each showing something not already shown. Null when nothing more would show.
    /// </summary>
    private static WidgetSlot? NewSlot(WidgetLayout layout)
    {
        if (!layout.UsesSlots || layout.Slots.Count >= WidgetLayout.MaximumSlots)
        {
            return null;
        }

        var used = layout.Slots.Select(slot => slot.Item).ToHashSet();
        foreach (var style in new[]
            { WidgetSlotStyle.Text, WidgetSlotStyle.Ring, WidgetSlotStyle.Sparkline })
        {
            var item = WidgetItemChoices.ForSlotStyle(style)
                .Select(choice => (WidgetItem)choice.Value)
                .Where(candidate => !used.Contains(candidate))
                .Cast<WidgetItem?>()
                .FirstOrDefault();
            if (item is null)
            {
                continue;
            }

            var slot = new WidgetSlot(item.Value, style);
            var added = layout with { Slots = [.. layout.Slots, slot] };
            if (WidgetTemplates.Shown(added)[^1])
            {
                return slot;
            }
        }

        return null;
    }

    private static bool CanAddSlot(WidgetLayout layout) => NewSlot(layout) is not null;

    /// <summary>
    /// Text always; a ring or a sparkline only when the metric suits it and no other slot
    /// already draws one, since the widget has room for one of each. The slot's own style
    /// stays in the list, so the list always shows what it is.
    /// </summary>
    private static List<(object Value, string Text)> StylesFor(WidgetLayout layout, int index)
    {
        var slot = layout.Slots[index];
        var others = layout.Slots.Where((_, position) => position != index).ToList();
        return
        [
            .. WidgetItemChoices.StylesFor(slot.Item).Where(choice =>
                (WidgetSlotStyle)choice.Value is var style
                && (style == slot.Style
                    || style == WidgetSlotStyle.Text
                    || others.All(other => other.Style != style))),
        ];
    }

    /// <summary>Remembers what is about to happen to which row, then makes the change.</summary>
    private void ChangeSlot(SlotMotion motion, int index, Func<WidgetLayout, WidgetLayout> edit)
    {
        pendingMotion = (motion, index);
        rowStep = slotList.Children.Count > 0
            ? ((FrameworkElement)slotList.Children[0]).ActualHeight + Gap
            : 0;
        change(edit);
    }

    /// <summary>The row folds away and fades first; the layout changes once it is gone.</summary>
    private void RemoveSlot(FrameworkElement row, int index)
    {
        if (SurfaceMotion.Current() == AnimationSetting.Off)
        {
            change(current => Without(current, index));
            return;
        }

        row.IsHitTestVisible = false;
        SurfaceMotion.Settle(row, "Opacity", 0);
        Flyout.CardFold.Run(row, folding: true, fitWindow: () => { });
        var timer = DispatcherQueue.CreateTimer();
        timer.Interval = SurfaceMotion.Normal;
        timer.IsRepeating = false;
        timer.Tick += (_, _) => change(current => Without(current, index));
        timer.Start();
    }

    /// <summary>
    /// A new row opens and fades in; moved rows slide from where they were; a changed row
    /// brightens back in. Off shows the new list at once.
    /// </summary>
    private void PlaySlotMotion()
    {
        var (motion, index) = pendingMotion;
        pendingMotion = (SlotMotion.None, 0);
        if (motion == SlotMotion.None || SurfaceMotion.Current() == AnimationSetting.Off)
        {
            return;
        }

        var rows = slotList.Children.OfType<FrameworkElement>().ToList();
        switch (motion)
        {
            case SlotMotion.Added when rows.Count > 0:
                var added = rows[^1];
                var shownOpacity = added.Opacity;
                added.Opacity = 0;
                Flyout.CardFold.Run(added, folding: false, fitWindow: () => { });
                SurfaceMotion.Settle(added, "Opacity", shownOpacity);
                break;
            case SlotMotion.MovedUp or SlotMotion.MovedDown
                when index >= 0 && index < rows.Count:
                var other = motion == SlotMotion.MovedUp ? index + 1 : index - 1;
                Slide(rows[index], motion == SlotMotion.MovedUp ? rowStep : -rowStep);
                if (other >= 0 && other < rows.Count)
                {
                    Slide(rows[other], motion == SlotMotion.MovedUp ? -rowStep : rowStep);
                }

                break;
            case SlotMotion.Changed when index >= 0 && index < rows.Count:
                var changed = rows[index];
                var target = changed.Opacity;
                changed.Opacity = target * ChangedDip;
                SurfaceMotion.Settle(changed, "Opacity", target);
                break;
        }
    }

    /// <summary>Starts a row where it was and lets it settle into its new place.</summary>
    private static void Slide(FrameworkElement row, double from)
    {
        var offset = new TranslateTransform { Y = from };
        row.RenderTransform = offset;
        SurfaceMotion.Settle(offset, "Y", 0);
    }

    private void RebuildSlots(WidgetLayout layout)
    {
        slotList.Children.Clear();
        var shown = WidgetTemplates.Shown(layout);
        var rules = ToneRules.Of(layout);
        for (var index = 0; index < layout.Slots.Count; index++)
        {
            var position = index;
            var row = new SlotRowView(
                position + 1,
                layout.Slots[position],
                canMoveUp: position > 0,
                canMoveDown: position < layout.Slots.Count - 1,
                canRemove: layout.Slots.Count > 1,
                isShown: position >= shown.Count || shown[position],
                metricSample: value => MetricSample(value, rules),
                styles: StylesFor(layout, position));
            row.ItemChanged += (_, item) => ChangeSlot(
                SlotMotion.Changed, position, current => WithItem(current, position, item));
            row.StyleChanged += (_, style) => ChangeSlot(
                SlotMotion.Changed, position, current => WithStyle(current, position, style));
            row.MovedUp += (_, _) => ChangeSlot(
                SlotMotion.MovedUp, position - 1, current => Moved(current, position, -1));
            row.MovedDown += (_, _) => ChangeSlot(
                SlotMotion.MovedDown, position + 1, current => Moved(current, position, 1));
            row.Removed += (_, _) => RemoveSlot(row, position);
            slotList.Children.Add(row);
        }

        PlaySlotMotion();

        var roomLeft = CanAddSlot(layout);
        addSlot.Content = roomLeft
            ? Texts.Format("settings.slot.add", WidgetLayout.MaximumSlots)
            : Texts.Get(layout.Slots.Count >= WidgetLayout.MaximumSlots
                ? "settings.slot.atMost"
                : "settings.slot.noRoom");
        addSlot.IsEnabled = roomLeft;
        ToolTipService.SetToolTip(addSlot, roomLeft ? null : Texts.Get("settings.slot.full"));
        slotList.Visibility = layout.UsesSlots ? Visibility.Visible : Visibility.Collapsed;
        addSlot.Visibility = layout.UsesSlots ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>What a metric shows right now, such as "$12.50" or "62%", in the list.</summary>
    private TextBlock? MetricSample(object value, ToneRules rules) =>
        previewFacts is null || value is not WidgetItem item
            ? null
            : new TextBlock
            {
                Text = SlotCells.Of(item, previewFacts, DateTimeOffset.Now, rules).Value,
                FontSize = HintSize,
                Foreground = Brush("HakariInkFaintBrush"),
            };

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
