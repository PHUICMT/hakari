<#
.SYNOPSIS
    Builds the MSIX package for the Microsoft Store from the same self-contained folder as the
    portable zip.
.DESCRIPTION
    Publishes both executables (tools/Publish-Release.ps1), adds the package manifest and
    pictures, and packs them with makeappx from the Windows SDK. The Store signs the package
    itself, so an upload needs no certificate; -CertificatePath signs it for a local test
    install instead.
.PARAMETER Name
    Package/Identity/Name from Partner Center, or a local test name.
.PARAMETER Publisher
    Package/Identity/Publisher from Partner Center, such as "CN=1234ABCD-...".
.PARAMETER PublisherDisplayName
    Package/Properties/PublisherDisplayName from Partner Center.
#>
param(
    [string]$Version = "0.9.1",
    [string]$Name = "PHUICMT.Hakari",
    [string]$Publisher = "CN=PHUICMT",
    [string]$PublisherDisplayName = "PHUICMT",
    [string]$CertificatePath = "",
    [string]$CertificatePassword = "",
    [string]$Output = "artifacts/msix"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$kits = "${env:ProgramFiles(x86)}\Windows Kits\10\bin"
$makeAppx = Get-ChildItem $kits -Recurse -Filter makeappx.exe |
    Where-Object FullName -like "*\x64\*" | Sort-Object FullName | Select-Object -Last 1
if (-not $makeAppx) { throw "makeappx.exe not found; install the Windows SDK." }

& (Join-Path $PSScriptRoot "Publish-Release.ps1") -Version $Version -Output "$Output/portable" | Out-Null
$staging = Join-Path $root "$Output/portable/Hakari"

& (Join-Path $PSScriptRoot "New-PackageAssets.ps1") -OutputDirectory (Join-Path $staging "Assets") | Out-Null
$manifest = Get-Content (Join-Path $root "packaging/msix/AppxManifest.xml") -Raw
$manifest = $manifest.Replace('$Name$', $Name).Replace('$Publisher$', $Publisher)
$manifest = $manifest.Replace('$PublisherDisplayName$', $PublisherDisplayName)
$manifest = $manifest.Replace('$Version$', "$Version.0")
Set-Content (Join-Path $staging "AppxManifest.xml") $manifest -Encoding utf8

$package = Join-Path $root "$Output/Hakari-$Version-x64.msix"
if (Test-Path $package) { Remove-Item $package -Force }
& $makeAppx.FullName pack /d $staging /p $package /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw "makeappx failed." }

if ($CertificatePath) {
    $signTool = Join-Path $makeAppx.DirectoryName "signtool.exe"
    & $signTool sign /fd SHA256 /f $CertificatePath /p $CertificatePassword $package | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "signtool failed." }
}

$size = [math]::Round((Get-Item $package).Length / 1MB, 1)
Write-Output "Wrote $package ($size MB)"
