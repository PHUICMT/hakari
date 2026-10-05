<#
.SYNOPSIS
    Draws the Hakari mark (a balance scale on the brand indigo) into assets/Hakari.ico.
.DESCRIPTION
    Same artwork as the tray icon (src/Hakari.Taskbar/Tray/TrayIconArtwork.cs), drawn on a
    32-unit grid and scaled per size, so every size is drawn sharp rather than resampled.
    Each entry is stored as PNG, which Windows reads for all sizes.
#>
param(
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\assets\Hakari.ico')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$gridSize = 32.0
$cornerRadius = 7.0
$strokeWidth = 2.4
$background = [System.Drawing.ColorTranslator]::FromHtml('#2f4c8c')

function New-MarkPng([int]$size) {
    $scale = $size / $gridSize
    $bitmap = New-Object System.Drawing.Bitmap $size, $size
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = 'AntiAlias'
    $graphics.ScaleTransform($scale, $scale)

    $diameter = $cornerRadius * 2
    $edge = $gridSize - $diameter
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc(0, 0, $diameter, $diameter, 180, 90)
    $path.AddArc($edge, 0, $diameter, $diameter, 270, 90)
    $path.AddArc($edge, $edge, $diameter, $diameter, 0, 90)
    $path.AddArc(0, $edge, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    $brush = New-Object System.Drawing.SolidBrush $background
    $graphics.FillPath($brush, $path)

    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), $strokeWidth
    $pen.StartCap = 'Round'; $pen.EndCap = 'Round'; $pen.LineJoin = 'Round'
    $graphics.DrawLine($pen, 16, 7, 16, 25)
    $graphics.DrawLine($pen, 11, 25, 21, 25)
    $graphics.DrawLine($pen, 7, 10, 25, 10)
    $graphics.DrawLine($pen, 9, 10, 6, 17)
    $graphics.DrawLine($pen, 9, 10, 12, 17)
    $graphics.DrawArc($pen, 5, 13, 8, 7, 0, 180)
    $graphics.DrawLine($pen, 23, 10, 20, 17)
    $graphics.DrawLine($pen, 23, 10, 26, 17)
    $graphics.DrawArc($pen, 19, 13, 8, 7, 0, 180)

    $stream = New-Object System.IO.MemoryStream
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $pen.Dispose(); $brush.Dispose(); $path.Dispose(); $graphics.Dispose(); $bitmap.Dispose()
    return , $stream.ToArray()
}

$images = foreach ($size in $sizes) { , (New-MarkPng $size) }

$headerBytes = 6
$entryBytes = 16
$output = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter $output
$writer.Write([uint16]0)
$writer.Write([uint16]1)
$writer.Write([uint16]$sizes.Count)

$offset = $headerBytes + $entryBytes * $sizes.Count
for ($index = 0; $index -lt $sizes.Count; $index++) {
    $size = $sizes[$index]
    $dimension = if ($size -ge 256) { 0 } else { $size }
    $writer.Write([byte]$dimension)
    $writer.Write([byte]$dimension)
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]32)
    $writer.Write([uint32]$images[$index].Length)
    $writer.Write([uint32]$offset)
    $offset += $images[$index].Length
}

foreach ($image in $images) { $writer.Write($image) }
$writer.Flush()

$directory = Split-Path $OutputPath -Parent
New-Item -ItemType Directory -Force $directory | Out-Null
[System.IO.File]::WriteAllBytes((Resolve-Path $directory).Path + '\' + (Split-Path $OutputPath -Leaf), $output.ToArray())
Write-Host "Wrote $OutputPath ($($sizes -join ', ') px)"
