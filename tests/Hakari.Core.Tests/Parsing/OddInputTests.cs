using System.Text;
using Hakari.Core.Parsing;
using Hakari.Core.Settings;

namespace Hakari.Core.Tests.Parsing;

/// <summary>Lines and files of an odd shape read as nothing rather than throwing.</summary>
public sealed class OddInputTests
{
    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);

    [Theory]
    [InlineData("""["type","assistant"]""")]
    [InlineData("""{"type":"assistant","message":[]}""")]
    [InlineData("""{"type":"assistant","message":{"id":"m","usage":{"input_tokens":1.5}}}""")]
    [InlineData("""{"type":"assistant","message":{"id":"m","usage":{"output_tokens":1e40}}}""")]
    public void An_odd_line_never_throws(string line)
    {
        var exception = Record.Exception(() => UsageLineParser.TryParse(Bytes(line)));

        Assert.Null(exception);
    }

    [Fact]
    public void Null_lists_in_settings_read_as_empty()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hakari-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """{"accountNicknames":null,"hiddenAccounts":null}""");
        try
        {
            var settings = new SettingsStore(path).Load();

            Assert.Empty(settings.HiddenAccounts);
            Assert.Null(settings.NicknameOf("anyone"));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
