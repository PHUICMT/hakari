using System.Net;
using System.Text;

namespace Hakari.Core.Tests.Support;

/// <summary>Answers requests by URL so tests never touch the network.</summary>
internal sealed class FakeHttpHandler : HttpMessageHandler
{
    private readonly Dictionary<string, (HttpStatusCode Status, string Body)> responses = [];

    public List<string> RequestedUrls { get; } = [];

    public void Respond(string url, HttpStatusCode status, string body) =>
        responses[url] = (status, body);

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var url = request.RequestUri!.ToString();
        RequestedUrls.Add(url);
        if (!responses.TryGetValue(url, out var response))
        {
            throw new HttpRequestException("offline");
        }

        return Task.FromResult(new HttpResponseMessage(response.Status)
        {
            Content = new StringContent(response.Body, Encoding.UTF8, "application/json"),
        });
    }
}
