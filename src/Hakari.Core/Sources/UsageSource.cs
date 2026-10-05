namespace Hakari.Core.Sources;

public sealed record UsageSource(
    string Id,
    SourceKind Kind,
    string DisplayName,
    string ConfigDirectory)
{
    public string ProjectsDirectory =>
        Path.Combine(ConfigDirectory, ClaudeConfigNames.ProjectsDirectoryName);
}
