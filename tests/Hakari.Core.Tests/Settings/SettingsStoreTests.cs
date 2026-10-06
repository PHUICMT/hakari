using Hakari.Core.Presentation.Widget;
using Hakari.Core.Settings;
using Hakari.Core.Tests.Support;

namespace Hakari.Core.Tests.Settings;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly TemporaryDirectory directory = new();

    [Fact]
    public void Uses_defaults_when_there_is_no_file()
    {
        var settings = Store().Load();

        Assert.Equal(AnimationSetting.FollowWindows, settings.Animation);
        Assert.False(settings.RefreshSignInAutomatically);
    }

    [Fact]
    public void Remembers_saved_changes()
    {
        var store = Store();

        store.Update(settings => settings with
        {
            Animation = AnimationSetting.Off,
            Currency = "THB",
        });

        Assert.Equal(AnimationSetting.Off, store.Load().Animation);
        Assert.Equal("THB", store.Load().Currency);
    }

    [Fact]
    public void Remembers_a_layout_with_slots_a_format_and_thresholds()
    {
        var store = Store();
        var layout = new WidgetLayout
        {
            Template = WidgetTemplate.Columns,
            Slots =
            [
                new WidgetSlot(WidgetItem.CostToday),
                new WidgetSlot(WidgetItem.SessionLimit, WidgetSlotStyle.Ring),
            ],
            CustomFormat = "{cost.today:$0.00}",
            WarnAt = 70,
            CriticalAt = 90,
            CycleSeconds = 8,
        };

        store.Update(settings => settings with { Widget = layout });

        Assert.Equal(layout, store.Load().Widget);
    }

    [Fact]
    public void Falls_back_to_defaults_when_the_file_is_broken()
    {
        var store = Store();
        File.WriteAllText(store.Path, "{ not json");

        var settings = store.Load();
        Assert.Equal(AnimationSetting.FollowWindows, settings.Animation);
        Assert.Empty(settings.ExtraConfigDirectories);
    }

    public void Dispose() => directory.Dispose();

    private SettingsStore Store() => new(directory.Combine(SettingsStore.FileName));
}
