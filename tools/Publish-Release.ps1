<#
.SYNOPSIS
    Builds the portable release: Hakari.exe and Hakari.Surfaces.exe side by side in one
    folder, self-contained (no .NET or Windows App SDK install needed), zipped.
.PARAMETER Version
    The version written into the binaries and the zip name, such as 1.0.0.
.PARAMETER Output
    Where the folder and the zip are written.
.PARAMETER Architecture
    x64 or arm64; arm64 runs natively on Windows on Arm PCs.
#>
param(
    [string]$Version = "0.0.0",
    [string]$Output = "artifacts/release",
    [ValidateSet("x64", "arm64")]
    [string]$Architecture = "x64"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$runtime = "win-$Architecture"
$platform = if ($Architecture -eq "arm64") { "ARM64" } else { "x64" }
$folder = Join-Path $root "$Output/$Architecture/Hakari"
$zip = Join-Path $root "$Output/Hakari-$Version-$runtime.zip"

if (Test-Path $folder) { Remove-Item $folder -Recurse -Force }
if (Test-Path $zip) { Remove-Item $zip -Force }

$common = @(
    "-c", "Release",
    "-r", $runtime,
    "--self-contained", "true",
    "-p:Version=$Version",
    "-p:DebugType=none",
    "-o", $folder,
    "--nologo"
)

dotnet publish (Join-Path $root "src/Hakari/Hakari.csproj") @common
if ($LASTEXITCODE -ne 0) { throw "Publishing Hakari.exe failed." }

dotnet publish (Join-Path $root "src/Hakari.Surfaces/Hakari.Surfaces.csproj") @common -p:Platform=$platform
if ($LASTEXITCODE -ne 0) { throw "Publishing Hakari.Surfaces.exe failed." }

# The release notes, when the release build wrote them, go in as the "what's new" text.
$notes = Join-Path $root "artifacts/notes/release.md"
if (Test-Path $notes) { Copy-Item $notes (Join-Path $folder "whats-new.md") }

foreach ($required in "Hakari.exe", "Hakari.Surfaces.exe", "pricing.json") {
    if (-not (Test-Path (Join-Path $folder $required))) { throw "$required is missing." }
}

Compress-Archive -Path $folder -DestinationPath $zip
$size = [math]::Round((Get-Item $zip).Length / 1MB, 1)
Write-Output "Wrote $zip ($size MB)"
