namespace Hakari.Core.Sound;

/// <summary>The colour of a note: which overtones sound with its pitch.</summary>
public enum Timbre
{
    /// <summary>The pitch with two soft harmonics; round and plain.</summary>
    Warm,

    /// <summary>The pitch alone; clean, like a sine beep.</summary>
    Pure,

    /// <summary>Overtones that are not whole multiples, as a struck bell rings.</summary>
    Bell,

    /// <summary>A strong fourth harmonic that dies fast, as a wooden bar sounds.</summary>
    Wood,
}

/// <summary>One note: a pitch with overtones that fades out, starting at a delay.</summary>
/// <param name="Frequency">The pitch, in hertz.</param>
/// <param name="Start">When the note begins, from the start of the sound.</param>
/// <param name="Length">How long until it has faded out.</param>
/// <param name="EndFrequency">A pitch to slide to over the note, or null to hold it.</param>
public sealed record Note(
    double Frequency,
    TimeSpan Start,
    TimeSpan Length,
    Timbre Timbre = Timbre.Warm,
    double? EndFrequency = null);

/// <summary>
/// Makes Hakari's alert sounds as WAV data, so no sound files have to ship and none can be
/// missing. Each note rises in a few milliseconds and fades exponentially, which keeps it soft.
/// </summary>
public static class ToneSynth
{
    public const int SampleRate = 44_100;

    private const short BitsPerSample = 16;
    private const short Channels = 1;
    private const int HeaderBytes = 44;
    private const int FormatChunkBytes = 16;
    private const short PcmFormat = 1;
    private const int BytesPerSample = BitsPerSample / 8;

    /// <summary>Loud enough to hear, with room so overlapping notes never clip.</summary>
    private const double Peak = 0.5 * short.MaxValue;

    private static readonly TimeSpan Attack = TimeSpan.FromMilliseconds(6);

    /// <summary>
    /// Overtones as (multiple of the pitch, share of its loudness, how much faster than the
    /// note it fades: high partials of a bell or a wooden bar die first).
    /// </summary>
    private static readonly Dictionary<Timbre, (double Multiple, double Share, double Fade)[]>
        Overtones = new()
        {
            [Timbre.Warm] = [(1, 1, 1), (2, 0.25, 1), (3, 0.08, 1)],
            [Timbre.Pure] = [(1, 1, 1)],
            [Timbre.Bell] = [(1, 1, 1), (2.76, 0.5, 2), (5.4, 0.25, 3), (8.93, 0.12, 4)],
            [Timbre.Wood] = [(1, 1, 1), (4, 0.4, 4), (9.2, 0.1, 6)],
        };

    /// <summary>The fade reaches about 1% of the start by the end of each note.</summary>
    private const double FadeSteepness = 4.6;

    public static byte[] Wav(IReadOnlyList<Note> notes)
    {
        var samples = Mix(notes);
        using var stream = new MemoryStream(HeaderBytes + samples.Length * BytesPerSample);
        using var writer = new BinaryWriter(stream);
        WriteHeader(writer, samples.Length);
        foreach (var sample in samples)
        {
            writer.Write(sample);
        }

        writer.Flush();
        return stream.ToArray();
    }

    private static short[] Mix(IReadOnlyList<Note> notes)
    {
        var end = notes.Max(note => note.Start + note.Length);
        var mixed = new double[(int)(end.TotalSeconds * SampleRate)];
        foreach (var note in notes)
        {
            AddNote(mixed, note);
        }

        var loudest = Math.Max(1, mixed.Max(Math.Abs));
        return [.. mixed.Select(value => (short)(value / loudest * Peak))];
    }

    private static void AddNote(double[] mixed, Note note)
    {
        var first = (int)(note.Start.TotalSeconds * SampleRate);
        var count = (int)(note.Length.TotalSeconds * SampleRate);
        var attack = Attack.TotalSeconds * SampleRate;
        var overtones = Overtones[note.Timbre];
        for (var position = 0; position < count && first + position < mixed.Length; position++)
        {
            var rise = Math.Min(1, position / attack);
            var progress = (double)position / count;
            var phase = Phase(note, (double)position / SampleRate);
            var wave = overtones.Sum(overtone =>
                overtone.Share
                * Math.Exp(-FadeSteepness * overtone.Fade * progress)
                * Math.Sin(phase * overtone.Multiple));
            mixed[first + position] += wave * rise;
        }
    }

    /// <summary>
    /// The angle the wave has turned through by <paramref name="seconds"/>; a sliding pitch
    /// adds up its changing speed, so the slide has no clicks.
    /// </summary>
    private static double Phase(Note note, double seconds)
    {
        var slide = (note.EndFrequency ?? note.Frequency) - note.Frequency;
        var length = note.Length.TotalSeconds;
        return 2 * Math.PI * (note.Frequency * seconds + slide * seconds * seconds / (2 * length));
    }

    private static void WriteHeader(BinaryWriter writer, int sampleCount)
    {
        var dataBytes = sampleCount * BytesPerSample;
        writer.Write("RIFF"u8);
        writer.Write(HeaderBytes - 8 + dataBytes);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(FormatChunkBytes);
        writer.Write(PcmFormat);
        writer.Write(Channels);
        writer.Write(SampleRate);
        writer.Write(SampleRate * Channels * BytesPerSample);
        writer.Write((short)(Channels * BytesPerSample));
        writer.Write(BitsPerSample);
        writer.Write("data"u8);
        writer.Write(dataBytes);
    }
}
