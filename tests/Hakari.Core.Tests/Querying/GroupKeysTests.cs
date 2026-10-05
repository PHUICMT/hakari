using Hakari.Core.Querying;

namespace Hakari.Core.Tests.Querying;

public sealed class GroupKeysTests
{
    [Fact]
    public void Splits_a_project_scoped_key()
    {
        var key = @"D:\WORK\hakari" + GroupKeys.Separator + "main";

        Assert.Equal((@"D:\WORK\hakari", "main"), GroupKeys.Split(key));
    }

    [Fact]
    public void Keeps_a_plain_key_whole()
    {
        Assert.Equal((string.Empty, "main"), GroupKeys.Split("main"));
    }
}
