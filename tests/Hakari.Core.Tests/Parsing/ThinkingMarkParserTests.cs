using System.Text;
using Hakari.Core.Parsing;

namespace Hakari.Core.Tests.Parsing;

public sealed class ThinkingMarkParserTests
{
    private const string Start = """{"type":"assistant","message":{"content":[""";
    private const string End = "]}}";

    private static bool Has(string parts) =>
        ThinkingMarkParser.HasThinking(Encoding.UTF8.GetBytes(Start + parts + End));

    [Fact]
    public void A_thinking_part_is_found_by_its_kind()
    {
        Assert.True(Has("""{"type":"thinking","thinking":"x"}"""));
        Assert.True(Has("""{"type":"redacted_thinking"}"""));
    }

    [Fact]
    public void The_word_inside_text_does_not_count()
    {
        Assert.False(Has("""{"type":"text","text":"I was thinking\" about it"}"""));
        Assert.False(Has("""{"type":"text","text":"hi"}"""));
    }
}
