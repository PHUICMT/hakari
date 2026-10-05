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
