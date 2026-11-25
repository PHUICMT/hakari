using System.Runtime.InteropServices;
using Hakari.Core.Settings;
using Hakari.Core.Sound;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace Hakari.Notifications;

/// <summary>
/// Plays the alert sound the user chose at the volume they chose. Windows' own notification
/// sound has no volume of its own to set, so only Hakari's sounds and picked files go
/// through here; the alert itself is then shown silent.
/// </summary>
internal static class AlertSoundPlayer
{
    private const string WindowsNotificationAlias = "Notification.Default";
    private const uint PlayAlias = 0x0001_0000;
    private const uint PlayAsync = 0x0001;
    private const uint NoDefaultSound = 0x0002;
    private const double PercentScale = 100.0;

    private static MediaPlayer? playing;

    /// <summary>Whether Hakari plays this alert's sound itself, so Windows should not.</summary>
    public static bool PlaysItself(HakariSettings settings) =>
        settings.AlertSound == AlertSound.None || AlertSoundFiles.PathOf(settings) is not null;

    /// <summary>Plays the chosen sound; for Windows' own sound it plays nothing.</summary>
    public static void Play(HakariSettings settings)
    {
        if (AlertSoundFiles.PathOf(settings) is not { } path)
        {
            return;
        }

        try
        {
            Stop();
            var player = new MediaPlayer
            {
                Volume = AlertSoundFiles.Usable(settings.AlertVolume) / PercentScale,
                AudioCategory = MediaPlayerAudioCategory.Alerts,
            };

            // Otherwise Windows shows a media overlay with play and pause for a short ping.
            player.CommandManager.IsEnabled = false;
            player.MediaEnded += (_, _) => Stop(player);
            player.MediaFailed += (_, _) => Stop(player);
            player.Source = MediaSource.CreateFromUri(new Uri(path));
            playing = player;
            player.Play();
        }
        catch (Exception exception) when (exception is COMException or ArgumentException
            or UnauthorizedAccessException or InvalidOperationException)
        {
            Stop();
        }
    }

    /// <summary>For the Settings test button: also plays Windows' own sound when chosen.</summary>
    public static void Preview(HakariSettings settings)
    {
        if (settings.AlertSound == AlertSound.Windows)
        {
            PlaySound(
                WindowsNotificationAlias,
                IntPtr.Zero,
                PlayAlias | PlayAsync | NoDefaultSound);
            return;
        }

        Play(settings);
    }

    private static void Stop(MediaPlayer? finished = null)
    {
        var player = Interlocked.Exchange(ref playing, null);
        if (finished is not null && !ReferenceEquals(player, finished))
        {
            // A newer sound is playing; leave it and only let the finished one go.
            Interlocked.CompareExchange(ref playing, player, null);
            finished.Dispose();
            return;
        }

        player?.Dispose();
    }

    [DllImport("winmm.dll", CharSet = CharSet.Unicode, EntryPoint = "PlaySoundW")]
    private static extern bool PlaySound(string sound, IntPtr module, uint flags);
}
