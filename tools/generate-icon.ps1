param(
    [string]$OutputPath = (Join-Path $PSScriptRoot "..\Assets\launcher.ico")
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$sizes = @(16, 32, 48, 256)
$frames = @()

foreach ($size in $sizes) {
    $bitmap = [System.Drawing.Bitmap]::new(
        $size,
        $size,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.Clear([System.Drawing.Color]::Transparent)

        $background = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
            [System.Drawing.Rectangle]::new(0, 0, $size, $size),
            [System.Drawing.Color]::FromArgb(255, 14, 42, 53),
            [System.Drawing.Color]::FromArgb(255, 25, 126, 119),
            45.0)
        try {
            $inset = [Math]::Max(1, [int]($size * 0.04))
            $graphics.FillEllipse(
                $background,
                $inset,
                $inset,
                $size - (2 * $inset),
                $size - (2 * $inset))
        }
        finally {
            $background.Dispose()
        }

        $scale = $size / 256.0
        $chevronPen = [System.Drawing.Pen]::new(
            [System.Drawing.Color]::FromArgb(245, 238, 251, 231),
            [Math]::Max(1.5, 24 * $scale))
        $chevronPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $chevronPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $chevronPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
        try {
            $points = @(
                [System.Drawing.PointF]::new(78 * $scale, 88 * $scale),
                [System.Drawing.PointF]::new(142 * $scale, 48 * $scale),
                [System.Drawing.PointF]::new(194 * $scale, 86 * $scale),
                [System.Drawing.PointF]::new(118 * $scale, 130 * $scale),
                [System.Drawing.PointF]::new(177 * $scale, 170 * $scale),
                [System.Drawing.PointF]::new(112 * $scale, 208 * $scale),
                [System.Drawing.PointF]::new(62 * $scale, 166 * $scale)
            )
            $graphics.DrawLines($chevronPen, $points)
        }
        finally {
            $chevronPen.Dispose()
        }

        $accent = [System.Drawing.SolidBrush]::new(
            [System.Drawing.Color]::FromArgb(255, 126, 232, 190))
        try {
            $diamond = @(
                [System.Drawing.PointF]::new(194 * $scale, 86 * $scale),
                [System.Drawing.PointF]::new(218 * $scale, 112 * $scale),
                [System.Drawing.PointF]::new(190 * $scale, 140 * $scale),
                [System.Drawing.PointF]::new(166 * $scale, 112 * $scale)
            )
            $graphics.FillPolygon($accent, $diamond)
        }
        finally {
            $accent.Dispose()
        }

        $memory = [System.IO.MemoryStream]::new()
        try {
            $bitmap.Save($memory, [System.Drawing.Imaging.ImageFormat]::Png)
            $frames += ,$memory.ToArray()
        }
        finally {
            $memory.Dispose()
        }
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

$outputDirectory = Split-Path -Parent $OutputPath
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
$stream = [System.IO.File]::Create($OutputPath)
$writer = [System.IO.BinaryWriter]::new($stream)
try {
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$frames.Count)

    $offset = 6 + (16 * $frames.Count)
    for ($index = 0; $index -lt $frames.Count; $index++) {
        $size = $sizes[$index]
        $dimension = [byte]0
        if ($size -ne 256) {
            $dimension = [byte]$size
        }
        $writer.Write($dimension)
        $writer.Write($dimension)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$frames[$index].Length)
        $writer.Write([uint32]$offset)
        $offset += $frames[$index].Length
    }

    foreach ($frame in $frames) {
        $writer.Write($frame)
    }
}
finally {
    $writer.Dispose()
    $stream.Dispose()
}

Write-Output "Generated $OutputPath with frames: $($sizes -join ', ')"
