using System.Runtime.InteropServices;

namespace Hakari.Core.Startup;

/// <summary>
/// Whether Hakari runs from an MSIX package (the Store build) or from a plain folder. The
/// package brings its own start-with-Windows task, notification identity and updates, so
/// the folder build's registry entries and update check stand aside there.
/// </summary>
public static partial class PackageIdentity
{
    private const int NoPackage = 15700;
    private const int InsufficientBuffer = 122;

    public static bool IsPackaged { get; } = Detect();

    private static bool Detect()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(8))
        {
            return false;
        }

        var length = 0u;
        var result = GetCurrentPackageFullName(ref length, IntPtr.Zero);
        return result != NoPackage && (result == InsufficientBuffer || result == 0);
    }

    [LibraryImport("kernel32.dll")]
    private static partial int GetCurrentPackageFullName(ref uint length, IntPtr name);
}
