using System.Net.Http.Headers;

namespace Hakari.Core.Currency;

internal static class ExchangeRateHttp
{
    private const string ProductName = "Hakari";
    private const string ProductVersion = "1.0";

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    public static HttpClient Client { get; } = CreateClient();

    public static bool IsNetworkProblem(Exception exception) =>
        exception is HttpRequestException
            or TaskCanceledException
            or System.Text.Json.JsonException;

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = RequestTimeout };
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue(ProductName, ProductVersion));
        return client;
    }
}
