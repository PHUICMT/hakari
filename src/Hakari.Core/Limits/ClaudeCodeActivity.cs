using System.ComponentModel;
using System.Diagnostics;
using Hakari.Core.Sources;

namespace Hakari.Core.Limits;

/// <summary>
/// Looks for running Claude Code processes. Any Windows instance counts for every Windows
/// source, which errs on the side of not refreshing.
/// </summary>
public sealed class ClaudeCodeActivity : IClaudeCodeActivity
{
    private const string ProcessName = "claude";
    private const string WslExecutable = "wsl.exe";
    private const int FoundExitCode = 0;

    private static readonly TimeSpan WslCheckTimeout = TimeSpan.FromSeconds(5);

    public bool IsRunning(UsageSource source) => source.Kind == SourceKind.Wsl
        ? IsRunningInWsl(source)
        : IsRunningOnWindows();

    private static bool IsRunningOnWindows()
    {
        var processes = Process.GetProcessesByName(ProcessName);
        foreach (var process in processes)
        {
            process.Dispose();
        }

        return processes.Length > 0;
    }

    /// <summary>Asked only right before a refresh, for a distribution already running.</summary>
    private static bool IsRunningInWsl(UsageSource source)
    {
        if (WslLocator.DistributionOf(source.ConfigDirectory) is not { } distribution)
        {
            return true;
        }

        var startInfo = new ProcessStartInfo(WslExecutable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
        };
        foreach (var argument in new[] { "-d", distribution, "-e", "pgrep", "-x", ProcessName })
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null || !process.WaitForExit(WslCheckTimeout))
            {
                return true;
            }

            return process.ExitCode == FoundExitCode;
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            return true;
        }
    }
}
