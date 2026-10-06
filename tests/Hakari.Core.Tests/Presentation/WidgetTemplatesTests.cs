using Hakari.Core.Presentation.Widget;

namespace Hakari.Core.Tests.Presentation;

public sealed class WidgetTemplatesTests
{
    [Fact]
    public void Every_template_starts_with_between_one_and_four_slots()
    {
        foreach (var template in WidgetTemplates.All)
        {
            var slots = WidgetTemplates.SlotsOf(template);

            Assert.InRange(slots.Count, 1, WidgetLayout.MaximumSlots);
        }
    }

    [Fact]
    public void Applying_a_template_replaces_the_slots_and_clears_the_custom_format()
    {
        var layout = new WidgetLayout { CustomFormat = "{cost.today}", WarnAt = 70 };

        var applied = WidgetTemplates.Apply(layout, WidgetTemplate.Columns);

        Assert.Equal(WidgetTemplate.Columns, applied.Template);
        Assert.Equal(4, applied.Slots.Count);
        Assert.Null(applied.CustomFormat);
        Assert.Equal(70, applied.WarnAt);
    }

    [Fact]
    public void The_ring_with_text_template_has_a_ring_slot()
    {
        var slots = WidgetTemplates.SlotsOf(WidgetTemplate.RingText);

        Assert.Contains(slots, slot => slot.Style == WidgetSlotStyle.Ring);
    }
}
