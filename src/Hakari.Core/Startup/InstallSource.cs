namespace Hakari.Core.Startup;

/// <summary>Where this copy of Hakari came from, which decides how it is updated.</summary>
public enum InstallKind
{
    /// <summary>The Microsoft Store; the Store updates it.</summary>
    Store,

    /// <summary>winget's portable install; <c>winget upgrade</c> updates it.</summary>
    Winget,

    /// <summary>A zip unpacked by hand; Hakari can replace its own folder.</summary>
    Zip,
}

/// <summary>
/// Tells the install kind from this machine alone, sending nothing: a package means the
/// Store, a folder under winget's packages means winget, anything else is a zip.
/// </summary>
public static class InstallSource
{
    private const string WingetPackagesFolder = @"\Microsoft\WinGet\Packages\";

    public static InstallKind Current { get; } = Detect(AppContext.BaseDirectory);

    public static InstallKind Detect(string baseDirectory)
    {
        if (PackageIdentity.IsPackaged)
        {
            return InstallKind.Store;
        }

        return baseDirectory.Contains(WingetPackagesFolder, StringComparison.OrdinalIgnoreCase)
            ? InstallKind.Winget
            : InstallKind.Zip;
    }
}
