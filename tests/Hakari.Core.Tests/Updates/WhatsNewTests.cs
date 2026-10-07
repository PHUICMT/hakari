using Hakari.Core.Startup;
using Hakari.Core.Updates;
using Hakari.Core.Tests.Support;

namespace Hakari.Core.Tests.Updates;

public sealed class WhatsNewTests
{
    [Fact]
    public void Reads_the_bullets_of_the_notes()
    {
        using var directory = new TemporaryDirectory();
        File.WriteAllLines(
            directory.Combine(WhatsNew.FileName),
            ["## Fixed", "", "- Animations play again", "- Faster first read", "", "Download…"]);

        Assert.Equal(
            ["Animations play again", "Faster first read"],
            WhatsNew.Changes(directory.Path));
    }

    [Theory]
    [InlineData("", "0.9.3", false)]
    [InlineData("0.9.2", "0.9.3", true)]
    [InlineData("0.9.3", "0.9.3", false)]
    [InlineData("1.0.0", "0.9.3", false)]
    public void Shows_only_after_an_update(string lastSeen, string current, bool shows)
    {
        Assert.Equal(shows, WhatsNew.IsNewSince(lastSeen, Version.Parse(current + ".0")));
    }

    [Theory]
    [InlineData(@"C:\Local\Microsoft\WinGet\Packages\PHUICMT.Hakari_x\Hakari\", "Winget")]
    [InlineData(@"C:\Local\Programs\Hakari\", "Zip")]
    public void Tells_winget_from_a_zip(string folder, string expected)
    {
        Assert.Equal(Enum.Parse<InstallKind>(expected), InstallSource.Detect(folder));
    }
}
