# Runs Hakari through every taskbar scenario and saves a screenshot of each taskbar.
# Every setting it changes is restored before the next scenario starts.
param(
    [string] $OutputDirectory = (Join-Path $PSScriptRoot '..\artifacts\scenarios')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing, System.Windows.Forms
Add-Type @'
using System;
using System.Runtime.InteropServices;

public static class ScenarioNative
{
    [StructLayout(LayoutKind.Sequential)]
    public struct AppBarData
    {
        public int Size;
        public IntPtr WindowHandle;
        public uint CallbackMessage;
        public uint Edge;
        public int Left, Top, Right, Bottom;
        public IntPtr State;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr FindWindow(string className, string windowName);

    [DllImport("shell32.dll")]
    public static extern IntPtr SHAppBarMessage(uint message, ref AppBarData data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr SendMessageTimeout(IntPtr window, uint message, IntPtr wordParameter,
        string longParameter, uint flags, uint timeout, out IntPtr result);

    public static void BroadcastSettingChange(string area)
    {
        IntPtr result;
        SendMessageTimeout(new IntPtr(0xffff), 0x001A, IntPtr.Zero, area, 0x0002, 2000, out result);
    }

    public static long GetAutoHide()
    {
        var data = new AppBarData();
        data.Size = Marshal.SizeOf(typeof(AppBarData));
        return SHAppBarMessage(0x00000004, ref data).ToInt64();
    }

    public static void SetAutoHide(bool enabled)
    {
        var data = new AppBarData();
        data.Size = Marshal.SizeOf(typeof(AppBarData));
        data.WindowHandle = FindWindow("Shell_TrayWnd", null);
        data.State = new IntPtr(enabled ? 1 : 0);
        SHAppBarMessage(0x0000000A, ref data);
    }
}
'@

$spike = Join-Path $PSScriptRoot '..\src\Hakari\bin\Release\net9.0-windows10.0.19041.0\Hakari.exe'
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
$advancedKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced'
$personalizeKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize'

function Save-Taskbars([string] $name) {
    $screens = [System.Windows.Forms.Screen]::AllScreens
    foreach ($screen in $screens) {
        $label = if ($screen.Primary) { 'primary' } else { 'secondary' }
        $width = [Math]::Min(1100, $screen.Bounds.Width)
        $height = 48
        $left = $screen.Bounds.Right - $width
        $top = $screen.Bounds.Bottom - $height
        $shot = New-Object System.Drawing.Bitmap $width, $height
        $graphics = [System.Drawing.Graphics]::FromImage($shot)
        $graphics.CopyFromScreen($left, $top, 0, 0, $shot.Size)
        $graphics.Dispose()
        $shot.Save((Join-Path $OutputDirectory "$name-$label.png"))
        $shot.Dispose()
    }
}

function Wait-ForTaskbar {
    $deadline = (Get-Date).AddSeconds(20)
    while ((Get-Date) -lt $deadline) {
        if ([ScenarioNative]::FindWindow('Shell_TrayWnd', $null) -ne [IntPtr]::Zero) { return }
        Start-Sleep -Milliseconds 250
    }
    Start-Process explorer.exe
    Start-Sleep -Seconds 5
}

$stuckRectsKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\StuckRects3'
$autoHideByteIndex = 8
$autoHideOffValue = 2

function Get-SavedAutoHide { (Get-ItemProperty $stuckRectsKey).Settings[$autoHideByteIndex] }

# Explorer saves the auto-hide flag here and reloads it on every restart.
function Set-SavedAutoHideOff {
    $settings = (Get-ItemProperty $stuckRectsKey).Settings
    $settings[$autoHideByteIndex] = $autoHideOffValue
    Set-ItemProperty $stuckRectsKey -Name Settings -Value $settings -Type Binary
}

if ([ScenarioNative]::GetAutoHide() -ne 0 -or (Get-SavedAutoHide) -ne $autoHideOffValue) {
    throw 'Auto-hide is already on; refusing to run so the results are not misleading.'
}

$log = Join-Path $env:LOCALAPPDATA 'Hakari\diagnostics.log'
$spikeProcess = Start-Process -FilePath $spike -ArgumentList '--diagnostics', '--report-seconds', '5' `
    -PassThru

try {
    Start-Sleep -Seconds 10
    Save-Taskbars '1-baseline'

    Write-Output 'Scenario: Explorer restart'
    Get-Process explorer | Stop-Process -Force
    Wait-ForTaskbar
    Start-Sleep -Seconds 8
    Save-Taskbars '2-after-explorer-restart'

    Write-Output 'Scenario: centered taskbar icons'
    $originalAlignment = (Get-ItemProperty $advancedKey).TaskbarAl
    Set-ItemProperty $advancedKey -Name TaskbarAl -Value 1 -Type DWord
    [ScenarioNative]::BroadcastSettingChange('TraySettings')
    Start-Sleep -Seconds 4
    Save-Taskbars '3-centered'
    if ($null -eq $originalAlignment) { Remove-ItemProperty $advancedKey -Name TaskbarAl }
    else { Set-ItemProperty $advancedKey -Name TaskbarAl -Value $originalAlignment -Type DWord }
    [ScenarioNative]::BroadcastSettingChange('TraySettings')
    Start-Sleep -Seconds 3

    Write-Output 'Scenario: auto-hide taskbar'
    [ScenarioNative]::SetAutoHide($true)
    Start-Sleep -Seconds 4
    Save-Taskbars '4-auto-hide'
    [ScenarioNative]::SetAutoHide($false)
    Set-SavedAutoHideOff
    Start-Sleep -Seconds 3

    Write-Output 'Scenario: full-screen window'
    $fullScreen = Start-Process powershell -WindowStyle Hidden -PassThru -ArgumentList @(
        '-NoProfile', '-Command',
        'Add-Type -AssemblyName System.Windows.Forms; $form = New-Object System.Windows.Forms.Form; ' +
        '$form.FormBorderStyle = 0; $form.WindowState = 2; $form.TopMost = $true; ' +
        '$form.BackColor = [System.Drawing.Color]::DarkSlateGray; [System.Windows.Forms.Application]::Run($form)')
    Start-Sleep -Seconds 4
    Save-Taskbars '5-full-screen'
    Stop-Process -Id $fullScreen.Id -Force
    Start-Sleep -Seconds 2

    Write-Output 'Scenario: light taskbar theme'
    $originalTheme = (Get-ItemProperty $personalizeKey).SystemUsesLightTheme
    Set-ItemProperty $personalizeKey -Name SystemUsesLightTheme -Value 1 -Type DWord
    [ScenarioNative]::BroadcastSettingChange('ImmersiveColorSet')
    Start-Sleep -Seconds 4
    Save-Taskbars '6-light-theme'
    Set-ItemProperty $personalizeKey -Name SystemUsesLightTheme -Value $originalTheme -Type DWord
    [ScenarioNative]::BroadcastSettingChange('ImmersiveColorSet')
    Start-Sleep -Seconds 4
    Write-Output 'Restoring: restart Explorer so every taskbar reloads the theme'
    Get-Process explorer | Stop-Process -Force
    Wait-ForTaskbar
    Start-Sleep -Seconds 8
    Save-Taskbars '7-restored'
}
finally {
    [ScenarioNative]::SetAutoHide($false)
    Set-SavedAutoHideOff
    Stop-Process -Id $spikeProcess.Id -Force -ErrorAction SilentlyContinue
}

$advanced = Get-ItemProperty $advancedKey
$personalize = Get-ItemProperty $personalizeKey
Write-Output ("Final state: auto-hide={0} saved auto-hide byte={1} TaskbarAl={2} SystemUsesLightTheme={3}" -f `
    [ScenarioNative]::GetAutoHide(), (Get-SavedAutoHide), $advanced.TaskbarAl, $personalize.SystemUsesLightTheme)
Get-Content $log
Get-ChildItem $OutputDirectory -Filter *.png | Select-Object -ExpandProperty Name
