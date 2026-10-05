using System.Runtime.InteropServices;

namespace Hakari.Core.Performance;

/// <summary>
/// Puts the current thread into Windows background processing mode, which lowers its CPU,
/// disk I/O and memory priority so indexing never competes with the user's foreground work.
/// </summary>
public sealed partial class BackgroundThreadMode : IDisposable
{
    private const int ThreadModeBackgroundBegin = 0x00010000;
    private const int ThreadModeBackgroundEnd = 0x00020000;

    private readonly bool entered;

    private BackgroundThreadMode(bool entered) => this.entered = entered;

    public static BackgroundThreadMode Enter()
    {
        if (!OperatingSystem.IsWindows())
        {
            return new BackgroundThreadMode(entered: false);
        }

        var entered = SetThreadPriority(GetCurrentThread(), ThreadModeBackgroundBegin);
        return new BackgroundThreadMode(entered);
    }

    public void Dispose()
    {
        if (entered)
        {
            SetThreadPriority(GetCurrentThread(), ThreadModeBackgroundEnd);
        }
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetThreadPriority(IntPtr threadHandle, int priority);

    [LibraryImport("kernel32.dll")]
    private static partial IntPtr GetCurrentThread();
}
