namespace Hakari.Core.Querying;

/// <summary>
/// Project folders the user joined into another, such as a project moved or copied to a new
/// path: every summary counts the old path's usage under the new one. Nothing is joined on
/// its own, since two folders of the same name in different places are different projects.
/// </summary>
public sealed class ProjectMerges
{
    private const int MostSteps = 32;

    private readonly Dictionary<string, string> targets;

    /// <param name="merges">Each joined path and the path it was joined into.</param>
    public ProjectMerges(IReadOnlyDictionary<string, string>? merges)
    {
        targets = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (from, _) in merges ?? new Dictionary<string, string>())
        {
            if (Follow(merges!, from) is { } target && target != from)
            {
                targets[from] = target;
            }
        }
    }

    public static ProjectMerges None { get; } = new(null);

    public bool IsEmpty => targets.Count == 0;

    /// <summary>The path a project's usage is counted under.</summary>
    public string TargetOf(string project) =>
        targets.TryGetValue(project, out var target) ? target : project;

    /// <summary>The paths joined into a project, not counting itself.</summary>
    public IReadOnlyList<string> JoinedInto(string project) =>
        [.. targets.Where(entry => entry.Value == project).Select(entry => entry.Key).Order()];

    /// <summary>
    /// The SQL for a record's project with the joins applied, built from
    /// <paramref name="project"/>, the plain project expression.
    /// </summary>
    internal string Apply(string project)
    {
        if (IsEmpty)
        {
            return project;
        }

        var cases = string.Concat(targets.Select(entry =>
            $" WHEN {Literal(entry.Key)} THEN {Literal(entry.Value)}"));
        return $"(CASE {project}{cases} ELSE {project} END)";
    }

    /// <summary>A chain (a into b, b into c) ends at c; a loop is left unjoined.</summary>
    private static string? Follow(IReadOnlyDictionary<string, string> merges, string from)
    {
        var current = from;
        for (var step = 0; step < MostSteps; step++)
        {
            if (!merges.TryGetValue(current, out var next) || string.IsNullOrEmpty(next))
            {
                return current;
            }

            if (next == from)
            {
                return null;
            }

            current = next;
        }

        return null;
    }

    private static string Literal(string text) => "'" + text.Replace("'", "''") + "'";
}
