namespace Hakari.Core.SessionNotes;

/// <summary>
/// The steps at which a session hears about a limit: nothing below the share the user chose,
/// then one note for each step after it, so a session is not told the same thing on every
/// tool call.
/// </summary>
public static class NoteLevel
{
    public const int None = 0;
    public const int Full = 100;
    public const int DefaultFrom = 80;

    /// <summary>The shares offered in Settings for the first note.</summary>
    public static readonly int[] Choices = [50, 60, 70, 80, 90, 95];

    private static readonly int[] LaterSteps = [60, 70, 80, 90, 95, Full];

    public static int Of(int percent, int from) =>
        Steps(from).LastOrDefault(step => percent >= step, None);

    /// <summary>A share saved by hand that cannot be a step falls back to the default.</summary>
    public static int Usable(int from) => from is > None and <= Full ? from : DefaultFrom;

    private static IEnumerable<int> Steps(int from) =>
        LaterSteps.Where(step => step > Usable(from)).Prepend(Usable(from));
}
