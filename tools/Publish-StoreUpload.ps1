<#
.SYNOPSIS
    Builds the file the Microsoft Store takes for an update: the x64 and arm64 packages in
    one bundle, wrapped as a .msixupload.
.DESCRIPTION
    Packages each architecture with tools/Publish-MSIX.ps1, reusing the portable folders from
    Publish-Release.ps1 when they exist, bundles them with makeappx and zips the bundle as the
    upload. The Store signs it, so nothing here needs a certificate.
.PARAMETER Version
    The version, such as 1.0.0; both packages and the bundle carry it.
.PARAMETER ReleaseFolder
    Where Publish-Release.ps1 left the portable folders, as <ReleaseFolder>/<arch>/Hakari.
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [string]$ReleaseFolder = "artifacts/release",
    [string]$Output = "artifacts/msix"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$kits = "${env:ProgramFiles(x86)}\Windows Kits\10\bin"
$makeAppx = Get-ChildItem $kits -Recurse -Filter makeappx.exe |
    Where-Object FullName -like "*\x64\*" | Sort-Object FullName | Select-Object -Last 1
if (-not $makeAppx) { throw "makeappx.exe not found; install the Windows SDK." }

$packages = Join-Path $root "$Output/bundle-input"
if (Test-Path $packages) { Remove-Item $packages -Recurse -Force }
New-Item -ItemType Directory -Force $packages | Out-Null

foreach ($architecture in "x64", "arm64") {
    $portable = Join-Path $root "$ReleaseFolder/$architecture/Hakari"
    $arguments = @{ Version = $Version; Architecture = $architecture; Output = $Output }
    if (Test-Path (Join-Path $portable "Hakari.exe")) { $arguments.PortableFolder = $portable }
    & (Join-Path $PSScriptRoot "Publish-Msix.ps1") @arguments | Out-Null
    Copy-Item (Join-Path $root "$Output/Hakari-$Version-$architecture.msix") $packages
}

$bundle = Join-Path $root "$Output/Hakari-$Version.msixbundle"
if (Test-Path $bundle) { Remove-Item $bundle -Force }
& $makeAppx.FullName bundle /d $packages /p $bundle /bv "$Version.0" /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw "makeappx bundle failed." }

# A .msixupload is a zip holding the bundle; it is what the Store's upload takes.
$upload = Join-Path $root "$Output/Hakari-$Version.msixupload"
$zip = "$upload.zip"
if (Test-Path $upload) { Remove-Item $upload -Force }
Compress-Archive -Path $bundle -DestinationPath $zip -Force
Move-Item $zip $upload -Force

$size = [math]::Round((Get-Item $upload).Length / 1MB, 1)
Write-Output "Wrote $upload ($size MB)"
