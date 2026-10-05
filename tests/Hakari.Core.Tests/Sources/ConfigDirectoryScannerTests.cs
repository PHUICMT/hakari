using Hakari.Core.Sources;
using Hakari.Core.Tests.Support;

namespace Hakari.Core.Tests.Sources;

public sealed class ConfigDirectoryScannerTests : IDisposable
{
    private readonly TemporaryDirectory home = new();

    [Fact]
    public void Finds_every_signed_in_or_used_config_folder()
    {
        Directory.CreateDirectory(home.Combine(".claude", "projects"));
        Directory.CreateDirectory(home.Combine(".claude-personal"));
        File.WriteAllText(home.Combine(".claude-personal", ".credentials.json"), "{}");
        Directory.CreateDirectory(home.Combine(".claude-empty"));

        var found = ConfigDirectoryScanner.FindUnder(home.Path).Select(Path.GetFileName);

        Assert.Equal([".claude", ".claude-personal"], found);
    }

    public void Dispose() => home.Dispose();
}
