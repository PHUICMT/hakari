using Hakari.Core.Configuration;
using Hakari.Core.Settings;

namespace Hakari.Core.Sound;

/// <summary>
/// Where each alert sound is on disk. Hakari's own sounds are made once into the data folder;
/// a file the user picks is copied there, so moving or deleting the original changes nothing.
/// </summary>
public static class AlertSoundFiles
{
    public const int DefaultVolume = 60;
    public const int MaximumVolume = 100;

    /// <summary>The volumes offered in Settings.</summary>
    public static readonly int[] Volumes = [10, 20, 30, 40, 50, 60, 70, 80, 90, 100];

    /// <summary>Formats Windows plays without extra codecs.</summary>
    public static readonly string[] Extensions = [".wav", ".mp3", ".wma", ".m4a"];

    private const string FolderName = "sounds";
    private const string CustomName = "custom";

    /// <summary>Bump to make the sounds again after their notes change.</summary>
    private const int Version = 1;

    private static readonly TimeSpan Now = TimeSpan.Zero;

    private static readonly Dictionary<AlertSound, Note[]> Melodies = new()
    {
        [AlertSound.Chime] =
        [
            new(659.25, Now, TimeSpan.FromMilliseconds(700)),
            new(880.00, TimeSpan.FromMilliseconds(130), TimeSpan.FromMilliseconds(800)),
        ],
        [AlertSound.Soft] = [new(392.00, Now, TimeSpan.FromMilliseconds(1000))],
        [AlertSound.Ping] = [new(1318.51, Now, TimeSpan.FromMilliseconds(380))],
    };

    public static string Folder => Path.Combine(HakariPaths.DataDirectory, FolderName);

    public static bool IsBuiltIn(AlertSound sound) => Melodies.ContainsKey(sound);

    public static int Usable(int volume) => Math.Clamp(volume, 0, MaximumVolume);

    /// <summary>
    /// The file to play, or null for Windows' own sound, for no sound, or for a picked file
    /// that is gone.
    /// </summary>
    public static string? PathOf(HakariSettings settings) => settings.AlertSound switch
    {
        AlertSound.Custom => File.Exists(settings.AlertSoundFile) ? settings.AlertSoundFile : null,
        var sound when Melodies.TryGetValue(sound, out var notes) => BuiltIn(sound, notes),
        _ => null,
    };

    /// <summary>Copies the user's file in, replacing an earlier pick; returns the copy.</summary>
    /// <exception cref="IOException">The file could not be read or copied.</exception>
    public static string Import(string sourcePath)
    {
        Directory.CreateDirectory(Folder);
        foreach (var old in Directory.EnumerateFiles(Folder, CustomName + ".*"))
        {
            File.Delete(old);
        }

        var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        var copy = Path.Combine(Folder, CustomName + extension);
        File.Copy(sourcePath, copy, overwrite: true);
        return copy;
    }

    public static bool IsSupported(string path) =>
        Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    private static string? BuiltIn(AlertSound sound, Note[] notes)
    {
        var path = Path.Combine(Folder, $"{sound.ToString().ToLowerInvariant()}-{Version}.wav");
        try
        {
            if (!File.Exists(path))
            {
                Directory.CreateDirectory(Folder);
                File.WriteAllBytes(path, ToneSynth.Wav(notes));
            }

            return path;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
