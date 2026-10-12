using System.Reflection;

namespace Hakari.Surfaces.Updates;

/// <summary>
/// Whether this copy was built from source on a developer's machine, which carries the
/// latest tag with "-dev" (see Directory.Build.props). Such a copy is updated by building,
/// so it is never offered a release or swapped for one.
/// </summary>
internal static class AppBuild
{
    private const string DevMark = "-dev";

    public static bool IsFromSource { get; } =
        typeof(AppBuild).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion.Contains(DevMark, StringComparison.Ordinal) == true;
}
