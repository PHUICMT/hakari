using System.Net;
using System.Text.Json.Nodes;
using Hakari.Core.Indexing;
using Hakari.Core.Limits;
using Hakari.Core.Sources;
using Hakari.Core.Tests.Support;
using Microsoft.Data.Sqlite;

namespace Hakari.Core.Tests.Limits;

public sealed class LimitServiceTests : IDisposable
{
    private const string AccountId = "account-a";
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 9, 0, 0, TimeSpan.Zero);

    private readonly TemporaryDirectory directory = new();
    private readonly IndexStore store;
    private readonly FakeHttpHandler http = new();
    private readonly FakeActivity activity = new();
    private readonly UsageSource source;
    private readonly string credentialsPath;

    public LimitServiceTests()
    {
        store = new IndexStore(directory.Combine("index.db"));
        var configDirectory = directory.Combine("config");
        Directory.CreateDirectory(configDirectory);
        credentialsPath = Path.Combine(configDirectory, ".credentials.json");
        source = new UsageSource("test", SourceKind.ConfigDirectory, "Test", configDirectory);
    }

    [Fact]
    public async Task Reads_live_limits_while_the_sign_in_is_valid()
    {
        WriteCredentials(expiresAt: Now.AddHours(3));
        http.Respond(UsageLimitEndpoints.UsageUrl, HttpStatusCode.OK, SampleLimits.UsageResponse);

        var result = await GetLimitsAsync(refreshAutomatically: false);

        Assert.Equal(LimitFreshness.Live, result.Snapshot?.Freshness);
        Assert.Equal(LimitFailure.None, result.Failure);
    }

    [Fact]
    public async Task Shows_last_known_limits_when_the_sign_in_expired()
    {
        await PrimeCacheWithLiveRead();
        WriteCredentials(expiresAt: Now.AddMinutes(-5));

        var result = await GetLimitsAsync(refreshAutomatically: false);

        Assert.Equal(LimitFreshness.LastKnown, result.Snapshot?.Freshness);
        Assert.Equal(LimitFailure.SignInExpired, result.Failure);
        Assert.DoesNotContain(UsageLimitEndpoints.TokenUrl, http.RequestedUrls);
    }

    [Fact]
    public async Task Leaves_refreshing_to_claude_code_while_it_runs()
    {
        WriteCredentials(expiresAt: Now.AddMinutes(-5));
        activity.Running = true;

        await GetLimitsAsync(refreshAutomatically: true);

        Assert.DoesNotContain(UsageLimitEndpoints.TokenUrl, http.RequestedUrls);
    }

    [Fact]
    public async Task Renews_the_sign_in_and_keeps_every_other_field()
    {
        WriteCredentials(expiresAt: Now.AddMinutes(-5));
        http.Respond(UsageLimitEndpoints.TokenUrl, HttpStatusCode.OK, SampleLimits.TokenResponse);
        http.Respond(UsageLimitEndpoints.UsageUrl, HttpStatusCode.OK, SampleLimits.UsageResponse);

        var result = await GetLimitsAsync(refreshAutomatically: true);

        Assert.Equal(LimitFreshness.Live, result.Snapshot?.Freshness);
        var saved = JsonNode.Parse(File.ReadAllText(credentialsPath))!;
        Assert.Equal("new-access", saved["claudeAiOauth"]!["accessToken"]!.GetValue<string>());
        Assert.Equal("new-refresh", saved["claudeAiOauth"]!["refreshToken"]!.GetValue<string>());
        Assert.Equal("team", saved["claudeAiOauth"]!["subscriptionType"]!.GetValue<string>());
        Assert.NotNull(saved["organizationUuid"]);
    }

    [Fact]
    public async Task Falls_back_to_last_known_limits_when_offline()
    {
        await PrimeCacheWithLiveRead();
        http.Respond(UsageLimitEndpoints.UsageUrl, HttpStatusCode.ServiceUnavailable, "{}");

        var result = await GetLimitsAsync(refreshAutomatically: false);

        Assert.Equal(LimitFreshness.LastKnown, result.Snapshot?.Freshness);
        Assert.Equal(LimitFailure.ServiceUnavailable, result.Failure);
    }

    public void Dispose()
    {
        store.Dispose();
        SqliteConnection.ClearAllPools();
        directory.Dispose();
    }

    private async Task PrimeCacheWithLiveRead()
    {
        WriteCredentials(expiresAt: Now.AddHours(3));
        http.Respond(UsageLimitEndpoints.UsageUrl, HttpStatusCode.OK, SampleLimits.UsageResponse);
        await GetLimitsAsync(refreshAutomatically: false);
    }

    private Task<LimitResult> GetLimitsAsync(bool refreshAutomatically) =>
        CreateService(refreshAutomatically).GetForAccountAsync(AccountId, [source], default);

    private void WriteCredentials(DateTimeOffset expiresAt) =>
        File.WriteAllText(credentialsPath, SampleLimits.CredentialsJson(expiresAt));

    private LimitService CreateService(bool refreshAutomatically) => new(
        new UsageLimitClient(new HttpClient(http), new FixedTime(Now)),
        new LimitCache(store),
        activity,
        new LimitServiceOptions(refreshAutomatically),
        new FixedTime(Now));

    private sealed class FakeActivity : IClaudeCodeActivity
    {
        public bool Running { get; set; }

        public bool IsRunning(UsageSource usageSource) => Running;
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
