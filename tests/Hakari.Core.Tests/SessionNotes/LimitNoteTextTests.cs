using Hakari.Core.Limits;
using Hakari.Core.SessionNotes;

namespace Hakari.Core.Tests.SessionNotes;

public class LimitNoteTextTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 14, 0, 0, TimeSpan.Zero);
    private static readonly TimeZoneInfo Bangkok =
        TimeZoneInfo.CreateCustomTimeZone("ICT", TimeSpan.FromHours(7), "ICT", "ICT");

    private static readonly UsageLimit Session =
        new("session", "session", 92, "warning", Now.AddMinutes(90), null, true);

    private static readonly UsageLimit Weekly =
        new("weekly_all", "weekly", 85, "warning", Now.AddDays(2), null, true);

    [Fact]
    public void An_empty_template_gives_hakaris_own_text()
    {
        var empty = Note("");
        var given = Note(LimitNoteText.DefaultTemplate);

        Assert.Equal(given, empty);
        Assert.StartsWith("Hakari usage note for this Claude account: the 5-hour limit", empty);
    }

    [Fact]
    public void Fills_each_value_from_the_fullest_limit()
    {
        var text = Note("{limit} {state} at {percent}, resets {resets} (in {in}).");

        Assert.Equal("5-hour limit nearly full at 92%, resets 22:30 (in 1 h 30 min).", text);
    }

    [Fact]
    public void Lists_every_limit_and_reads_names_in_any_case()
    {
        var text = Note("Limits: {LIMITS}");

        Assert.Contains("the 5-hour limit is at 92%", text);
        Assert.Contains("the weekly limit is at 85%", text);
    }

    [Fact]
    public void Leaves_unknown_braces_as_written()
    {
        Assert.Equal("Stop {soon}, 92%", Note("Stop {soon}, {percent}"));
    }

    [Fact]
    public void Cuts_a_very_long_note()
    {
        var text = Note(new string('x', LimitNoteText.MaximumLength + 500));

        Assert.Equal(LimitNoteText.MaximumLength, text.Length);
    }

    [Fact]
    public void The_settings_sample_uses_the_users_text()
    {
        var sample = LimitNoteText.Sample("{limit}: {percent}", Now, Bangkok);

        Assert.Equal("5-hour limit: 92%", sample);
    }

    private static string Note(string template) =>
        LimitNoteText.NearlyFull([Session, Weekly], Now, Now, Bangkok, template);
}
