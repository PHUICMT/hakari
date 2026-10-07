<#
.SYNOPSIS
    Builds the portable release: Hakari.exe and Hakari.Surfaces.exe side by side in one
    folder, self-contained (no .NET or Windows App SDK install needed), zipped.
.PARAMETER Version
    The version written into the binaries and the zip name, such as 1.0.0.
.PARAMETER Output
    Where the folder and the zip are written.
#>
param(
    [string]$Version = "0.0.0",
    [string]$Output = "artifacts/release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$folder = Join-Path $root "$Output/Hakari"
$zip = Join-Path $root "$Output/Hakari-$Version-win-x64.zip"

if (Test-Path $folder) { Remove-Item $folder -Recurse -Force }
if (Test-Path $zip) { Remove-Item $zip -Force }

$common = @(
    "-c", "Release",
    "-r", "win-x64",
    "--self-contained", "true",
    "-p:Version=$Version",
    "-p:DebugType=none",
    "-o", $folder,
    "--nologo"
)

dotnet publish (Join-Path $root "src/Hakari/Hakari.csproj") @common
if ($LASTEXITCODE -ne 0) { throw "Publishing Hakari.exe failed." }

dotnet publish (Join-Path $root "src/Hakari.Surfaces/Hakari.Surfaces.csproj") @common -p:Platform=x64
if ($LASTEXITCODE -ne 0) { throw "Publishing Hakari.Surfaces.exe failed." }

foreach ($required in "Hakari.exe", "Hakari.Surfaces.exe", "pricing.json") {
    if (-not (Test-Path (Join-Path $folder $required))) { throw "$required is missing." }
}

Compress-Archive -Path $folder -DestinationPath $zip
$size = [math]::Round((Get-Item $zip).Length / 1MB, 1)
Write-Output "Wrote $zip ($size MB)"
