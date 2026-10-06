using Hakari.Core.Presentation.Widget;

namespace Hakari.Core.Tests.Presentation;

public sealed class SavedLayoutsTests
{
    private static readonly WidgetLayout Columns =
        WidgetTemplates.Apply(new WidgetLayout(), WidgetTemplate.Columns);

    [Fact]
    public void Keeps_a_layout_under_its_name_newest_first()
    {
        var saved = SavedLayouts.Save([], "Work", new WidgetLayout());
        saved = SavedLayouts.Save(saved, "Busy day", Columns);

        Assert.Equal(["Busy day", "Work"], saved.Select(entry => entry.Name));
    }

    [Fact]
    public void Saving_under_the_same_name_replaces_the_old_one()
    {
        var saved = SavedLayouts.Save([], "Work", new WidgetLayout());

        saved = SavedLayouts.Save(saved, "work", Columns);

        Assert.Equal(Columns, Assert.Single(saved).Layout);
    }

    [Fact]
    public void A_blank_name_keeps_nothing()
    {
        Assert.Empty(SavedLayouts.Save([], "   ", Columns));
    }

    [Fact]
    public void Lets_the_oldest_go_past_the_limit()
    {
        IReadOnlyList<NamedLayout> saved = [];
        for (var number = 0; number <= SavedLayouts.MaximumCount; number++)
        {
            saved = SavedLayouts.Save(saved, $"L{number}", Columns);
        }

        Assert.Equal(SavedLayouts.MaximumCount, saved.Count);
        Assert.DoesNotContain(saved, entry => entry.Name == "L0");
    }

    [Fact]
    public void Removes_by_name()
    {
        var saved = SavedLayouts.Save([], "Work", Columns);

        Assert.Empty(SavedLayouts.Remove(saved, "Work"));
    }
}
