using System.Text.Json;

namespace Hakari.Core.Tests.Support;

internal static class SampleLogLines
{
    public const string User = """{"type":"user","message":{"role":"user","content":"hello"}}""";

    public static string Assistant(
        string messageId = "msg_1",
        string requestId = "req_1",
        string model = "claude-opus-5",
        string timestamp = "2026-10-05T06:45:53.637Z",
        bool isSidechain = false)
    {
        var line = new Dictionary<string, object>
        {
            ["type"] = "assistant",
            ["requestId"] = requestId,
            ["sessionId"] = "session_1",
            ["timestamp"] = timestamp,
            ["cwd"] = @"D:\work",
            ["gitBranch"] = "main",
            ["isSidechain"] = isSidechain,
            ["message"] = new Dictionary<string, object>
            {
                ["id"] = messageId,
                ["model"] = model,
                ["usage"] = SampleUsage(),
            },
        };

        return JsonSerializer.Serialize(line);
    }

    private static Dictionary<string, object> SampleUsage() => new()
    {
        ["input_tokens"] = 2,
        ["output_tokens"] = 433,
        ["cache_creation_input_tokens"] = 69438,
        ["cache_read_input_tokens"] = 32048,
        ["cache_creation"] = new Dictionary<string, object>
        {
            ["ephemeral_1h_input_tokens"] = 69438,
            ["ephemeral_5m_input_tokens"] = 0,
        },
        ["speed"] = "standard",
    };
}
