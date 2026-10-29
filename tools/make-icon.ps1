# Draws the AsciiWall icon (three drifting wave lines on a dark tile) and writes
#   desktop\assets\AsciiWall.ico   (16 to 256 px, used by the exe, installer and shortcuts)
#   desktop\web\icon.png           (used by the settings window)
# Run with Windows PowerShell:  powershell -NoProfile -File make-icon.ps1
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot
$assets = Join-Path $root 'assets'
New-Item -ItemType Directory -Force $assets | Out-Null

function New-RoundRect([single]$x, [single]$y, [single]$w, [single]$h, [single]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $p.AddArc($x, $y, $r * 2, $r * 2, 180, 90)
    $p.AddArc($x + $w - $r * 2, $y, $r * 2, $r * 2, 270, 90)
    $p.AddArc($x + $w - $r * 2, $y + $h - $r * 2, $r * 2, $r * 2, 0, 90)
    $p.AddArc($x, $y + $h - $r * 2, $r * 2, $r * 2, 90, 90)
    $p.CloseFigure()
    return $p
}

function New-Tile([int]$s) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $path = New-RoundRect 0.5 0.5 ($s - 1) ($s - 1) ($s * 0.23)
    $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.RectangleF 0, 0, $s, $s), ([System.Drawing.Color]::FromArgb(38, 46, 74)), ([System.Drawing.Color]::FromArgb(11, 13, 24)), 70
    $g.FillPath($bg, $path)
    $edge = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(70, 255, 255, 255)), ([Math]::Max(1, $s / 128))
    $g.DrawPath($edge, $path)

    $colors = @(
        [System.Drawing.Color]::FromArgb(255, 124, 240, 201),
        [System.Drawing.Color]::FromArgb(235, 143, 184, 255),
        [System.Drawing.Color]::FromArgb(215, 185, 162, 255)
    )
    $ys = @(0.33, 0.52, 0.71)
    for ($row = 0; $row -lt 3; $row++) {
        $pts = New-Object 'System.Collections.Generic.List[System.Drawing.PointF]'
        for ($i = 0; $i -le 14; $i++) {
            $x = $s * (0.17 + 0.66 * $i / 14.0)
            $y = $s * $ys[$row] + [Math]::Sin($i / 14.0 * [Math]::PI * 2 + $row * 1.3) * $s * 0.072
            $pts.Add((New-Object System.Drawing.PointF ([single]$x), ([single]$y)))
        }
        $pen = New-Object System.Drawing.Pen $colors[$row], ([Math]::Max(1.2, $s * 0.075))
        $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $g.DrawCurve($pen, $pts.ToArray(), 0.5)
        $pen.Dispose()
    }
    $g.Dispose(); $bg.Dispose(); $edge.Dispose(); $path.Dispose()
    return $bmp
}

$sizes = 16, 24, 32, 48, 64, 128, 256
$pngs = @()
foreach ($s in $sizes) {
    $bmp = New-Tile $s
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngs += , $ms.ToArray()
    if ($s -eq 256) { $bmp.Save((Join-Path $root 'web\icon.png'), [System.Drawing.Imaging.ImageFormat]::Png) }
    $bmp.Dispose(); $ms.Dispose()
}

# ICO container with PNG-compressed images (supported since Windows Vista).
$out = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $out
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]
    $dim = if ($s -ge 256) { 0 } else { $s }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$pngs[$i].Length); $w.Write([uint32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $w.Write($p) }
$w.Flush()
[System.IO.File]::WriteAllBytes((Join-Path $assets 'AsciiWall.ico'), $out.ToArray())
Write-Host "Wrote $assets\AsciiWall.ico and web\icon.png"
