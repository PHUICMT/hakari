namespace Hakari.Core.Presentation.Widget;

/// <summary>One place in the widget: what it shows and how it is drawn.</summary>
public sealed record WidgetSlot(WidgetItem Item, WidgetSlotStyle Style = WidgetSlotStyle.Text);
