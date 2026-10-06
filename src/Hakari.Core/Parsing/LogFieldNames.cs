namespace Hakari.Core.Parsing;

internal static class LogFieldNames
{
    public const string Type = "type";
    public const string Message = "message";
    public const string MessageId = "id";
    public const string Model = "model";
    public const string Usage = "usage";
    public const string RequestId = "requestId";
    public const string Timestamp = "timestamp";
    public const string SessionId = "sessionId";
    public const string WorkingDirectory = "cwd";
    public const string GitBranch = "gitBranch";
    public const string IsSidechain = "isSidechain";
    public const string Speed = "speed";
    public const string InputTokens = "input_tokens";
    public const string OutputTokens = "output_tokens";
    public const string CacheCreationInputTokens = "cache_creation_input_tokens";
    public const string CacheReadInputTokens = "cache_read_input_tokens";
    public const string CacheCreation = "cache_creation";
    public const string CacheWriteFiveMinutes = "ephemeral_5m_input_tokens";
    public const string CacheWriteOneHour = "ephemeral_1h_input_tokens";
    public const string InferenceGeography = "inference_geo";
    public const string ServerToolUse = "server_tool_use";
    public const string WebSearchRequests = "web_search_requests";
    public const string AiTitle = "aiTitle";
    public const string CustomTitle = "customTitle";
}
