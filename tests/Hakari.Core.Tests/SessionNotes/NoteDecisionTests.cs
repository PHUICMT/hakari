using Hakari.Core.Limits;
using Hakari.Core.SessionNotes;

namespace Hakari.Core.Tests.SessionNotes;

public class NoteDecisionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 14, 0, 0, TimeSpan.Zero);
    private static readonly TimeZoneInfo Bangkok =
        TimeZoneInfo.CreateCustomTimeZone("ICT", TimeSpan.FromHours(7), "ICT", "ICT");

    private static readonly Dictionary<string, int> NothingKnown = [];
    private const int DefaultFrom = NoteLevel.DefaultFrom;

    [Fact]
    public void Says_nothing_below_eighty_percent()
    {
        var decision = Decide(HookInput.UserPromptSubmit, NothingKnown, Session(79));

        Assert.Null(decision.Text);
        Assert.Empty(decision.Levels);
    }

    [Fact]
    public void Tells_the_share_and_the_reset_time_once_past_eighty()
    {
        var decision = Decide(HookInput.UserPromptSubmit, NothingKnown, Session(92));

        Assert.NotNull(decision.Text);
        Assert.Contains("5-hour limit is at 92%", decision.Text);
        Assert.Contains("resets 22:30 local time (in 1 h 30 min)", decision.Text);
        Assert.Contains("save progress", decision.Text);
    }

    [Fact]
    public void Does_not_repeat_a_step_already_told()
    {
        var first = Decide(HookInput.PostToolUse, NothingKnown, Session(92));

        var again = Decide(HookInput.PostToolUse, first.Levels, Session(93));

        Assert.Null(again.Text);
    }

    [Fact]
    public void Tells_again_when_the_limit_climbs_a_step()
    {
        var first = Decide(HookInput.PostToolUse, NothingKnown, Session(92));

        var climbed = Decide(HookInput.PostToolUse, first.Levels, Session(96));

        Assert.Contains("96%", climbed.Text);
    }

    [Fact]
    public void A_new_or_compacted_session_hears_it_again()
    {
        var first = Decide(HookInput.PostToolUse, NothingKnown, Session(92));

        var compacted = Decide(HookInput.SessionStart, first.Levels, Session(92));

        Assert.Contains("92%", compacted.Text);
    }

    [Fact]
    public void A_full_limit_says_to_stop_at_a_safe_point()
    {
        var decision = Decide(HookInput.UserPromptSubmit, NothingKnown, Session(100));

        Assert.Contains("is full", decision.Text);
        Assert.Contains("stop at a safe point", decision.Text);
    }

    [Fact]
    public void Says_when_a_limit_it_told_about_has_reset()
    {
        var full = Decide(HookInput.PostToolUse, NothingKnown, Session(100));

        var reset = Decide(HookInput.UserPromptSubmit, full.Levels, Session(3));

        Assert.Contains("5-hour limit has reset and is at 3%", reset.Text);
        Assert.Empty(reset.Levels);
    }

    [Fact]
    public void Names_a_model_limit_and_gives_a_weekday_for_a_later_reset()
    {
        var weekly = new UsageLimit(
            "weekly_scoped", "weekly", 85, "warning", Now.AddDays(2), "Fable", true);

        var decision = Decide(HookInput.UserPromptSubmit, NothingKnown, weekly);

        Assert.Contains("weekly Fable limit is at 85%", decision.Text);
        Assert.Contains("resets Sun 21:00", decision.Text);
    }

    [Fact]
    public void Says_when_old_numbers_were_read()
    {
        var snapshot = new LimitSnapshot(
            [Session(90)], null, Now.AddHours(-1), LimitFreshness.Live);

        var decision = NoteDecision.Decide(
            HookInput.UserPromptSubmit, snapshot, NothingKnown, DefaultFrom, Now, Bangkok);

        Assert.Contains("read at 20:00", decision.Text);
    }

    [Fact]
    public void Starts_at_the_share_the_user_chose_then_steps_up()
    {
        var first = Decide(HookInput.PostToolUse, NothingKnown, 50, Session(55));
        var same = Decide(HookInput.PostToolUse, first.Levels, 50, Session(58));
        var next = Decide(HookInput.PostToolUse, first.Levels, 50, Session(61));

        Assert.Contains("55%", first.Text);
        Assert.Null(same.Text);
        Assert.Contains("61%", next.Text);
        Assert.Null(Decide(HookInput.PostToolUse, NothingKnown, 90, Session(85)).Text);
    }

    [Fact]
    public void A_share_saved_by_hand_that_makes_no_sense_uses_eighty()
    {
        Assert.Equal(NoteLevel.DefaultFrom, NoteLevel.Usable(0));
        Assert.Equal(NoteLevel.DefaultFrom, NoteLevel.Usable(250));
        Assert.Equal(65, NoteLevel.Usable(65));
    }

    private static UsageLimit Session(int percent) =>
        new("session", "session", percent, "warning", Now.AddMinutes(90), null, true);

    private static NoteDecision Decide(
        string eventName,
        IReadOnlyDictionary<string, int> known,
        params UsageLimit[] limits) =>
        Decide(eventName, known, DefaultFrom, limits);

    private static NoteDecision Decide(
        string eventName,
        IReadOnlyDictionary<string, int> known,
        int from,
        params UsageLimit[] limits) =>
        NoteDecision.Decide(
            eventName,
            new LimitSnapshot(limits, null, Now, LimitFreshness.Live),
            known,
            from,
            Now,
            Bangkok);
}
