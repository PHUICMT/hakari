<#
.SYNOPSIS
    Draws the Store listing pictures into docs/store/art: 1:1 box art, 9:16 poster art and
    the 16:9 super hero art, all from the Hakari mark on the brand's night-blue gradient.
.DESCRIPTION
    The box and poster art carry the name; the super hero art must not, by Store rule.
#>
param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\docs\store\art')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$gridSize = 32.0
$cornerRadius = 7.0
$strokeWidth = 2.4
$markColor = [System.Drawing.ColorTranslator]::FromHtml('#2f4c8c')
$skyTop = [System.Drawing.ColorTranslator]::FromHtml('#46609e')
$skyBottom = [System.Drawing.ColorTranslator]::FromHtml('#0b0e1c')
$inkMuted = [System.Drawing.ColorTranslator]::FromHtml('#b9c8ea')

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
    $brush = New-Object System.Drawing.SolidBrush $markColor
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

function New-Canvas([int]$width, [int]$height) {
    $bitmap = New-Object System.Drawing.Bitmap $width, $height
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = 'AntiAlias'
    $graphics.TextRenderingHint = 'AntiAliasGridFit'
    $area = New-Object System.Drawing.Rectangle 0, 0, $width, $height
    $sky = New-Object System.Drawing.Drawing2D.LinearGradientBrush $area, $skyTop, $skyBottom, 60
    $graphics.FillRectangle($sky, $area)
    $sky.Dispose()
    return $bitmap, $graphics
}

function Draw-Text($graphics, [string]$text, [float]$size, $color, [float]$centerX, [float]$top, [string]$weight) {
    $style = if ($weight -eq 'bold') { [System.Drawing.FontStyle]::Bold } else { [System.Drawing.FontStyle]::Regular }
    $font = New-Object System.Drawing.Font 'Segoe UI', $size, $style, ([System.Drawing.GraphicsUnit]::Pixel)
    $brush = New-Object System.Drawing.SolidBrush $color
    $measured = $graphics.MeasureString($text, $font)
    $graphics.DrawString($text, $font, $brush, $centerX - $measured.Width / 2, $top)
    $font.Dispose(); $brush.Dispose()
}

function Save([System.Drawing.Bitmap]$bitmap, $graphics, [string]$name) {
    $graphics.Dispose()
    $bitmap.Save((Join-Path $OutputDirectory $name), [System.Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
}

New-Item -ItemType Directory -Force $OutputDirectory | Out-Null

# 1:1 box art, 2160 x 2160: the mark with the name under it.
$bitmap, $graphics = New-Canvas 2160 2160
Draw-Mark $graphics 680 520 800
Draw-Text $graphics 'Hakari' 260 ([System.Drawing.Color]::White) 1080 1400 'bold'
Draw-Text $graphics 'Usage Meter' 120 $inkMuted 1080 1720 'regular'
Save $bitmap $graphics 'box-art-2160.png'

# 9:16 poster art, 1440 x 2160.
$bitmap, $graphics = New-Canvas 1440 2160
Draw-Mark $graphics 370 560 700
Draw-Text $graphics 'Hakari' 230 ([System.Drawing.Color]::White) 720 1340 'bold'
Draw-Text $graphics 'Usage Meter' 110 $inkMuted 720 1620 'regular'
Save $bitmap $graphics 'poster-art-1440x2160.png'

# 16:9 super hero art, 3840 x 2160: no name, by Store rule.
$bitmap, $graphics = New-Canvas 3840 2160
Draw-Mark $graphics 1520 680 800
Save $bitmap $graphics 'super-hero-3840x2160.png'

Write-Output "Wrote Store art to $OutputDirectory"
