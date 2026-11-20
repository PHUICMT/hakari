using System.Text.Json;
using System.Text.Json.Nodes;
using Hakari.Core.SessionNotes;

namespace Hakari.Core.Tests.SessionNotes;

public class ClaudeHookSettingsTests
{
    private const string ExecutablePath = @"C:\Apps\Hakari\Hakari.exe";
    private const string MovedPath = @"D:\Hakari\Hakari.exe";

    private static readonly HookCommand Command =
        ClaudeHookSettings.CommandFor(ExecutablePath, HookShell.Bash);

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
    public void A_bash_command_runs_hakari_only_while_it_is_there_and_always_succeeds()
    {
        Assert.Equal(
            "[ -e \"C:/Apps/Hakari/Hakari.exe\" ] && \"C:/Apps/Hakari/Hakari.exe\" "
                + "--usage-note; exit 0",
            Command.Command);
    }

    [Fact]
    public void A_powershell_command_checks_the_path_and_calls_it_with_the_call_operator()
    {
        var command = ClaudeHookSettings.CommandFor(@"C:\O'Neil\Hakari.exe", HookShell.PowerShell);

        Assert.Equal(
            "if (Test-Path -LiteralPath 'C:\\O''Neil\\Hakari.exe') "
                + "{ & 'C:\\O''Neil\\Hakari.exe' --usage-note }",
            command.Command);
    }

    [Fact]
    public void Adds_one_handler_for_each_event_with_its_shell()
    {
        var json = ClaudeHookSettings.WithHook(null, Command);

        Assert.True(ClaudeHookSettings.HasHook(json, Command));
        var hooks = JsonNode.Parse(json)!["hooks"]!.AsObject();
        Assert.Equal(["SessionStart", "UserPromptSubmit", "PostToolUse"], hooks.Select(e => e.Key));
        Assert.Equal("bash", hooks["SessionStart"]![0]!["hooks"]![0]!["shell"]!.GetValue<string>());
    }

    [Fact]
    public void A_changed_shell_counts_as_not_installed()
    {
        var json = ClaudeHookSettings.WithHook(null, Command);

        var powerShell = ClaudeHookSettings.CommandFor(ExecutablePath, HookShell.PowerShell);

        Assert.False(ClaudeHookSettings.HasHook(json, powerShell));
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
        var moved = ClaudeHookSettings.CommandFor(MovedPath, HookShell.Bash);
        var once = ClaudeHookSettings.WithHook(OtherSettings, Command);

        var again = ClaudeHookSettings.WithHook(once, moved);

        Assert.True(ClaudeHookSettings.HasHook(again, moved));
        Assert.DoesNotContain("C:/Apps", again);
        Assert.False(ClaudeHookSettings.HasHook(once, moved));
    }

    [Fact]
    public void Replaces_a_hook_written_by_the_first_version()
    {
        var escapedNew = Command.Command.Replace("\"", "\\\"");
        var escapedFirst = "\\\"C:/Apps/Hakari/Hakari.exe\\\" --usage-note";
        var first = ClaudeHookSettings.WithHook(null, Command).Replace(escapedNew, escapedFirst);

        var updated = ClaudeHookSettings.WithHook(first, Command);

        Assert.True(ClaudeHookSettings.HasHook(updated, Command));
        Assert.Equal(3, updated.Split("--usage-note").Length - 1);
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
