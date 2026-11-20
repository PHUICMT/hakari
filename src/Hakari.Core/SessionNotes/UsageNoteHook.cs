using System.Text.Json;
using System.Text.Json.Nodes;
using Hakari.Core.Configuration;
using Hakari.Core.Indexing;
using Hakari.Core.Limits;
using Hakari.Core.Settings;
using Microsoft.Data.Sqlite;

namespace Hakari.Core.SessionNotes;

/// <param name="Output">What to print for Claude Code, or null to print nothing.</param>
/// <param name="WantsFreshLimits">
/// The limits are close to full but were read a while ago, so Hakari.exe should read them
/// again; this answer still uses what is known, so the session never waits for the network.
/// </param>
public sealed record HookAnswer(string? Output, bool WantsFreshLimits)
{
    public static HookAnswer Nothing { get; } = new(null, false);
}

/// <summary>
/// Runs as a Claude Code hook: tells the session when its account's usage limit is nearly
/// full and when it resets, so the work can be saved first. It reads only what Hakari already
/// keeps on this PC and never the session's prompts or tool output.
/// </summary>
public static class UsageNoteHook
{
    private const string OutputField = "hookSpecificOutput";
    private const string EventField = "hookEventName";
    private const string ContextField = "additionalContext";

    private static readonly TimeSpan ToolCallSpacing = TimeSpan.FromSeconds(30);

    /// <summary>Readings this old are asked for again once a limit gets close.</summary>
    private static readonly TimeSpan FreshFor = TimeSpan.FromMinutes(2);

    /// <summary>This far below the first note, a fresher reading is worth asking for.</summary>
    private const int CloseMargin = 10;

    public static HookAnswer Run(string standardInput, DateTimeOffset now)
    {
        if (HookInput.Parse(standardInput) is not { } input
            || IsTooSoon(input, SessionNoteMemory.Default, now))
        {
            return HookAnswer.Nothing;
        }

        var settings = SettingsStore.Default.Load();
        if (!settings.TellSessionsAboutLimits
            || SessionAccount.IdOf(input.TranscriptPath) is not { } accountId)
        {
            return HookAnswer.Nothing;
        }

        return Respond(
            input,
            accountId,
            settings,
            LoadSnapshot(accountId),
            SessionNoteMemory.Default,
            now,
            TimeZoneInfo.Local);
    }

    public static HookAnswer Respond(
        HookInput input,
        string accountId,
        HakariSettings settings,
        LimitSnapshot? snapshot,
        SessionNoteMemory memory,
        DateTimeOffset now,
        TimeZoneInfo zone)
    {
        // An account whose limits were turned off shows none, not its last ones.
        if (snapshot is null || settings.LimitsOffAccounts.Contains(accountId))
        {
            return HookAnswer.Nothing;
        }

        var from = NoteLevel.Usable(settings.SessionNotesFrom);
        var decision = NoteDecision.Decide(
            input.EventName,
            snapshot,
            memory.Known(input.SessionId),
            from,
            now,
            zone);
        memory.Remember(input.SessionId, decision.Levels, now);

        var wantsFresh = now - snapshot.FetchedAt > FreshFor
            && snapshot.ProjectedTo(now).Limits
                .Any(limit => limit.Percent >= from - CloseMargin);
        return new HookAnswer(
            decision.Text is { } text ? Format(input.EventName, text) : null,
            wantsFresh);
    }

    /// <summary>
    /// Tool calls come seconds apart and limits are read a minute apart at best, so a tool
    /// call soon after the last look stops here, before anything slow is read. A session
    /// start or a prompt is always looked at.
    /// </summary>
    public static bool IsTooSoon(HookInput input, SessionNoteMemory memory, DateTimeOffset now) =>
        input.EventName == HookInput.PostToolUse
        && memory.LookedAtWithin(input.SessionId, now, ToolCallSpacing);

    /// <summary>The form Claude Code adds to the model's context for every event.</summary>
    public static string Format(string eventName, string text) =>
        new JsonObject
        {
            [OutputField] = new JsonObject
            {
                [EventField] = eventName,
                [ContextField] = text,
            },
        }.ToJsonString();

    private static LimitSnapshot? LoadSnapshot(string accountId)
    {
        if (!File.Exists(HakariPaths.DefaultIndexPath))
        {
            return null;
        }

        try
        {
            using var store = IndexStore.OpenReadOnly(HakariPaths.DefaultIndexPath);
            return new LimitCache(store).Load(accountId);
        }
        catch (Exception exception) when (exception is SqliteException or IOException
            or UnauthorizedAccessException or InvalidOperationException or JsonException)
        {
            return null;
        }
    }
}
