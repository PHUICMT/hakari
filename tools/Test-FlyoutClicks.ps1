# Clicks the primary widget like a person would and checks the flyout's behavior:
# opens, closes on a second click, closes on a click elsewhere, and the window process exits
# when idle. Uses no screenshots. Pass the widget's position on the taskbar.
param(
    [int] $WidgetX = 3060,
    [int] $WidgetY = 1416,
    [int] $ElsewhereX = 1700,
    [int] $ElsewhereY = 700
)

$ErrorActionPreference = 'Stop'
Add-Type @'
using System;
using System.Runtime.InteropServices;

public static class FlyoutClickNative
{
    public delegate bool EnumProc(IntPtr window, IntPtr parameter);

    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, int x, int y, uint data, UIntPtr extra);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc callback, IntPtr parameter);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr window);

    public static void Click(int x, int y)
    {
        SetCursorPos(x, y);
        mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
        mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
    }

    public static bool HasVisibleWindow(uint processId)
    {
        var visible = false;
        EnumWindows((window, parameter) =>
        {
            uint owner;
            GetWindowThreadProcessId(window, out owner);
            if (owner == processId && IsWindowVisible(window)) { visible = true; return false; }
            return true;
        }, IntPtr.Zero);
        return visible;
    }
}
'@

$root = Split-Path $PSScriptRoot
$resident = Join-Path $root 'src\Hakari\bin\Release\net9.0-windows10.0.19041.0\Hakari.exe'
Get-Process Hakari.Surfaces, Hakari -ErrorAction SilentlyContinue | Stop-Process -Force
$residentProcess = Start-Process $resident -PassThru
Start-Sleep -Seconds 7

function Wait-Visible($process, [bool] $expected, [int] $timeoutMilliseconds) {
    $watch = [Diagnostics.Stopwatch]::StartNew()
    while ($watch.ElapsedMilliseconds -lt $timeoutMilliseconds) {
        if ([FlyoutClickNative]::HasVisibleWindow([uint32]$process.Id) -eq $expected) {
            return $watch.ElapsedMilliseconds
        }
        Start-Sleep -Milliseconds 10
    }
    return -1
}

try {
    $watch = [Diagnostics.Stopwatch]::StartNew()
    [FlyoutClickNative]::Click($WidgetX, $WidgetY)
    $surfaces = $null
    while (-not $surfaces -and $watch.ElapsedMilliseconds -lt 8000) {
        $surfaces = Get-Process Hakari.Surfaces -ErrorAction SilentlyContinue | Select-Object -First 1
        Start-Sleep -Milliseconds 20
    }
    $opened = Wait-Visible $surfaces $true 8000
    Write-Output "1. click widget opens the flyout (cold): $($opened -ge 0) after $($watch.ElapsedMilliseconds) ms"

    Start-Sleep -Milliseconds 800
    [FlyoutClickNative]::Click($ElsewhereX, $ElsewhereY)
    Write-Output "2. click elsewhere closes it: $((Wait-Visible $surfaces $false 1500) -ge 0)"

    Start-Sleep -Milliseconds 800
    [FlyoutClickNative]::Click($WidgetX, $WidgetY)
    $warm = Wait-Visible $surfaces $true 3000
    Write-Output "3. click widget reopens it (warm): $($warm -ge 0) after $warm ms"

    Start-Sleep -Milliseconds 800
    [FlyoutClickNative]::Click($WidgetX, $WidgetY)
    Write-Output "4. second click on the widget closes it: $((Wait-Visible $surfaces $false 1500) -ge 0)"

    Start-Sleep -Milliseconds 800
    [FlyoutClickNative]::Click($WidgetX, $WidgetY)
    [void](Wait-Visible $surfaces $true 3000)
    Start-Sleep -Milliseconds 800
    [FlyoutClickNative]::Click($ElsewhereX, $ElsewhereY)
    Write-Output "5. click elsewhere after a pipe open closes it: $((Wait-Visible $surfaces $false 1500) -ge 0)"

    Start-Sleep -Seconds 63
    $surfaces.Refresh()
    Write-Output "6. window process exits when idle: $($surfaces.HasExited)"
}
finally {
    Stop-Process -Id $residentProcess.Id -Force -ErrorAction SilentlyContinue
    Get-Process Hakari.Surfaces -ErrorAction SilentlyContinue | Stop-Process -Force
}
