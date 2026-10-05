using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Hakari.Core.Limits.Credentials;
using Hakari.Core.Parsing;

namespace Hakari.Core.Limits;

public sealed class UsageLimitClient(HttpClient httpClient, TimeProvider timeProvider)
{
    private const string BearerScheme = "Bearer";
    private const string RefreshGrantType = "refresh_token";
    private const string GrantTypeField = "grant_type";
    private const string RefreshTokenField = "refresh_token";
    private const string ClientIdField = "client_id";
    private const string AccessTokenProperty = "access_token";
    private const string RefreshTokenProperty = "refresh_token";
    private const string ExpiresInProperty = "expires_in";

    public async Task<LimitResult> FetchAsync(
        OAuthCredentials credentials,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, UsageLimitEndpoints.UsageUrl);
        request.Headers.Authorization =
            new AuthenticationHeaderValue(BearerScheme, credentials.AccessToken);
        request.Headers.Add(
            UsageLimitEndpoints.OAuthBetaHeader,
            UsageLimitEndpoints.OAuthBetaValue);

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return LimitResult.Failed(LimitFailure.SignInExpired);
            }

            if (!response.IsSuccessStatusCode)
            {
                return LimitResult.Failed(LimitFailure.ServiceUnavailable);
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            return LimitResult.Succeeded(UsageLimitParser.Parse(json, timeProvider.GetUtcNow()));
        }
        catch (Exception exception) when (IsNetworkProblem(exception))
        {
            return LimitResult.Failed(LimitFailure.Offline);
        }
    }

    /// <summary>Exchanges a refresh token for new tokens. Returns null when refused.</summary>
    public async Task<TokenGrant?> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken)
    {
        var body = new Dictionary<string, string>
        {
            [GrantTypeField] = RefreshGrantType,
            [RefreshTokenField] = refreshToken,
            [ClientIdField] = UsageLimitEndpoints.ClaudeCodeClientId,
        };

        try
        {
            using var response = await httpClient.PostAsJsonAsync(
                UsageLimitEndpoints.TokenUrl,
                body,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            return ReadGrant(json);
        }
        catch (Exception exception) when (IsNetworkProblem(exception))
        {
            return null;
        }
    }

    private TokenGrant? ReadGrant(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.GetStringOrNull(AccessTokenProperty) is not { } accessToken)
        {
            return null;
        }

        var lifetime = TimeSpan.FromSeconds(root.GetInt64OrZero(ExpiresInProperty));
        return new TokenGrant(
            AccessToken: accessToken,
            RefreshToken: root.GetStringOrNull(RefreshTokenProperty),
            ExpiresAt: timeProvider.GetUtcNow() + lifetime);
    }

    private static bool IsNetworkProblem(Exception exception) =>
        exception is HttpRequestException or TaskCanceledException or JsonException;
}
