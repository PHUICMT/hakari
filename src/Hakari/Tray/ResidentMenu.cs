using Hakari.Core.Displays;
using Hakari.Core.Localization;
using Hakari.Core.Settings;
using Hakari.Core.Startup;
using Hakari.Taskbar.Tray;

namespace Hakari.Tray;

/// <summary>The menu shared by the tray icon and the widget's right click.</summary>
internal sealed class ResidentMenu(
    SettingsStore settingsStore,
    Action<HakariSettings> apply,
    Action openSettings)
{
    private static readonly (AnimationSetting Setting, string LabelKey)[] AnimationChoices =
    [
        (AnimationSetting.FollowWindows, "menu.animations.follow"),
        (AnimationSetting.Full, "menu.animations.full"),
        (AnimationSetting.Reduced, "menu.animations.reduced"),
        (AnimationSetting.Off, "menu.animations.off"),
    ];

    private static string ExecutablePath => Environment.ProcessPath ?? string.Empty;

    public event EventHandler? QuitRequested;

    /// <summary>Built each time it opens, so it is always in the current language.</summary>
    public IReadOnlyList<TrayMenuItem> Build()
    {
        var settings = settingsStore.Load();
        var items = new List<TrayMenuItem>
        {
            new(Texts.Get("menu.settings"), openSettings),
            new(Texts.Get("menu.pause"), () => Change(current => current with
            {
                Paused = !current.Paused,
            }), settings.Paused),
            TrayMenuItem.Separator,
        };

        items.AddRange(AnimationChoices.Select(choice => new TrayMenuItem(
            Texts.Get(choice.LabelKey),
            () => Change(current => current with { Animation = choice.Setting }),
            settings.Animation == choice.Setting)));

        items.Add(TrayMenuItem.Separator);
        var onEveryDisplay = settings.Displays == TaskbarDisplays.All;
        items.Add(new(Texts.Get("menu.everyDisplay"), () => Change(current => current with
        {
            Displays = onEveryDisplay ? TaskbarDisplays.Primary : TaskbarDisplays.All,
        }), onEveryDisplay));
        var startsWithWindows = Hakari.Shared.StartWithWindows.IsOn(ExecutablePath);
        items.Add(new(
            Texts.Get("menu.startup"),
            () => Hakari.Shared.StartWithWindows.Set(ExecutablePath, !startsWithWindows),
            startsWithWindows));
        items.Add(TrayMenuItem.Separator);
        items.Add(new(
            Texts.Get("menu.quit"),
            () => QuitRequested?.Invoke(this, EventArgs.Empty)));
        return items;
    }

    private void Change(Func<HakariSettings, HakariSettings> change) =>
        apply(settingsStore.Update(change));
}
