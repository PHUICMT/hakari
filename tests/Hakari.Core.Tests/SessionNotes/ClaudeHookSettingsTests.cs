using System.Text.Json;
using System.Text.Json.Nodes;
using Hakari.Core.SessionNotes;

namespace Hakari.Core.Tests.SessionNotes;

public class ClaudeHookSettingsTests
{
    private const string Command = "\"C:/Apps/Hakari/Hakari.exe\" --usage-note";
    private const string MovedCommand = "\"D:/Hakari/Hakari.exe\" --usage-note";

    private const string OtherSettings = """
        {
          "model": "opus",
          "hooks": {
            "PostToolUse": [
              { "matcher": "Edit", "hooks": [ { "type": "command", "command": "format.sh" } ] }
            ]
          }
        }
        """;

    [Fact]
    public void Writes_the_command_with_forward_slashes_and_the_flag()
    {
        Assert.Equal(Command, ClaudeHookSettings.CommandFor(@"C:\Apps\Hakari\Hakari.exe"));
    }

    [Fact]
    public void Adds_one_handler_for_each_event_to_an_empty_file()
    {
        var json = ClaudeHookSettings.WithHook(null, Command);

        Assert.True(ClaudeHookSettings.HasHook(json, Command));
        var hooks = JsonNode.Parse(json)!["hooks"]!.AsObject();
        Assert.Equal(["SessionStart", "UserPromptSubmit", "PostToolUse"], hooks.Select(e => e.Key));
    }

    [Fact]
    public void Keeps_the_users_own_settings_and_hooks()
    {
        var json = ClaudeHookSettings.WithHook(OtherSettings, Command);

        var root = JsonNode.Parse(json)!;
        Assert.Equal("opus", root["model"]!.GetValue<string>());
        Assert.Contains("format.sh", json);
        Assert.Equal(2, root["hooks"]!["PostToolUse"]!.AsArray().Count);
    }

    [Fact]
    public void Adding_again_replaces_an_old_path_instead_of_doubling()
    {
        var once = ClaudeHookSettings.WithHook(OtherSettings, Command);

        var moved = ClaudeHookSettings.WithHook(once, MovedCommand);

        Assert.True(ClaudeHookSettings.HasHook(moved, MovedCommand));
        Assert.DoesNotContain("C:/Apps", moved);
        Assert.False(ClaudeHookSettings.HasHook(once, MovedCommand));
    }

    [Fact]
    public void Removing_leaves_the_file_as_it_was_before()
    {
        var added = ClaudeHookSettings.WithHook(OtherSettings, Command);

        var removed = ClaudeHookSettings.WithoutHook(added);

        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(OtherSettings), JsonNode.Parse(removed)));
        Assert.False(ClaudeHookSettings.HasAnyHook(removed));
    }

    [Fact]
    public void Removing_from_a_file_with_only_hakari_drops_the_hooks_list()
    {
        var removed = ClaudeHookSettings.WithoutHook(ClaudeHookSettings.WithHook("{}", Command));

        Assert.Equal("{}", removed);
    }

    [Fact]
    public void Keeps_thai_text_and_quotes_readable()
    {
        var json = ClaudeHookSettings.WithHook("""{ "note": "ทดสอบ <a> & 'b'" }""", Command);

        Assert.Contains("ทดสอบ <a> & 'b'", json);
    }

    [Fact]
    public void Leaves_a_file_that_is_not_json_alone()
    {
        Assert.ThrowsAny<JsonException>(() => ClaudeHookSettings.WithHook("{ broken", Command));
        Assert.ThrowsAny<JsonException>(() => ClaudeHookSettings.WithHook("[1, 2]", Command));
    }
}
