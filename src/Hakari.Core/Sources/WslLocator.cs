using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Microsoft.Win32;

namespace Hakari.Core.Sources;

public static class WslLocator
{
    private const string DistributionsRegistryPath =
        @"Software\Microsoft\Windows\CurrentVersion\Lxss";

    private const string DistributionNameValue = "DistributionName";
    private const string NetworkRoot = @"\\wsl.localhost";
    private const string HomeDirectoryName = "home";
    private const string RootUserHomeDirectoryName = "root";

    private const string WslExecutable = "wsl.exe";
    private const string ListRunningArguments = "--list --running --quiet";

    private static readonly TimeSpan ListRunningTimeout = TimeSpan.FromSeconds(5);
    private static readonly string[] IgnoredDistributionPrefixes = ["docker-desktop"];
    private static readonly char[] OutputSeparators = ['\r', '\n', '\0'];
    private static readonly string[] VirtualMachineProcessNames = ["vmmemWSL", "vmmem"];

    private const StringSplitOptions OutputSplitOptions =
        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries;

    public static IReadOnlyList<string> FindDistributions(WslScanMode mode) => mode switch
    {
        WslScanMode.Off => [],
        WslScanMode.RunningOnly => FindDistributions()
            .Intersect(FindRunningDistributions(), StringComparer.OrdinalIgnoreCase)
            .ToList(),
        WslScanMode.All => FindDistributions(),
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };

    /// <summary>
    /// WSL 2 runs every distribution in one utility VM; without its process nothing runs, and
    /// looking for it costs far less than starting wsl.exe.
    /// </summary>
    public static bool IsVirtualMachineRunning() =>
        VirtualMachineProcessNames.Any(IsProcessRunning);

    private static bool IsProcessRunning(string name)
    {
        var processes = Process.GetProcessesByName(name);
        foreach (var process in processes)
        {
            process.Dispose();
        }

        return processes.Length > 0;
    }

    public static IReadOnlyList<string> FindRunningDistributions()
    {
        if (!OperatingSystem.IsWindows() || !IsVirtualMachineRunning())
        {
            return [];
        }

        try
        {
            return RunWslListCommand();
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            return [];
        }
    }

    private static IReadOnlyList<string> RunWslListCommand()
    {
        var startInfo = new ProcessStartInfo(WslExecutable, ListRunningArguments)
        {
            RedirectStandardOutput = true,
            StandardOutputEncoding = Encoding.Unicode,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return [];
        }

        // Read while waiting, so a wsl.exe that hangs is given up on after the timeout rather
        // than holding the caller forever.
        var reading = process.StandardOutput.ReadToEndAsync();
        if (!process.WaitForExit(ListRunningTimeout))
        {
            try
            {
                process.Kill();
            }
            catch (Exception exception) when (exception is InvalidOperationException
                or Win32Exception)
            {
                // It ended on its own in the meantime.
            }

            return [];
        }

        if (process.ExitCode != 0 || !reading.Wait(ListRunningTimeout))
        {
            return [];
        }

        return reading.Result.Split(OutputSeparators, OutputSplitOptions).ToList();
    }

    public static IReadOnlyList<string> FindDistributions()
    {
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        using var distributionsKey = Registry.CurrentUser.OpenSubKey(DistributionsRegistryPath);
        if (distributionsKey is null)
        {
            return [];
        }

        var distributions = new List<string>();
        foreach (var subKeyName in distributionsKey.GetSubKeyNames())
        {
            using var distributionKey = distributionsKey.OpenSubKey(subKeyName);
            if (distributionKey?.GetValue(DistributionNameValue) is string name && !IsIgnored(name))
            {
                distributions.Add(name);
            }
        }

        return distributions;
    }

    public static IReadOnlyList<string> FindUserHomes(string distribution)
    {
        var distributionRoot = Path.Combine(NetworkRoot, distribution);
        var homes = new List<string>();
        var homeParent = Path.Combine(distributionRoot, HomeDirectoryName);
        homes.AddRange(EnumerateDirectoriesSafely(homeParent));

        var rootUserHome = Path.Combine(distributionRoot, RootUserHomeDirectoryName);
        if (DirectoryExistsSafely(rootUserHome))
        {
            homes.Add(rootUserHome);
        }

        return homes;
    }

    /// <summary>The distribution a \\wsl.localhost\name\... path belongs to.</summary>
    public static string? DistributionOf(string path)
    {
        var prefix = NetworkRoot + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var remainder = path[prefix.Length..];
        var separator = remainder.IndexOf(Path.DirectorySeparatorChar);
        return separator > 0 ? remainder[..separator] : remainder;
    }

    private static bool IsIgnored(string distribution) =>
        IgnoredDistributionPrefixes.Any(prefix =>
            distribution.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<string> EnumerateDirectoriesSafely(string directory)
    {
        try
        {
            return Directory.EnumerateDirectories(directory).ToList();
        }
        catch (Exception exception) when (ConfigDirectoryScanner.IsAccessProblem(exception))
        {
            return [];
        }
    }

    private static bool DirectoryExistsSafely(string directory)
    {
        try
        {
            return Directory.Exists(directory);
        }
        catch (Exception exception) when (ConfigDirectoryScanner.IsAccessProblem(exception))
        {
            return false;
        }
    }
}
