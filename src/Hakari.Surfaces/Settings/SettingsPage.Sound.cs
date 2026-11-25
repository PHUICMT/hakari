using Hakari.Core.Interprocess;
using Hakari.Core.Localization;
using Hakari.Core.Settings;
using Hakari.Core.Sound;
using Microsoft.UI.Xaml;
using Windows.Storage.Pickers;

namespace Hakari.Surfaces.Settings;

/// <summary>
/// The alert sound rows: which sound, a button to hear it, and how loud. Hakari.exe plays it,
/// so the test sounds exactly like a real alert.
/// </summary>
public sealed partial class SettingsPage
{
    private bool soundHandlersAttached;

    private static readonly AlertSound[] SoundChoices =
    [
        AlertSound.Windows,
        AlertSound.Chime,
        AlertSound.Soft,
        AlertSound.Ping,
        AlertSound.Custom,
        AlertSound.None,
    ];

    private void FillSound(HakariSettings settings)
    {
        AlertSoundSelect.SetChoices(
            SoundChoices.Select(sound => ((object)sound, SoundName(sound))),
            settings.AlertSound);
        AlertVolumeSelect.SetChoices(
            AlertSoundFiles.Volumes.Select(percent => ((object)percent, $"{percent}%")),
            NearestVolume(settings.AlertVolume));
        ShowSoundState(settings);
        if (soundHandlersAttached)
        {
            return;
        }

        soundHandlersAttached = true;
        AlertSoundSelect.Selected += (_, value) => OnSoundChosen((AlertSound)value);
        AlertVolumeSelect.Selected += (_, value) =>
            Save(current => current with { AlertVolume = (int)value });
    }

    private async void OnSoundChosen(AlertSound sound)
    {
        if (sound != AlertSound.Custom)
        {
            Save(current => current with { AlertSound = sound });
            ShowSoundState(store.Load());
            return;
        }

        var copy = await PickSoundFileAsync();
        var settings = copy is null
            ? store.Load()
            : store.Update(current => current with
            {
                AlertSound = AlertSound.Custom,
                AlertSoundFile = copy,
            });

        // A cancelled or failed pick goes back to what was chosen before.
        AlertSoundSelect.SetChoices(
            SoundChoices.Select(choice => ((object)choice, SoundName(choice))),
            settings.AlertSound);
        ShowSoundState(settings);
    }

    /// <returns>Hakari's copy of the picked file, or null when none was picked or copied.</returns>
    private async Task<string?> PickSoundFileAsync()
    {
        var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.MusicLibrary };
        foreach (var extension in AlertSoundFiles.Extensions)
        {
            picker.FileTypeFilter.Add(extension);
        }

        WinRT.Interop.InitializeWithWindow.Initialize(picker, HostWindowHandle);
        if (await picker.PickSingleFileAsync() is not { } file
            || !AlertSoundFiles.IsSupported(file.Path))
        {
            return null;
        }

        try
        {
            return await Task.Run(() => AlertSoundFiles.Import(file.Path));
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException)
        {
            AlertSoundRow.Description = Texts.Get("settings.alerts.sound.failed");
            return null;
        }
    }

    private void OnPlaySoundClicked(object sender, RoutedEventArgs args) =>
        ResidentChannel.TrySend(ResidentCommand.PlayAlertSound);

    /// <summary>
    /// Says which file plays, and that the volume only reaches Hakari's sounds; the volume row
    /// and the test button have nothing to do while alerts make no sound.
    /// </summary>
    private void ShowSoundState(HakariSettings settings)
    {
        var picked = settings.AlertSound == AlertSound.Custom
            && File.Exists(settings.AlertSoundFile);
        AlertSoundRow.Description = picked
            ? Texts.Format("settings.alerts.sound.file", Path.GetFileName(settings.AlertSoundFile))
            : Texts.Get("settings.alerts.sound.description");
        AlertVolumeSelect.IsEnabled = settings.AlertSound is not (AlertSound.Windows
            or AlertSound.None);
        PlaySoundButton.IsEnabled = settings.AlertSound != AlertSound.None;
    }

    private static int NearestVolume(int volume) =>
        AlertSoundFiles.Volumes.MinBy(choice => Math.Abs(choice - volume));

    private static string SoundName(AlertSound sound) => Texts.Get(sound switch
    {
        AlertSound.Chime => "settings.alerts.sound.chime",
        AlertSound.Soft => "settings.alerts.sound.soft",
        AlertSound.Ping => "settings.alerts.sound.ping",
        AlertSound.Custom => "settings.alerts.sound.custom",
        AlertSound.None => "settings.alerts.sound.none",
        _ => "settings.alerts.sound.windows",
    });
}
