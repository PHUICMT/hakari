namespace Hakari.Core.Limits.Credentials;

internal static class CredentialFieldNames
{
    public const string FileName = ".credentials.json";
    public const string OAuthSection = "claudeAiOauth";
    public const string AccessToken = "accessToken";
    public const string RefreshToken = "refreshToken";
    public const string ExpiresAt = "expiresAt";
    public const string RefreshTokenExpiresAt = "refreshTokenExpiresAt";
    public const string SubscriptionType = "subscriptionType";
    public const string RateLimitTier = "rateLimitTier";
}
