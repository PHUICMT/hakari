namespace Hakari.Core.Tests.Support;

/// <summary>Made-up values in the shape of the real responses.</summary>
internal static class SampleLimits
{
    public const string UsageResponse = """
        {"five_hour":{"utilization":40.0},
         "limits":[
           {"kind":"session","group":"session","percent":40,"severity":"normal",
            "resets_at":"2026-10-05T11:00:00+00:00","scope":null,"is_active":false},
           {"kind":"weekly_all","group":"weekly","percent":91,"severity":"warning",
            "resets_at":"2026-10-08T15:00:00+00:00","scope":null,"is_active":true},
           {"kind":"weekly_scoped","group":"weekly","percent":5,"severity":"normal",
            "resets_at":"2026-10-08T15:00:00+00:00",
            "scope":{"model":{"id":null,"display_name":"Fable"}},"is_active":false}],
         "extra_usage":{"is_enabled":true,"monthly_limit":5000,"used_credits":1250.0,
            "currency":"USD","decimal_places":2},
         "some_future_field":{"anything":true}}
        """;

    public const string TokenResponse =
        """{"access_token":"new-access","refresh_token":"new-refresh","expires_in":28800}""";

    public static string CredentialsJson(DateTimeOffset expiresAt) => $$"""
        {"claudeAiOauth":{"accessToken":"old-access","refreshToken":"old-refresh",
          "expiresAt":{{expiresAt.ToUnixTimeMilliseconds()}},
          "refreshTokenExpiresAt":"2026-10-31T09:20:00+00:00",
          "scopes":["user:inference"],"subscriptionType":"team",
          "rateLimitTier":"default_claude_max_5x"},
         "organizationUuid":"00000000-0000-0000-0000-000000000000"}
        """;
}
