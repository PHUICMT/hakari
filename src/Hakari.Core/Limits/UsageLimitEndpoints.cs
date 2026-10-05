namespace Hakari.Core.Limits;

/// <summary>
/// The endpoints Claude Code itself uses for /usage and for keeping its sign-in alive. Neither
/// is documented publicly, so both are isolated here and may need updating.
/// </summary>
public static class UsageLimitEndpoints
{
    public const string UsageUrl = "https://api.anthropic.com/api/oauth/usage";
    public const string TokenUrl = "https://console.anthropic.com/v1/oauth/token";
    public const string OAuthBetaHeader = "anthropic-beta";
    public const string OAuthBetaValue = "oauth-2025-04-20";

    /// <summary>Claude Code's public OAuth client id, required to refresh its tokens.</summary>
    public const string ClaudeCodeClientId = "9d1c250a-e61b-44d9-88ed-5944d1962f5e";
}
