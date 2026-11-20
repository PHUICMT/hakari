using System.Text.Json;
using Hakari.Core.Configuration;

namespace Hakari.Core.SessionNotes;

/// <summary>A session's remembered steps and when it was last looked at.</summary>
public sealed record SessionNoteEntry(DateTimeOffset UpdatedAt, Dictionary<string, int> Levels);

/// <summary>
/// What each session has been told, kept in a small file so each hook call, a new process,
/// knows. Sessions not heard from for a few days are dropped. Two hooks writing at once can
/// at worst repeat one note.
/// </summary>
public sealed class SessionNoteMemory(string path)
{
    private const string FileName = "session-notes.json";
    private static readonly TimeSpan KeepFor = TimeSpan.FromDays(3);

    public static SessionNoteMemory Default { get; } =
        new(Path.Combine(HakariPaths.DataDirectory, FileName));

    public IReadOnlyDictionary<string, int> Known(string sessionId) =>
        Load().TryGetValue(sessionId, out var entry) ? entry.Levels : new Dictionary<string, int>();

    /// <summary>Whether this session was looked at less than <paramref name="span"/> ago.</summary>
    public bool LookedAtWithin(string sessionId, DateTimeOffset now, TimeSpan span) =>
        Load().TryGetValue(sessionId, out var entry) && now - entry.UpdatedAt < span;

    public void Remember(
        string sessionId,
        IReadOnlyDictionary<string, int> levels,
        DateTimeOffset now)
    {
        var all = Load()
            .Where(entry => now - entry.Value.UpdatedAt < KeepFor)
            .ToDictionary(entry => entry.Key, entry => entry.Value);
        all[sessionId] = new SessionNoteEntry(now, new Dictionary<string, int>(levels));
        Save(all);
    }

    private Dictionary<string, SessionNoteEntry> Load()
    {
        try
        {
            return File.Exists(path)
                && JsonSerializer.Deserialize<Dictionary<string, SessionNoteEntry>>(
                    File.ReadAllText(path)) is { } all
                ? all
                : [];
        }
        catch (Exception exception) when (exception is IOException or JsonException
            or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private void Save(Dictionary<string, SessionNoteEntry> all)
    {
        var temporary = $"{path}.{Environment.ProcessId}.tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(temporary, JsonSerializer.Serialize(all));
            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException)
        {
            TryDelete(temporary);
        }
    }

    private static void TryDelete(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException)
        {
        }
    }
}
