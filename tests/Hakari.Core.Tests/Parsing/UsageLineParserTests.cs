using System.Text;
using Hakari.Core.Parsing;
using Hakari.Core.Tests.Support;

namespace Hakari.Core.Tests.Parsing;

public class UsageLineParserTests
{
    [Fact]
    public void Reads_cache_write_split_by_duration()
    {
        var record = Parse(SampleLogLines.Assistant());

        Assert.NotNull(record);
        Assert.Equal(0, record.Tokens.CacheWriteFiveMinutes);
        Assert.Equal(69438, record.Tokens.CacheWriteOneHour);
        Assert.Equal(32048, record.Tokens.CacheRead);
    }

    [Fact]
    public void Builds_deduplication_key_from_message_and_request()
    {
        var record = Parse(SampleLogLines.Assistant(messageId: "msg_9", requestId: "req_9"));

        Assert.Equal("msg_9|req_9", record?.DeduplicationKey);
    }

    [Fact]
    public void Ignores_user_lines() => Assert.Null(Parse(SampleLogLines.User));

    [Fact]
    public void Ignores_synthetic_model_lines() =>
        Assert.Null(Parse(SampleLogLines.Assistant(model: "<synthetic>")));

    [Fact]
    public void Ignores_malformed_json() => Assert.Null(Parse("""{"type":"assistant",broken"""));

    private static Hakari.Core.Usage.UsageRecord? Parse(string line) =>
        UsageLineParser.TryParse(Encoding.UTF8.GetBytes(line));
}
