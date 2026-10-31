using System.Net.Http.Headers;
using System.Text.Json;
using Hakari.Core.Configuration;

namespace Hakari.Core.Pricing;

/// <summary>When prices were last looked for, kept between runs.</summary>
public sealed record PriceRefreshState(DateTimeOffset CheckedAt, DateTimeOffset FallbackAt);

/// <summary>
/// Keeps prices current without being asked: the project's table once a day, sooner (hourly)
/// while some model in the logs has no price, and then LiteLLM's list for models still
/// missing, at most every six hours. Only public price files are fetched; nothing about the
/// user is sent. A network problem is quiet and tried again later.
/// </summary>
public static class PriceRefresh
{
    private const string StateFileName = "prices-check.json";
    private const string ProductName = "Hakari";

    private static readonly TimeSpan CheckEvery = TimeSpan.FromDays(1);
    private static readonly TimeSpan CheckWhileMissing = TimeSpan.FromHours(1);
    private static readonly TimeSpan FallbackEvery = TimeSpan.FromHours(6);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

    public static string StatePath => Path.Combine(HakariPaths.DataDirectory, StateFileName);

    /// <summary>Whether a look is due, given the models that have no price now.</summary>
    public static bool IsDue(DateTimeOffset now, IReadOnlyCollection<string> unpriced)
    {
        var state = Load();
        return now - state.CheckedAt >= (unpriced.Count > 0 ? CheckWhileMissing : CheckEvery)
            || unpriced.Count > 0 && now - state.FallbackAt >= FallbackEvery;
    }

    /// <returns>True when any price changed, so totals should be read again.</returns>
    public static async Task<bool> RefreshAsync(
        IReadOnlyCollection<string> unpriced,
        DateTimeOffset now,
        CancellationToken cancellation)
    {
        var state = Load();
        var changed = false;
        using var client = new HttpClient { Timeout = RequestTimeout };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(ProductName, "1"));

        if (now - state.CheckedAt >= (unpriced.Count > 0 ? CheckWhileMissing : CheckEvery))
        {
            changed |= await TryAsync(async () => PricingSources.Accept(
                await client.GetStringAsync(PricingSources.LatestAddress, cancellation)));

            // Plan names and prices change too, as when Max 20x became Ultra.
            await TryAsync(async () => Accounts.PlanCatalog.Accept(
                await client.GetStringAsync(Accounts.PlanCatalog.LatestAddress, cancellation)));
            state = state with { CheckedAt = now };
        }

        var current = PricingSources.LoadCurrent();
        var stillMissing = unpriced.Where(model => !current.IsPriced(model)).ToList();
        if (stillMissing.Count > 0 && now - state.FallbackAt >= FallbackEvery)
        {
            changed |= await TryAsync(async () => PricingSources.AcceptFallback(
                LiteLlmPrices.Extract(
                    await client.GetStringAsync(LiteLlmPrices.Address, cancellation),
                    stillMissing)));
            state = state with { FallbackAt = now };
        }

        Save(state);
        return changed;
    }

    private static async Task<bool> TryAsync(Func<Task<bool>> fetch)
    {
        try
        {
            return await fetch();
        }
        catch (Exception exception) when (exception is HttpRequestException
            or TaskCanceledException or JsonException or IOException
            or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static PriceRefreshState Load()
    {
        try
        {
            return File.Exists(StatePath)
                && JsonSerializer.Deserialize<PriceRefreshState>(File.ReadAllText(StatePath))
                    is { } state
                ? state
                : new PriceRefreshState(DateTimeOffset.MinValue, DateTimeOffset.MinValue);
        }
        catch (Exception exception) when (exception is IOException or JsonException
            or UnauthorizedAccessException)
        {
            return new PriceRefreshState(DateTimeOffset.MinValue, DateTimeOffset.MinValue);
        }
    }

    private static void Save(PriceRefreshState state)
    {
        try
        {
            Directory.CreateDirectory(HakariPaths.DataDirectory);
            File.WriteAllText(StatePath, JsonSerializer.Serialize(state));
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException)
        {
        }
    }
}
