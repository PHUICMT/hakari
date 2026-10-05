using Hakari.Core.Sources;
using Hakari.Core.Tests.Support;
using Hakari.Core.Watching;

namespace Hakari.Core.Tests.Watching;

public sealed class ChangeTrackerTests : IDisposable
{
    private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(5);

    private static readonly ChangeTrackerOptions NoDebounce = ChangeTrackerOptions.Default with
    {
        Debounce = TimeSpan.Zero,
    };

    private readonly TemporaryDirectory directory = new();
    private readonly UsageSource source;

    public ChangeTrackerTests()
    {
        Directory.CreateDirectory(directory.Combine("config", "projects", "project_a"));
        source = new UsageSource(
            Id: "test",
            Kind: SourceKind.ConfigDirectory,
            DisplayName: "Test",
            ConfigDirectory: directory.Combine("config"));
    }

    [Fact]
    public void Starts_with_a_full_scan_of_every_source()
    {
        using var tracker = new ChangeTracker([source], NoDebounce, TimeProvider.System);

        var work = tracker.TakeWork();

        Assert.Equal([source], work.FullScans);
    }

    [Fact]
    public void Reports_only_the_file_that_was_written()
    {
        using var tracker = new ChangeTracker([source], NoDebounce, TimeProvider.System);
        tracker.TakeWork();
        var logFile = directory.Combine("config", "projects", "project_a", "session.jsonl");

        File.AppendAllText(logFile, "{}\n");
        var work = WaitForChanges(tracker);

        Assert.Empty(work.FullScans);
        Assert.Contains(logFile, work.ChangedFiles[source]);
    }

    [Fact]
    public void Has_nothing_to_do_while_idle()
    {
        using var tracker = new ChangeTracker([source], NoDebounce, TimeProvider.System);
        tracker.TakeWork();

        Assert.True(tracker.TakeWork().IsEmpty);
    }

    [Fact]
    public void Treats_network_paths_as_unwatchable()
    {
        var wslSource = source with
        {
            ConfigDirectory = @"\\wsl.localhost\Ubuntu\home\alex\.claude",
        };

        Assert.False(ChangeTracker.IsWatchable(wslSource));
        Assert.True(ChangeTracker.IsWatchable(source));
    }

    public void Dispose() => directory.Dispose();

    private static PendingWork WaitForChanges(ChangeTracker tracker)
    {
        var deadline = DateTime.UtcNow + EventTimeout;
        while (DateTime.UtcNow < deadline)
        {
            tracker.WaitForWork(TimeSpan.FromMilliseconds(100), CancellationToken.None);
            var work = tracker.TakeWork();
            if (!work.IsEmpty)
            {
                return work;
            }
        }

        throw new TimeoutException("No file change was reported.");
    }
}
