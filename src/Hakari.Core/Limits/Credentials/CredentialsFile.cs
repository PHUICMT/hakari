using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hakari.Core.Sources;

namespace Hakari.Core.Limits.Credentials;

/// <summary>Reads, and when refreshing writes back, Claude Code's credentials file.</summary>
public sealed class CredentialsFile(string path)
{
    private const string TemporarySuffix = ".hakari-tmp";

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = false };

    public string Path { get; } = path;

    public static CredentialsFile For(UsageSource source) =>
        new(System.IO.Path.Combine(source.ConfigDirectory, CredentialFieldNames.FileName));

    public bool Exists => File.Exists(Path);

    /// <summary>Returns the credentials and a fingerprint of the file they came from.</summary>
    public (OAuthCredentials Credentials, string Fingerprint)? Read()
    {
        try
        {
            var bytes = File.ReadAllBytes(Path);
            var root = JsonNode.Parse(bytes) as JsonObject;
            var section = root?[CredentialFieldNames.OAuthSection] as JsonObject;
            var credentials = section is null ? null : ToCredentials(section);
            return credentials is null ? null : (credentials, Fingerprint(bytes));
        }
        catch (Exception exception) when (IsFileProblem(exception))
        {
            return null;
        }
    }

    /// <summary>
    /// Replaces the tokens while keeping every other field, through a temporary file and an
    /// atomic move. Gives up if the file changed since <paramref name="expectedFingerprint"/>,
    /// because then Claude Code refreshed it itself and its tokens must win.
    /// </summary>
    public bool TryWriteTokens(TokenGrant grant, string expectedFingerprint)
    {
        try
        {
            var bytes = File.ReadAllBytes(Path);
            if (Fingerprint(bytes) != expectedFingerprint)
            {
                return false;
            }

            var root = (JsonNode.Parse(bytes) as JsonObject)!;
            var section = (root[CredentialFieldNames.OAuthSection] as JsonObject)!;
            ApplyGrant(section, grant);

            var temporaryPath = Path + TemporarySuffix;
            File.WriteAllText(temporaryPath, root.ToJsonString(WriteOptions));
            File.Move(temporaryPath, Path, overwrite: true);
            return true;
        }
        catch (Exception exception) when (IsFileProblem(exception))
        {
            return false;
        }
    }

    private static void ApplyGrant(JsonObject section, TokenGrant grant)
    {
        section[CredentialFieldNames.AccessToken] = grant.AccessToken;
        section[CredentialFieldNames.ExpiresAt] = grant.ExpiresAt.ToUnixTimeMilliseconds();
        if (grant.RefreshToken is { } refreshToken)
        {
            section[CredentialFieldNames.RefreshToken] = refreshToken;
        }
    }

    private static OAuthCredentials? ToCredentials(JsonObject section)
    {
        var accessToken = section[CredentialFieldNames.AccessToken]?.GetValue<string>();
        var expiresAt = ReadMilliseconds(section, CredentialFieldNames.ExpiresAt);
        if (accessToken is null || expiresAt is null)
        {
            return null;
        }

        return new OAuthCredentials(
            AccessToken: accessToken,
            RefreshToken: section[CredentialFieldNames.RefreshToken]?.GetValue<string>(),
            ExpiresAt: expiresAt.Value,
            RefreshTokenExpiresAt: ReadRefreshExpiry(section),
            SubscriptionType: section[CredentialFieldNames.SubscriptionType]?.GetValue<string>(),
            RateLimitTier: section[CredentialFieldNames.RateLimitTier]?.GetValue<string>());
    }

    /// <summary>Claude Code has written this field both as milliseconds and as a date.</summary>
    private static DateTimeOffset? ReadRefreshExpiry(JsonObject section)
    {
        var node = section[CredentialFieldNames.RefreshTokenExpiresAt];
        if (node is JsonValue value && value.TryGetValue<string>(out var text)
            && DateTimeOffset.TryParse(text, out var parsed))
        {
            return parsed;
        }

        return ReadMilliseconds(section, CredentialFieldNames.RefreshTokenExpiresAt);
    }

    private static DateTimeOffset? ReadMilliseconds(JsonObject section, string field) =>
        section[field] is JsonValue value && value.TryGetValue<long>(out var milliseconds)
            ? DateTimeOffset.FromUnixTimeMilliseconds(milliseconds)
            : null;

    private static string Fingerprint(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static bool IsFileProblem(Exception exception) =>
        exception is IOException
            or UnauthorizedAccessException
            or JsonException
            or InvalidOperationException
            or FormatException;
}
