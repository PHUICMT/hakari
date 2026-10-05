using System.Text;
using Hakari.Core;

namespace Hakari.Core.Tests;

public class ParserAndPricingTests
{
    const string Line = """{"type":"assistant","requestId":"req_1","sessionId":"s1","timestamp":"2026-10-05T06:45:53.637Z","isSidechain":false,"message":{"id":"msg_1","model":"claude-opus-5","usage":{"input_tokens":2,"cache_creation_input_tokens":69438,"cache_read_input_tokens":32048,"output_tokens":433,"cache_creation":{"ephemeral_1h_input_tokens":69438,"ephemeral_5m_input_tokens":0},"speed":"standard"}}}""";

    static PricingTable Pricing() =>
        PricingTable.Load(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "pricing.json")));

    [Fact]
    public void Parses_cache_write_split()
    {
        var r = UsageLineParser.TryParse(Encoding.UTF8.GetBytes(Line))!;
        Assert.Equal(0, r.CacheWrite5mTokens);
        Assert.Equal(69438, r.CacheWrite1hTokens);
        Assert.Equal("msg_1|req_1", r.DedupeKey);
    }

    [Fact]
    public void Ignores_non_assistant_lines() =>
        Assert.Null(UsageLineParser.TryParse("""{"type":"user"}"""u8));

    [Fact]
    public void Prices_1h_cache_at_2x_input()
    {
        var r = UsageLineParser.TryParse(Encoding.UTF8.GetBytes(Line))!;
        var expected = (2 * 5m + 433 * 25m + 69438 * 10m + 32048 * 0.5m) / 1_000_000m;
        Assert.Equal(expected, Pricing().Cost(r));
    }
}
