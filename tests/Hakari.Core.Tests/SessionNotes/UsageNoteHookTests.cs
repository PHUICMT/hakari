using System.Text.Json.Nodes;
using Hakari.Core.Limits;
using Hakari.Core.SessionNotes;
using Hakari.Core.Settings;
using Hakari.Core.Tests.Support;

namespace Hakari.Core.Tests.SessionNotes;

public class UsageNoteHookTests : IDisposable
{
    private const string AccountId = "account-1";
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 14, 0, 0, TimeSpan.Zero);

    private readonly TemporaryDirectory directory = new();
    private readonly SessionNoteMemory memory;

    public UsageNoteHookTests() =>
        memory = new SessionNoteMemory(directory.Combine("session-notes.json"));

    public void Dispose() => directory.Dispose();

    [Fact]
    public void Reads_only_the_hooks_own_fields()
    {
        var input = HookInput.Parse("""
            {"session_id":"s1","transcript_path":"C:/Users/a/.claude/projects/p/s1.jsonl",
             "hook_event_name":"UserPromptSubmit","prompt":"secret"}
            """);

        Assert.Equal(new HookInput("UserPromptSubmit", "s1",
            "C:/Users/a/.claude/projects/p/s1.jsonl", null), input);
        Assert.Null(HookInput.Parse("not json"));
        Assert.Null(HookInput.Parse("""{"session_id":"s1"}"""));
    }

    [Fact]
    public void Finds_the_config_folder_above_the_projects_folder()
    {
        Assert.Equal(
            @"C:\Users\a\.claude-work",
            SessionAccount.ConfigDirectoryOf(@"C:\Users\a\.claude-work\projects\D--x\s.jsonl"));
        Assert.Null(SessionAccount.ConfigDirectoryOf(@"C:\temp\s.jsonl"));
    }

    [Fact]
    public void Prints_the_note_in_the_form_claude_code_adds_to_the_context()
    {
        var answer = Respond(Input(HookInput.PostToolUse), Snapshot(91, Now));

        var output = JsonNode.Parse(answer.Output!)!["hookSpecificOutput"]!;
        Assert.Equal("PostToolUse", output["hookEventName"]!.GetValue<string>());
        Assert.Contains("91%", output["additionalContext"]!.GetValue<string>());
    }

    [Fact]
    public void Remembers_what_each_session_heard()
    {
        Respond(Input(HookInput.PostToolUse), Snapshot(91, Now));

        Assert.Null(Respond(Input(HookInput.PostToolUse), Snapshot(91, Now)).Output);
        Assert.NotNull(Respond(Input(HookInput.PostToolUse, "s2"), Snapshot(91, Now)).Output);
    }

    [Fact]
    public void A_tool_call_right_after_a_look_stops_early_but_a_prompt_does_not()
    {
        Respond(Input(HookInput.PostToolUse), Snapshot(50, Now));

        Assert.True(UsageNoteHook.IsTooSoon(Input(HookInput.PostToolUse), memory, Now));
        Assert.False(UsageNoteHook.IsTooSoon(Input(HookInput.UserPromptSubmit), memory, Now));
        Assert.False(UsageNoteHook.IsTooSoon(
            Input(HookInput.PostToolUse), memory, Now.AddMinutes(1)));
    }

    [Fact]
    public void Says_nothing_for_an_account_whose_limits_are_turned_off()
    {
        var settings = new HakariSettings { LimitsOffAccounts = [AccountId] };

        var answer = UsageNoteHook.Respond(
            Input(HookInput.PostToolUse), AccountId, settings, Snapshot(95, Now), memory, Now,
            TimeZoneInfo.Utc);

        Assert.Equal(HookAnswer.Nothing, answer);
    }

    [Fact]
    public void Asks_for_fresh_limits_only_when_close_and_old()
    {
        Assert.False(Respond(Input(HookInput.PostToolUse), Snapshot(75, Now)).WantsFreshLimits);
        Assert.True(Respond(
            Input(HookInput.PostToolUse), Snapshot(75, Now.AddMinutes(-5))).WantsFreshLimits);
        Assert.False(Respond(
            Input(HookInput.PostToolUse), Snapshot(40, Now.AddMinutes(-5))).WantsFreshLimits);
    }

    private HookAnswer Respond(HookInput input, LimitSnapshot snapshot) =>
        UsageNoteHook.Respond(
            input, AccountId, new HakariSettings(), snapshot, memory, Now, TimeZoneInfo.Utc);

    private static HookInput Input(string eventName, string sessionId = "s1") =>
        new(eventName, sessionId, @"C:\Users\a\.claude\projects\p\s.jsonl", null);

    private static LimitSnapshot Snapshot(int percent, DateTimeOffset fetchedAt) =>
        new(
            [new UsageLimit("session", "session", percent, "warning", Now.AddHours(2), null, true)],
            null,
            fetchedAt,
            LimitFreshness.Live);
}
