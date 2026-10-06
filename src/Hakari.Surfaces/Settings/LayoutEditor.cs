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
    private readonly TaskbarStrips strips = new();
    private readonly StackPanel formatSample = new()
    {
        Orientation = Orientation.Horizontal,
        Spacing = 2,
    };
    private (WidgetLayout Layout, IReadOnlyList<ComposedWidget> Panels, MultiAccountMode Mode,
        int Focused)? shown;
    private WidgetTemplate? hoveredTemplate;
    private readonly List<TemplateCard> templateCards = [];
    private readonly Func<IReadOnlyList<NamedLayout>> saved;
    private readonly Action<Func<IReadOnlyList<NamedLayout>, IReadOnlyList<NamedLayout>>>
        changeSaved;
    private readonly Grid savedGrid = new() { ColumnSpacing = Gap, RowSpacing = Gap };
    private readonly TextBlock savedHint = new();
    private readonly TextBox nameBox = new();
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

    /// <param name="saved">The user's saved layouts, read fresh each time.</param>
    /// <param name="changeSaved">Saves a change to that list.</param>
    public LayoutEditor(
        Func<WidgetLayout> currentLayout,
        Action<Func<WidgetLayout, WidgetLayout>> change,
        Func<IReadOnlyList<NamedLayout>> saved,
        Action<Func<IReadOnlyList<NamedLayout>, IReadOnlyList<NamedLayout>>> changeSaved)
    {
        this.currentLayout = currentLayout;
        this.change = change;
        this.saved = saved;
        this.changeSaved = changeSaved;
        formatTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        formatTimer.Interval = TimeSpan.FromMilliseconds(FormatDebounceMilliseconds);
        formatTimer.IsRepeating = false;
        formatTimer.Tick += (_, _) => ApplyFormat();

        Children.Add(Header());
        TargetBar.Margin = new Thickness(Gutter, Gap, Gutter, 0);
        TargetBar.Visibility = Visibility.Collapsed;
        Children.Add(TargetBar);
        Children.Add(EditorColumns());
        Children.Add(MyLayouts());
        Children.Add(SimpleRow());
    }

    /// <summary>Whose layout is being edited; hidden while the accounts share one block.</summary>
    public LayoutTargetBar TargetBar { get; } = new();

    public void ShowTargets(IReadOnlyList<LayoutTarget> targets, string chosen)
    {
        TargetBar.Visibility = targets.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        if (targets.Count > 1)
        {
            TargetBar.Show(targets, chosen);
        }
    }

    /// <summary>Shows the layout's state in every control, without changing it.</summary>
    /// <param name="focusedPanel">The block of the account being edited, the rest dimmed.</param>
    public void Refresh(
        WidgetLayout layout,
        IReadOnlyList<ComposedWidget> panels,
        WidgetFacts facts,
        MultiAccountMode mode,
        int focusedPanel = -1)
    {
        refreshing = true;
        previewFacts = facts;
        try
        {
            shown = (layout, panels, mode, focusedPanel);
            if (hoveredTemplate is { } hovered)
            {
                ShowTemplateSample(hovered);
            }
            else
            {
                strips.Show(panels, focusedPanel);
            }

            var now = DateTimeOffset.Now;
            var isSavedLayout = saved().Any(entry => entry.Layout == layout);
            foreach (var card in templateCards)
            {
                card.Choose(
                    !isSavedLayout && layout.UsesSlots && card.Kind == layout.Template);
            }

            ShowFormatSample();

            RebuildSaved(layout, facts, mode, now);
            RebuildSlots(layout);
            if (formatBox.FocusState == FocusState.Unfocused)
            {
                formatBox.Text = layout.CustomFormat ?? string.Empty;
            }

            belowText.Text = Texts.Format("settings.thresholds.below", layout.WarnAt);
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



    /// <summary>
    /// The user's own layouts, each a card that puts it back, and a field to save the one
    /// being edited under a name.
    /// </summary>
    private StackPanel MyLayouts()
    {
        var block = new StackPanel
        {
            Margin = new Thickness(Gutter, Gutter, Gutter, 0),
            Spacing = Gap,
        };
        block.Children.Add(new TextBlock
        {
            Text = Texts.Get("settings.layout.mine"),
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("HakariInkBrush"),
        });
        savedHint.Text = Texts.Get("settings.layout.mine.empty");
        savedHint.FontSize = HintSize;
        savedHint.Foreground = Brush("HakariInkFaintBrush");
        savedHint.TextWrapping = TextWrapping.Wrap;
        block.Children.Add(savedHint);
        savedGrid.ColumnDefinitions.Add(new ColumnDefinition());
        savedGrid.ColumnDefinitions.Add(new ColumnDefinition());
        block.Children.Add(savedGrid);
        block.Children.Add(SaveRow());
        return block;
    }

    private Grid SaveRow()
    {
        nameBox.Style = (Style)Application.Current.Resources["HakariTextBox"];
        nameBox.PlaceholderText = Texts.Get("settings.layout.name");
        nameBox.MaxLength = SavedLayouts.MaximumNameLength;
        var save = new Button
        {
            Content = Texts.Get("settings.layout.save"),
            Style = (Style)Application.Current.Resources["HakariPrimaryButton"],
        };
        save.Click += (_, _) => SaveCurrent();
        nameBox.KeyDown += (_, args) =>
        {
            if (args.Key == Windows.System.VirtualKey.Enter)
            {
                args.Handled = true;
                SaveCurrent();
            }
        };
        var row = new Grid { ColumnSpacing = Gap };
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(nameBox);
        Grid.SetColumn(save, 1);
        row.Children.Add(save);
        return row;
    }

    private void SaveCurrent()
    {
        var name = nameBox.Text;
        if (name.Trim().Length == 0)
        {
            nameBox.Focus(FocusState.Programmatic);
            return;
        }

        var layout = currentLayout();
        nameBox.Text = string.Empty;
        changeSaved(list => SavedLayouts.Save(list, name, layout));
    }

    private void RebuildSaved(
        WidgetLayout layout,
        WidgetFacts facts,
        MultiAccountMode mode,
        DateTimeOffset now)
    {
        var list = saved();
        var known = savedGrid.Children.OfType<SavedLayoutCard>()
            .Select(card => card.Saved.Name)
            .ToHashSet();
        savedGrid.Children.Clear();
        savedGrid.RowDefinitions.Clear();
        savedHint.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        for (var index = 0; index < list.Count; index++)
        {
            if (index % 2 == 0)
            {
                savedGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            var entry = list[index];
            var card = new SavedLayoutCard(entry);
            card.Choose(entry.Layout == layout);
            card.ShowPreview(
                WidgetPanels.Compose(mode, entry.Layout, _ => entry.Layout, facts, now));
            card.Chosen += (_, chosen) => change(_ => chosen.Layout);
            card.Removed += (_, removed) =>
                changeSaved(current => SavedLayouts.Remove(current, removed.Name));
            Grid.SetColumn(card, index % 2);
            Grid.SetRow(card, index / 2);
            savedGrid.Children.Add(card);
            if (!known.Contains(entry.Name) && known.Count + 1 >= list.Count)
            {
                FadeIn(card);
            }
        }
    }

    private static void FadeIn(UIElement element)
    {
        if (SurfaceMotion.Current() == AnimationSetting.Off)
        {
            return;
        }

        element.Opacity = 0;
        SurfaceMotion.Settle(element, "Opacity", 1);
    }

    /// <summary>A block that appears fades in once it is on screen.</summary>
    internal static WidgetPreview FadingIn(WidgetPreview preview)
    {
        if (SurfaceMotion.Current() == AnimationSetting.Off)
        {
            return preview;
        }

        preview.Opacity = 0;
        preview.Loaded += (_, _) =>
            SurfaceMotion.Settle(preview, "Opacity", preview.Tag is double target ? target : 1);
        return preview;
    }

    private static FrameworkElement WithRoomBelow(FrameworkElement content)
    {
        content.Margin = new Thickness(0, 0, 0, ScrollBarRoom);
        return content;
    }

    private const double PickerWidth = 240;
    private const double NarrowWidth = 640;
    private const double ColumnGap = 16;

    /// <summary>
    /// The template picker on the left; on the right the preview strips, the slots, the
    /// custom format and the color and cycle row. A narrow window stacks the two.
    /// </summary>
    private Grid EditorColumns()
    {
        var grid = new Grid
        {
            Margin = new Thickness(Gutter, Gutter, Gutter, 0),
            ColumnSpacing = ColumnGap,
            RowSpacing = ColumnGap,
        };
        var pickerColumn = new ColumnDefinition { Width = new GridLength(PickerWidth) };
        grid.ColumnDefinitions.Add(pickerColumn);
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var picker = Templates();
        var right = new StackPanel { Spacing = 12 };
        right.Children.Add(strips);
        right.Children.Add(Slots());
        right.Children.Add(FormatBlock());
        right.Children.Add(ColorsAndCycle());
        grid.Children.Add(picker);
        grid.Children.Add(right);
        grid.SizeChanged += (_, args) =>
        {
            var narrow = args.NewSize.Width < NarrowWidth;
            pickerColumn.Width = narrow ? new GridLength(0) : new GridLength(PickerWidth);
            grid.ColumnSpacing = narrow ? 0 : ColumnGap;
            Grid.SetColumnSpan(picker, narrow ? 2 : 1);
            Grid.SetColumn(right, narrow ? 0 : 1);
            Grid.SetColumnSpan(right, narrow ? 2 : 1);
            Grid.SetRow(right, narrow ? 1 : 0);
        };
        Grid.SetColumn(right, 1);
        return grid;
    }

    private StackPanel Templates()
    {
        var list = new StackPanel { Spacing = 6 };
        foreach (var template in WidgetTemplates.All)
        {
            var card = new TemplateCard(template);
            card.Chosen += (_, chosen) =>
            {
                hoveredTemplate = null;
                change(layout => Pick(layout, chosen));
            };
            card.Hovered += (_, over) => HoverTemplate(card.Kind, over);
            list.Children.Add(card);
            templateCards.Add(card);
        }

        return list;
    }

    /// <summary>While the pointer rests on a template, the strips show the widget in it.</summary>
    private void HoverTemplate(WidgetTemplate template, bool over)
    {
        if (over)
        {
            hoveredTemplate = template;
            ShowTemplateSample(template);
            return;
        }

        if (hoveredTemplate == template)
        {
            hoveredTemplate = null;
            if (shown is { } current)
            {
                strips.Show(current.Panels, current.Focused);
            }
        }
    }

    private void ShowTemplateSample(WidgetTemplate template)
    {
        if (shown is not { } current || previewFacts is null)
        {
            return;
        }

        if (current.Layout.UsesSlots && current.Layout.Template == template)
        {
            strips.Show(current.Panels, current.Focused);
            return;
        }

        var sample = WidgetTemplates.Apply(current.Layout, template);
        strips.Show(WidgetPanels.Compose(
            current.Mode, sample, _ => sample, previewFacts, DateTimeOffset.Now));
    }

    /// <summary>What the format being typed gives, before it is applied.</summary>
    private void ShowFormatSample()
    {
        if (shown is not { } current || previewFacts is null)
        {
            return;
        }

        var text = formatBox.Text.Trim();
        var sample = current.Layout with { CustomFormat = text.Length == 0 ? null : text };
        var panels = WidgetPanels.Compose(
            current.Mode, sample, _ => sample, previewFacts, DateTimeOffset.Now);
        while (formatSample.Children.Count > panels.Count)
        {
            formatSample.Children.RemoveAt(formatSample.Children.Count - 1);
        }

        for (var index = 0; index < panels.Count; index++)
        {
            if (index >= formatSample.Children.Count)
            {
                formatSample.Children.Add(FadingIn(new WidgetPreview()));
            }

            ((WidgetPreview)formatSample.Children[index]).Show(panels[index]);
        }
    }

    /// <summary>The accounts template cycles one account's value at a time.</summary>
    private static WidgetLayout Pick(WidgetLayout layout, WidgetTemplate template) =>
        WidgetTemplates.Apply(layout, template);

    private StackPanel Slots()
    {
        addSlot.Style = (Style)Application.Current.Resources["HakariDashedButton"];
        addSlot.HorizontalAlignment = HorizontalAlignment.Stretch;
        addSlot.HorizontalContentAlignment = HorizontalAlignment.Left;
        addSlot.Click += (_, _) =>
            ChangeSlot(SlotMotion.Added, WidgetLayout.MaximumSlots, AddSlot);
        var block = new StackPanel { Spacing = 6 };
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
                ShowFormatSample();
                formatTimer.Stop();
                formatTimer.Start();
            }
        };

        var block = new StackPanel { Spacing = 6 };
        block.Children.Add(Hint(Texts.Get("settings.format.description")));
        block.Children.Add(formatBox);
        var sampleRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Gap };
        sampleRow.Children.Add(new TextBlock
        {
            Text = Texts.Get("settings.preview"),
            FontSize = HintSize,
            Foreground = Brush("HakariInkFaintBrush"),
            VerticalAlignment = VerticalAlignment.Center,
        });
        sampleRow.Children.Add(formatSample);
        block.Children.Add(SideScroller(sampleRow));
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

    private const double NarrowRow = 560;

    /// <summary>
    /// One row, as in the design: the three colors a limit takes with where each starts, and
    /// on the right whether the widget cycles through its slots and how fast. Narrow, the
    /// cycle goes under the colors.
    /// </summary>
    private Grid ColorsAndCycle()
    {
        warnSelect.Selected += (_, value) => change(layout => WithWarn(layout, (int)value));
        criticalSelect.Selected += (_, value) => change(layout => layout with
        {
            CriticalAt = (int)value,
            WarnAt = Math.Min(layout.WarnAt, (int)value - 5),
        });
        cycleToggle.Click += (_, _) =>
            ChangeCycle(cycleToggle.IsChecked == true ? DefaultCycleSeconds : 0);
        cycleSelect.Selected += (_, value) => ChangeCycle((int)value);

        var colors = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Gap };
        colors.Children.Add(RowText(Texts.Get("settings.thresholds"), isLabel: true));
        colors.Children.Add(Dot("HakariAccentBrush"));
        colors.Children.Add(belowText);
        colors.Children.Add(Dot("HakariWarnBrush"));
        colors.Children.Add(warnSelect);
        colors.Children.Add(Dot("HakariCriticalBrush"));
        colors.Children.Add(criticalSelect);
        colors.Children.Add(RowText(Texts.Get("settings.thresholds.andUp"), isLabel: false));
        ToolTipService.SetToolTip(colors, Texts.Get("settings.thresholds.description"));

        var cycle = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Gap };
        cycle.Children.Add(RowText(Texts.Get("settings.cycle"), isLabel: true));
        cycle.Children.Add(cycleSelect);
        cycle.Children.Add(cycleToggle);
        ToolTipService.SetToolTip(cycle, Texts.Get("settings.cycle.description"));

        var row = new Grid { RowSpacing = Gap, ColumnSpacing = Gap };
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        row.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        row.Children.Add(colors);
        row.Children.Add(cycle);
        Grid.SetColumn(cycle, 1);
        row.SizeChanged += (_, args) =>
        {
            var narrow = args.NewSize.Width < NarrowRow;
            Grid.SetColumn(cycle, narrow ? 0 : 1);
            Grid.SetRow(cycle, narrow ? 1 : 0);
        };
        return row;
    }

    private readonly TextBlock belowText = RowText(string.Empty, isLabel: false);

    private static TextBlock RowText(string text, bool isLabel) => new()
    {
        Text = text,
        FontSize = 13,
        Foreground = Brush(isLabel ? "HakariInkBrush" : "HakariInkMutedBrush"),
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(0, 0, isLabel ? 4 : 0, 0),
    };
    private static WidgetLayout WithWarn(WidgetLayout layout, int warnAt) => layout with
    {
        WarnAt = warnAt,
        CriticalAt = Math.Max(layout.CriticalAt, warnAt + 5),
    };

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
