using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Hakari.Core.Interprocess;
using Hakari.Core.Startup;
using Hakari.Core.Updates;
using ProcessorKind = System.Runtime.InteropServices.Architecture;

namespace Hakari.Surfaces.Updates;

/// <summary>
/// Updates Hakari in place, by how it was installed. A zip copy downloads the new zip for its
/// architecture, checks it against the SHA-256 published beside it, unpacks it, and hands a
/// small script the job of swapping the folder once both processes have quit, then starting
/// the new Hakari.exe. A winget copy does the same with "winget upgrade", so winget's own
/// record stays right. The Store updates its copies itself.
/// </summary>
internal static class SelfUpdate
{
    private const string ProductName = "Hakari";
    private const string WingetId = "PHUICMT.Hakari";
    private const string ResidentExe = "Hakari.exe";
    private const string UnpackedFolder = "Hakari";
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(5);

    /// <summary>Whether this copy can update itself, not only open a download page.</summary>
    public static bool CanUpdateInPlace =>
        InstallSource.Current switch
        {
            InstallKind.Winget => true,
            InstallKind.Zip => CanWrite(AppContext.BaseDirectory),
            _ => false,
        };

    private static string Architecture =>
        RuntimeInformation.ProcessArchitecture == ProcessorKind.Arm64
            ? "arm64"
            : "x64";

    /// <summary>
    /// Prepares the update and starts the swap, then asks both processes to quit. Progress
    /// is reported as text keys, ready to show. Throws with
    /// a plain message when something is wrong, before anything is changed.
    /// </summary>
    public static async Task StartAsync(string version, IProgress<string> progress)
    {
        var script = InstallSource.Current == InstallKind.Winget
            ? WingetScript()
            : await ZipScriptAsync(version, progress);
        Launch(script);
        ResidentChannel.TrySend(ResidentCommand.Quit);
    }

    private static async Task<string> ZipScriptAsync(string version, IProgress<string> progress)
    {
        var work = Path.Combine(Path.GetTempPath(), $"hakari-update-{Guid.NewGuid():N}");
        Directory.CreateDirectory(work);
        using var client = new HttpClient { Timeout = DownloadTimeout };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(ProductName, "1"));

        progress.Report("flyout.notice.updating.download");
        var zip = Path.Combine(work, "update.zip");
        var bytes = await client.GetByteArrayAsync(UpdateCheck.ZipAddress(version, Architecture));
        await File.WriteAllBytesAsync(zip, bytes);

        progress.Report("flyout.notice.updating.verify");
        var published = await client.GetStringAsync(
            UpdateCheck.ChecksumAddress(version, Architecture));
        var expected = published.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        var actual = Convert.ToHexString(SHA256.HashData(bytes));
        if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The download does not match its checksum.");
        }

        progress.Report("flyout.notice.updating.unpack");
        ZipFile.ExtractToDirectory(zip, work);
        var source = Path.Combine(work, UnpackedFolder);
        if (!File.Exists(Path.Combine(source, ResidentExe)))
        {
            throw new InvalidDataException("The download does not hold Hakari.exe.");
        }

        var target = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        return WriteScript(work, $$"""
            $source = '{{Quote(source)}}'
            $target = '{{Quote(target)}}'
            {{WaitForExit}}
            Copy-Item -Path (Join-Path $source '*') -Destination $target -Recurse -Force
            Start-Process (Join-Path $target '{{ResidentExe}}')
            Remove-Item '{{Quote(work)}}' -Recurse -Force -ErrorAction SilentlyContinue
            """);
    }

    private static string WingetScript()
    {
        var work = Path.Combine(Path.GetTempPath(), $"hakari-update-{Guid.NewGuid():N}");
        Directory.CreateDirectory(work);
        var target = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        return WriteScript(work, $$"""
            $target = '{{Quote(target)}}'
            {{WaitForExit}}
            winget upgrade --id {{WingetId}} --exact --silent `
                --accept-source-agreements --accept-package-agreements
            Start-Process (Join-Path $target '{{ResidentExe}}')
            Remove-Item '{{Quote(work)}}' -Recurse -Force -ErrorAction SilentlyContinue
            """);
    }

    /// <summary>Both processes from this folder must have quit before their files change.</summary>
    private const string WaitForExit = """
        $deadline = (Get-Date).AddSeconds(30)
        while ((Get-Date) -lt $deadline) {
            $running = Get-Process Hakari, Hakari.Surfaces -ErrorAction SilentlyContinue |
                Where-Object { $_.Path -and $_.Path.StartsWith($target, 'OrdinalIgnoreCase') }
            if (-not $running) { break }
            Start-Sleep -Milliseconds 300
        }
        """;

    private static string WriteScript(string folder, string body)
    {
        var path = Path.Combine(folder, "update.ps1");
        File.WriteAllText(path, body);
        return path;
    }

    private static void Launch(string script)
    {
        var start = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in new[]
        {
            "-NoProfile", "-ExecutionPolicy", "Bypass", "-WindowStyle", "Hidden", "-File", script,
        })
        {
            start.ArgumentList.Add(argument);
        }

        Process.Start(start)?.Dispose();
    }

    private static string Quote(string text) => text.Replace("'", "''");

    private static bool CanWrite(string folder)
    {
        try
        {
            var probe = Path.Combine(folder, $".write-test-{Guid.NewGuid():N}");
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
            return true;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
