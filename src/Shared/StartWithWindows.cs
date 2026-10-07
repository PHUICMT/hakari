using Hakari.Core.Startup;
using Windows.ApplicationModel;

namespace Hakari.Shared;

/// <summary>
/// Start with Windows for either build: the package's StartupTask when installed from the
/// Store, the per-user Run key otherwise. Linked into both executables, which need WinRT
/// for the task; Hakari.Core stays free of it.
/// </summary>
internal static class StartWithWindows
{
    /// <summary>Matches the StartupTask's TaskId in the package manifest.</summary>
    private const string TaskId = "HakariStartup";

    public static bool IsOn(string? residentPath)
    {
        if (!PackageIdentity.IsPackaged)
        {
            return residentPath is not null && StartupRegistration.IsRegistered(residentPath);
        }

        return Task()?.State is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
    }

    /// <summary>Whether the user can change it here; Windows can lock a package's task.</summary>
    public static bool CanChange(string? residentPath)
    {
        if (!PackageIdentity.IsPackaged)
        {
            return residentPath is not null;
        }

        return Task()?.State is StartupTaskState.Enabled or StartupTaskState.Disabled;
    }

    public static void Set(string? residentPath, bool on)
    {
        if (!PackageIdentity.IsPackaged)
        {
            if (residentPath is not null)
            {
                StartupRegistration.Set(residentPath, on);
            }

            return;
        }

        if (Task() is not { } task)
        {
            return;
        }

        if (on)
        {
            task.RequestEnableAsync().AsTask().GetAwaiter().GetResult();
        }
        else
        {
            task.Disable();
        }
    }

    private static StartupTask? Task()
    {
        try
        {
            return StartupTask.GetAsync(TaskId).AsTask().GetAwaiter().GetResult();
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            return null;
        }
    }
}
