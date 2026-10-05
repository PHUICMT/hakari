using System.Diagnostics;
using Hakari.Core.Settings;
using Hakari.Startup;
using Hakari.Taskbar.Tray;

namespace Hakari.Tray;

/// <summary>The menu shared by the tray icon and the widget's right click.</summary>
internal sealed class ResidentMenu(SettingsStore settingsStore, Action<HakariSettings> apply)
{
    private static readonly (AnimationSetting Setting, string Label)[] AnimationChoices =
    [
        (AnimationSetting.FollowWindows, "Animations: follow Windows"),
        (AnimationSetting.Full, "Animations: full"),
        (AnimationSetting.Reduced, "Animations: reduced"),
        (AnimationSetting.Off, "Animations: off"),
    ];

    public event EventHandler? QuitRequested;

    public IReadOnlyList<TrayMenuItem> Build()
    {
        var settings = settingsStore.Load();
        var items = new List<TrayMenuItem>
        {
            new("Pause updates", () => Change(current => current with
            {
                Paused = !current.Paused,
            }), settings.Paused),
            TrayMenuItem.Separator,
        };

        items.AddRange(AnimationChoices.Select(choice => new TrayMenuItem(
            choice.Label,
            () => Change(current => current with { Animation = choice.Setting }),
            settings.Animation == choice.Setting)));

        items.Add(TrayMenuItem.Separator);
        items.Add(new("Show on every display", () => Change(current => current with
        {
            ShowOnSecondaryTaskbars = !current.ShowOnSecondaryTaskbars,
        }), settings.ShowOnSecondaryTaskbars));
        items.Add(new(
            "Start with Windows",
            StartupRegistration.Toggle,
            StartupRegistration.IsRegistered()));
        items.Add(new("Open settings file", OpenSettingsFile));
        items.Add(TrayMenuItem.Separator);
        items.Add(new("Quit Hakari", () => QuitRequested?.Invoke(this, EventArgs.Empty)));
        return items;
    }

    private void Change(Func<HakariSettings, HakariSettings> change) =>
        apply(settingsStore.Update(change));

    private void OpenSettingsFile()
    {
        if (!File.Exists(settingsStore.Path))
        {
            settingsStore.Save(settingsStore.Load());
        }

        Process.Start(new ProcessStartInfo(settingsStore.Path) { UseShellExecute = true });
    }
}
