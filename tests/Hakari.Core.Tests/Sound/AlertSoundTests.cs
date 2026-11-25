using System.Text;
using Hakari.Core.Settings;
using Hakari.Core.Sound;
using Hakari.Core.Tests.Support;

namespace Hakari.Core.Tests.Sound;

public class AlertSoundTests
{
    private const int HeaderBytes = 44;
    private const int BytesPerSample = 2;

    [Fact]
    public void Makes_a_playable_16_bit_mono_wav_of_the_right_length()
    {
        var wav = ToneSynth.Wav([new Note(440, TimeSpan.Zero, TimeSpan.FromMilliseconds(500))]);

        Assert.Equal("RIFF", Encoding.ASCII.GetString(wav, 0, 4));
        Assert.Equal("WAVE", Encoding.ASCII.GetString(wav, 8, 4));
        Assert.Equal(wav.Length - 8, BitConverter.ToInt32(wav, 4));
        Assert.Equal(ToneSynth.SampleRate, BitConverter.ToInt32(wav, 24));
        Assert.Equal(ToneSynth.SampleRate / 2, (wav.Length - HeaderBytes) / BytesPerSample);
    }

    [Fact]
    public void Starts_from_silence_fades_out_and_never_clips()
    {
        var wav = ToneSynth.Wav([new Note(880, TimeSpan.Zero, TimeSpan.FromMilliseconds(400))]);
        var samples = Enumerable.Range(0, (wav.Length - HeaderBytes) / BytesPerSample)
            .Select(index => BitConverter.ToInt16(wav, HeaderBytes + index * BytesPerSample))
            .ToArray();

        Assert.Equal(0, samples[0]);
        Assert.True(samples.Max(sample => Math.Abs((int)sample)) < short.MaxValue);
        var loudStart = samples.Take(2000).Max(sample => Math.Abs((int)sample));
        var quietEnd = samples.TakeLast(2000).Max(sample => Math.Abs((int)sample));
        Assert.True(quietEnd < loudStart / 10);
    }

    [Fact]
    public void Every_sound_hakari_makes_has_notes_and_a_name()
    {
        var builtIn = Enum.GetValues<AlertSound>().Where(AlertSoundFiles.IsBuiltIn).ToList();

        Assert.Equal(9, builtIn.Count);
        Assert.DoesNotContain(AlertSound.Windows, builtIn);
        Assert.DoesNotContain(AlertSound.Custom, builtIn);
    }

    [Theory]
    [InlineData(Timbre.Warm)]
    [InlineData(Timbre.Pure)]
    [InlineData(Timbre.Bell)]
    [InlineData(Timbre.Wood)]
    public void Every_timbre_and_a_sliding_pitch_stay_within_range(Timbre timbre)
    {
        var wav = ToneSynth.Wav(
        [
            new Note(300, TimeSpan.Zero, TimeSpan.FromMilliseconds(300), timbre, 900),
            new Note(450, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(300), timbre),
        ]);

        var loudest = Enumerable.Range(0, (wav.Length - HeaderBytes) / BytesPerSample)
            .Max(index => Math.Abs((int)BitConverter.ToInt16(wav, HeaderBytes + index * 2)));
        Assert.InRange(loudest, short.MaxValue / 4, short.MaxValue - 1);
    }

    [Fact]
    public void Plays_no_file_for_windows_sound_no_sound_or_a_missing_pick()
    {
        Assert.Null(AlertSoundFiles.PathOf(new HakariSettings { AlertSound = AlertSound.Windows }));
        Assert.Null(AlertSoundFiles.PathOf(new HakariSettings { AlertSound = AlertSound.None }));
        Assert.Null(AlertSoundFiles.PathOf(new HakariSettings
        {
            AlertSound = AlertSound.Custom,
            AlertSoundFile = @"C:\nowhere\gone.wav",
        }));
    }

    [Fact]
    public void Plays_a_picked_file_that_is_still_there()
    {
        using var directory = new TemporaryDirectory();
        var file = directory.Combine("mine.wav");
        File.WriteAllBytes(file, [1, 2, 3]);

        var settings = new HakariSettings { AlertSound = AlertSound.Custom, AlertSoundFile = file };

        Assert.Equal(file, AlertSoundFiles.PathOf(settings));
    }

    [Fact]
    public void Accepts_common_sound_formats_only()
    {
        Assert.True(AlertSoundFiles.IsSupported(@"C:\a\Bell.MP3"));
        Assert.True(AlertSoundFiles.IsSupported(@"C:\a\bell.wav"));
        Assert.False(AlertSoundFiles.IsSupported(@"C:\a\bell.exe"));
    }

    [Fact]
    public void Keeps_the_volume_between_silent_and_full()
    {
        Assert.Equal(0, AlertSoundFiles.Usable(-5));
        Assert.Equal(100, AlertSoundFiles.Usable(250));
        Assert.Equal(60, AlertSoundFiles.Usable(60));
    }
}
