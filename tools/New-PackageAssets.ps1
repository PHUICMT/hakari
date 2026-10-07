<#
.SYNOPSIS
    Draws the MSIX tile and logo pictures from the Hakari mark into packaging/msix/Assets.
.DESCRIPTION
    The same balance-scale mark as tools/New-AppIcon.ps1, drawn at each picture's size (at
    twice the base size, which Windows scales down for 100% displays). Square pictures are the
    mark itself; the wide tile centres it on a transparent background.
#>
param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\packaging\msix\Assets')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$gridSize = 32.0
$cornerRadius = 7.0
$strokeWidth = 2.4
$background = [System.Drawing.ColorTranslator]::FromHtml('#2f4c8c')

function Draw-Mark([System.Drawing.Graphics]$graphics, [float]$left, [float]$top, [float]$size) {
    $state = $graphics.Save()
    $graphics.TranslateTransform($left, $top)
    $graphics.ScaleTransform($size / $gridSize, $size / $gridSize)

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

    $pen.Dispose(); $brush.Dispose(); $path.Dispose()
    $graphics.Restore($state)
}

function Save-Picture([string]$name, [int]$width, [int]$height, [float]$markShare) {
    $bitmap = New-Object System.Drawing.Bitmap $width, $height
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = 'AntiAlias'
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $size = [Math]::Min($width, $height) * $markShare
    Draw-Mark $graphics (($width - $size) / 2) (($height - $size) / 2) $size
    $graphics.Dispose()
    $bitmap.Save((Join-Path $OutputDirectory $name), [System.Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
}

New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
Save-Picture 'StoreLogo.png' 100 100 1.0
Save-Picture 'Square44x44Logo.png' 88 88 1.0
Save-Picture 'Square150x150Logo.png' 300 300 0.66
Save-Picture 'Wide310x150Logo.png' 620 300 0.66
Write-Output "Wrote package pictures to $OutputDirectory"
