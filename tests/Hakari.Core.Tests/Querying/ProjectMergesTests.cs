using Hakari.Core.Indexing;
using Hakari.Core.Pricing;
using Hakari.Core.Querying;
using Hakari.Core.Sources;
using Hakari.Core.Tests.Support;

namespace Hakari.Core.Tests.Querying;

public sealed class ProjectMergesTests : IDisposable
{
    private const string OldPath = @"D:\old\app";
    private const string NewPath = @"D:\new\app";

    private readonly TemporaryDirectory directory = new();
    private readonly IndexStore store;
    private readonly UsageSource source;

    public ProjectMergesTests()
    {
        store = new IndexStore(directory.Combine("index.db"));
        var configDirectory = directory.Combine("config");
        Directory.CreateDirectory(Path.Combine(configDirectory, "projects", "project_a"));
        source = new UsageSource("test", SourceKind.ConfigDirectory, "Test", configDirectory);
    }

    [Fact]
    public void Counts_a_joined_folder_under_the_one_it_was_joined_into()
    {
        IndexReplies((OldPath, "one"), (NewPath, "two"), (@"D:\other\app", "three"));
        var merges = new ProjectMerges(new Dictionary<string, string> { [OldPath] = NewPath });

        var projects = new UsageQuery(store, PricingTable.LoadBundled(), merges: merges)
            .Summarize(UsageFilter.Everything, GroupBy.Project)
            .ToDictionary(project => project.Key, project => project.Messages);

        Assert.Equal(2, projects[NewPath]);
        Assert.Equal(1, projects[@"D:\other\app"]);
        Assert.False(projects.ContainsKey(OldPath));
    }

    [Fact]
    public void Filters_on_the_joined_project_find_both_folders()
    {
        IndexReplies((OldPath, "one"), (NewPath, "two"));
        var merges = new ProjectMerges(new Dictionary<string, string> { [OldPath] = NewPath });

        var total = new UsageQuery(store, PricingTable.LoadBundled(), merges: merges)
            .Total(new UsageFilter(Project: NewPath));

        Assert.Equal(2, total.Messages);
    }

    [Fact]
    public void Follows_a_chain_and_leaves_a_loop_unjoined()
    {
        var chain = new ProjectMerges(new Dictionary<string, string>
        {
            ["a"] = "b",
            ["b"] = "c",
            ["x"] = "y",
            ["y"] = "x",
        });

        Assert.Equal("c", chain.TargetOf("a"));
        Assert.Equal("c", chain.TargetOf("b"));
        Assert.Equal("x", chain.TargetOf("x"));
        Assert.Equal(["a", "b"], chain.JoinedInto("c"));
    }

    [Fact]
    public void Keeps_a_quote_in_a_path_from_breaking_the_query()
    {
        const string quoted = @"D:\it's\app";
        IndexReplies((quoted, "one"));
        var merges = new ProjectMerges(new Dictionary<string, string> { [quoted] = NewPath });

        var total = new UsageQuery(store, PricingTable.LoadBundled(), merges: merges)
            .Total(new UsageFilter(Project: NewPath));

        Assert.Equal(1, total.Messages);
    }

    private void IndexReplies(params (string Folder, string Id)[] replies)
    {
        File.WriteAllLines(
            Path.Combine(source.ProjectsDirectory, "project_a", "session.jsonl"),
            replies.Select(reply => WithFolder(
                SampleLogLines.Assistant(messageId: reply.Id, requestId: reply.Id),
                reply.Folder)));
        new Indexer(store).Index([source]);
    }

    private static string WithFolder(string line, string folder)
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(line)!.AsObject();
        node["cwd"] = folder;
        return node.ToJsonString();
    }

    public void Dispose()
    {
        store.Dispose();
        directory.Dispose();
    }
}
