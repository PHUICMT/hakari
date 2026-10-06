using System.Text;
using Hakari.Core.Parsing;

namespace Hakari.Core.Tests.Parsing;

public sealed class SessionTitleParserTests
{
    [Fact]
    public void Reads_the_title_Claude_Code_made_for_a_session()
    {
        var line = """{"type":"ai-title","aiTitle":"Fix the sync bug","sessionId":"s1"}""";

        var title = SessionTitleParser.TryParse(Encoding.UTF8.GetBytes(line));

        Assert.Equal(new SessionTitle("s1", "Fix the sync bug", IsCustom: false), title);
    }

    [Fact]
    public void Reads_a_name_the_user_gave()
    {
        var line = """{"type":"custom-title","customTitle":"Meter work","sessionId":"s1"}""";

        var title = SessionTitleParser.TryParse(Encoding.UTF8.GetBytes(line));

        Assert.True(title!.IsCustom);
        Assert.Equal("Meter work", title.Title);
    }

    [Fact]
    public void Ignores_every_other_line()
    {
        var line = """{"type":"user","message":{"content":"hello"},"sessionId":"s1"}""";

        Assert.Null(SessionTitleParser.TryParse(Encoding.UTF8.GetBytes(line)));
    }

    [Fact]
    public void Ignores_a_title_line_with_no_session_or_no_title()
    {
        var noSession = """{"type":"ai-title","aiTitle":"x"}""";
        var noTitle = """{"type":"ai-title","sessionId":"s1"}""";

        Assert.Null(SessionTitleParser.TryParse(Encoding.UTF8.GetBytes(noSession)));
        Assert.Null(SessionTitleParser.TryParse(Encoding.UTF8.GetBytes(noTitle)));
    }
}
