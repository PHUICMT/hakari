<#
.SYNOPSIS
    Writes a release's notes from the commits since the previous version tag: new features,
    fixes and speed-ups, in Markdown for GitHub and plain text for the Store.
.PARAMETER Tag
    The release tag, such as v1.0.0.
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$Tag,
    [string]$Output = "artifacts/notes"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
New-Item -ItemType Directory -Force (Join-Path $root $Output) | Out-Null

$previous = git -C $root describe --tags --abbrev=0 "$Tag^" 2>$null
$range = if ($LASTEXITCODE -eq 0 -and $previous) { "$previous..$Tag" } else { $Tag }
$subjects = git -C $root log $range --format=%s --no-merges

$sections = [ordered]@{ feat = "New"; fix = "Fixed"; perf = "Faster" }
$markdown = New-Object System.Collections.Generic.List[string]
$plain = New-Object System.Collections.Generic.List[string]
foreach ($type in $sections.Keys) {
    $lines = @($subjects | Where-Object { $_ -match "^$type(\([^)]*\))?!?: " } |
        ForEach-Object { $_ -replace "^$type(\([^)]*\))?!?: ", "" } |
        ForEach-Object { $_.Substring(0, 1).ToUpper() + $_.Substring(1) })
    if ($lines.Count -eq 0) { continue }
    $markdown.Add("## $($sections[$type])"); $markdown.Add("")
    $lines | ForEach-Object { $markdown.Add("- $_"); $plain.Add("- $_") }
    $markdown.Add("")
}

if ($markdown.Count -eq 0) { $markdown.Add("Maintenance release."); $plain.Add("Maintenance release.") }
$markdown.Add("Download the zip for your PC (x64, or arm64 for Windows on Arm), unzip it and run ``Hakari.exe``. To update, quit Hakari from the tray and replace the folder; settings stay.")

# The Store shows plain text and allows 1500 characters.
$storeText = ($plain -join "`n")
if ($storeText.Length -gt 1500) { $storeText = $storeText.Substring(0, 1497) + "..." }

Set-Content (Join-Path $root "$Output/release.md") ($markdown -join "`n") -Encoding utf8
Set-Content (Join-Path $root "$Output/store.txt") $storeText -Encoding utf8
Write-Output "Notes for $Tag from $range"
