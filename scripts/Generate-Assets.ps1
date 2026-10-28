<#
.SYNOPSIS
    Generates high-resolution raster assets (PNGs) for BusinessHarness.
.DESCRIPTION
    Renders icons, logo lockups, README header banner, and GitHub repository cover image
    using native .NET System.Drawing / GDI+.
#>

[CmdletBinding()]
param(
    [string]$OutputDir
)

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { (Get-Location).Path }
    $OutputDir = Join-Path $scriptDir "..\assets"
}
$OutputDir = [System.IO.Path]::GetFullPath($OutputDir)

Add-Type -AssemblyName System.Drawing

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

function New-RoundedPath([System.Drawing.RectangleF]$rect, [float]$radius) {
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $d = [Math]::Max(1.0, $radius * 2)
    $arc = [System.Drawing.RectangleF]::new($rect.X, $rect.Y, $d, $d)
    
    $path.AddArc($arc, 180, 90)
    $arc.X = $rect.Right - $d
    $path.AddArc($arc, 270, 90)
    $arc.Y = $rect.Bottom - $d
    $path.AddArc($arc, 0, 90)
    $arc.X = $rect.Left
    $path.AddArc($arc, 90, 90)
    $path.CloseFigure()
    return $path
}

# --- 1. GENERATE ICON (512x512) ---
Write-Host "Rendering icon.png (512x512)..."
$iconSize = 512
$iconBmp = [System.Drawing.Bitmap]::new($iconSize, $iconSize)
$g = [System.Drawing.Graphics]::FromImage($iconBmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit

# Background Squircle: Deep Obsidian Slate
$bgRect = [System.Drawing.RectangleF]::new(0, 0, $iconSize, $iconSize)
$bgPath = New-RoundedPath $bgRect 116
$bgBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
    [System.Drawing.PointF]::new(0, 0),
    [System.Drawing.PointF]::new($iconSize, $iconSize),
    [System.Drawing.Color]::FromArgb(255, 24, 34, 28),
    [System.Drawing.Color]::FromArgb(255, 10, 14, 11)
)
$g.FillPath($bgBrush, $bgPath)

# Warm Ambient Glow
$glowPath = [System.Drawing.Drawing2D.GraphicsPath]::new()
$glowPath.AddEllipse(30, 20, 400, 400)
$pbg = [System.Drawing.Drawing2D.PathGradientBrush]::new($glowPath)
$pbg.CenterColor = [System.Drawing.Color]::FromArgb(40, 224, 90, 54)
$pbg.SurroundColors = @([System.Drawing.Color]::FromArgb(0, 10, 14, 11))
$g.FillPath($pbg, $bgPath)
$pbg.Dispose()
$glowPath.Dispose()

# Subtle Outer Rim
$rimPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(150, 47, 66, 54), 2)
$rimRect = [System.Drawing.RectangleF]::new(2, 2, $iconSize - 4, $iconSize - 4)
$rimPath = New-RoundedPath $rimRect 114
$g.DrawPath($rimPen, $rimPath)
$rimPen.Dispose()
$rimPath.Dispose()

# Monogram Glyph Geometry
$creamColor = [System.Drawing.Color]::FromArgb(255, 246, 245, 241)

# 'b' Spine & Loop
$bPen = [System.Drawing.Pen]::new($creamColor, 38)
$bPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$bPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$bPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round

$bPath = [System.Drawing.Drawing2D.GraphicsPath]::new()
$bPath.AddLine(144, 130, 144, 382)
$bPath.AddLine(144, 382, 200, 382)
$bPath.AddBezier(200, 382, 272, 382, 272, 230, 200, 230)
$bPath.AddLine(200, 230, 144, 230)
$g.DrawPath($bPen, $bPath)

# 'h' Center Spine
$g.DrawLine($bPen, 272, 130, 272, 382)
$bPen.Dispose()
$bPath.Dispose()

# 'h' Dynamic Harness Arch
$terraBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
    [System.Drawing.PointF]::new(272, 160),
    [System.Drawing.PointF]::new(368, 382),
    [System.Drawing.Color]::FromArgb(255, 255, 127, 89),
    [System.Drawing.Color]::FromArgb(255, 186, 60, 27)
)
$hPen = [System.Drawing.Pen]::new($terraBrush, 38)
$hPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$hPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$hPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round

$hArchPath = [System.Drawing.Drawing2D.GraphicsPath]::new()
$hArchPath.AddBezier(272, 230, 272, 160, 368, 160, 368, 230)
$hArchPath.AddLine(368, 230, 368, 382)
$g.DrawPath($hPen, $hArchPath)
$hPen.Dispose()
$terraBrush.Dispose()
$hArchPath.Dispose()

# Verification Status Beacon (Green on foot)
$greenBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 140, 224, 160))
$greenGlowPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(90, 140, 224, 160), 4)
$g.FillEllipse($greenBrush, 360, 374, 16, 16)
$g.DrawEllipse($greenGlowPen, 354, 368, 28, 28)
$greenGlowPen.Dispose()
$greenBrush.Dispose()

# Anchor Telemetry Dot at Arch Peak
$terraNodeBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(230, 255, 155, 122))
$g.FillEllipse($terraNodeBrush, 314, 172, 12, 12)
$terraNodeBrush.Dispose()

# Save Master icon.png
$iconPath = Join-Path $OutputDir "icon.png"
$iconBmp.Save($iconPath, [System.Drawing.Imaging.ImageFormat]::Png)

# Resample Smaller Icon Sizes (256, 128, 64, 32)
foreach ($size in @(256, 128, 64, 32)) {
    $subBmp = [System.Drawing.Bitmap]::new($size, $size)
    $subG = [System.Drawing.Graphics]::FromImage($subBmp)
    $subG.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $subG.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $subG.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $subG.DrawImage($iconBmp, 0, 0, $size, $size)
    $subPath = Join-Path $OutputDir "icon-$size.png"
    $subBmp.Save($subPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $subG.Dispose()
    $subBmp.Dispose()
    Write-Host "  -> Generated $subPath"
}

$g.Dispose()
$iconBmp.Dispose()
$bgBrush.Dispose()
$bgPath.Dispose()


# --- 2. GENERATE LOGO (800x180) ---
Write-Host "Rendering logo.png (800x180)..."
$logoW = 800
$logoH = 180
$logoBmp = [System.Drawing.Bitmap]::new($logoW, $logoH)
$lg = [System.Drawing.Graphics]::FromImage($logoBmp)
$lg.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$lg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$lg.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit

# Background Container Box
$logoRect = [System.Drawing.RectangleF]::new(0, 0, $logoW, $logoH)
$logoPath = New-RoundedPath $logoRect 16
$logoBgBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
    [System.Drawing.PointF]::new(0, 0),
    [System.Drawing.PointF]::new($logoW, $logoH),
    [System.Drawing.Color]::FromArgb(255, 20, 28, 23),
    [System.Drawing.Color]::FromArgb(255, 14, 20, 16)
)
$lg.FillPath($logoBgBrush, $logoPath)
$logoBorderPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 42, 58, 47), 1.5)
$lg.DrawPath($logoBorderPen, $logoPath)
$logoBorderPen.Dispose()

# Emblem Box on Left (128x128 at x=24, y=26)
$emblemRect = [System.Drawing.RectangleF]::new(24, 26, 128, 128)
$emblemPath = New-RoundedPath $emblemRect 28
$emblemBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
    [System.Drawing.PointF]::new(24, 26),
    [System.Drawing.PointF]::new(24, 154),
    [System.Drawing.Color]::FromArgb(255, 38, 52, 43),
    [System.Drawing.Color]::FromArgb(255, 20, 28, 23)
)
$lg.FillPath($emblemBrush, $emblemPath)
$emblemBorder = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 55, 75, 63), 1.5)
$lg.DrawPath($emblemBorder, $emblemPath)
$emblemBorder.Dispose()
$emblemBrush.Dispose()
$emblemPath.Dispose()

# Emblem Glyph inside Box
$mBPen = [System.Drawing.Pen]::new($creamColor, 9.5)
$mBPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$mBPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$mBPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round

# 'b'
$mBPath = [System.Drawing.Drawing2D.GraphicsPath]::new()
$mBPath.AddLine(60, 58, 60, 122)
$mBPath.AddLine(60, 122, 74, 122)
$mBPath.AddBezier(74, 122, 92, 122, 92, 84, 74, 84)
$mBPath.AddLine(74, 84, 60, 84)
$lg.DrawPath($mBPen, $mBPath)

# 'h' stem
$lg.DrawLine($mBPen, 92, 58, 92, 122)
$mBPen.Dispose()
$mBPath.Dispose()

# 'h' arch
$mArchPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 224, 90, 54), 9.5)
$mArchPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$mArchPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$mArchPath = [System.Drawing.Drawing2D.GraphicsPath]::new()
$mArchPath.AddBezier(92, 84, 92, 66, 116, 66, 116, 84)
$mArchPath.AddLine(116, 84, 116, 122)
$lg.DrawPath($mArchPen, $mArchPath)
$mArchPen.Dispose()
$mArchPath.Dispose()

# Green beacon dot
$mGreen = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 140, 224, 160))
$lg.FillEllipse($mGreen, 113, 119, 7, 7)
$mGreen.Dispose()

# Brand Name Typography
$fontTitle = [System.Drawing.Font]::new("Segoe UI", 38, [System.Drawing.FontStyle]::Bold)
$titleBrush = [System.Drawing.SolidBrush]::new($creamColor)
$lg.DrawString("BusinessHarness", $fontTitle, $titleBrush, 176, 42)
$titleBrush.Dispose()

# Dot '.' in terracotta
$dotSize = $lg.MeasureString("BusinessHarness", $fontTitle)
$dotBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 224, 90, 54))
$lg.DrawString(".", $fontTitle, $dotBrush, 176 + $dotSize.Width - 14, 42)
$dotBrush.Dispose()
$fontTitle.Dispose()

# Subtitle / Tagline
$fontSub = [System.Drawing.Font]::new("Consolas", 10, [System.Drawing.FontStyle]::Bold)
$subBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 148, 168, 156))
$lg.DrawString("BOUNDED DIFF REVIEW  •  SPEC VALIDATION  •  CI GATE", $fontSub, $subBrush, 180, 110)
$subBrush.Dispose()
$fontSub.Dispose()

# Right Badges
$fontBadge = [System.Drawing.Font]::new("Consolas", 8.5, [System.Drawing.FontStyle]::Bold)
$badgeBgBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 24, 34, 28))
$badgeBorderPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 46, 63, 52), 1.2)

# Badge 1: .NET 10 CLI
$b1Rect = [System.Drawing.RectangleF]::new(646, 44, 120, 28)
$b1Path = New-RoundedPath $b1Rect 5
$lg.FillPath($badgeBgBrush, $b1Path)
$lg.DrawPath($badgeBorderPen, $b1Path)
$b1Path.Dispose()
$b1TextBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 140, 224, 160))
$lg.DrawString(".NET 10 CLI", $fontBadge, $b1TextBrush, 666, 50)
$b1TextBrush.Dispose()

# Badge 2: AI BOUNDED
$b2Rect = [System.Drawing.RectangleF]::new(646, 78, 120, 28)
$b2Path = New-RoundedPath $b2Rect 5
$lg.FillPath($badgeBgBrush, $b2Path)
$lg.DrawPath($badgeBorderPen, $b2Path)
$b2Path.Dispose()
$b2TextBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 224, 90, 54))
$lg.DrawString("AI BOUNDED", $fontBadge, $b2TextBrush, 668, 84)
$b2TextBrush.Dispose()

# Badge 3: ZERO LEAK
$b3Rect = [System.Drawing.RectangleF]::new(646, 112, 120, 26)
$b3Path = New-RoundedPath $b3Rect 5
$lg.FillPath($badgeBgBrush, $b3Path)
$lg.DrawPath($badgeBorderPen, $b3Path)
$b3Path.Dispose()
$b3TextBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 136, 157, 144))
$lg.DrawString("ZERO LEAK", $fontBadge, $b3TextBrush, 672, 118)
$b3TextBrush.Dispose()

$badgeBgBrush.Dispose()
$badgeBorderPen.Dispose()
$fontBadge.Dispose()

$logoPathOut = Join-Path $OutputDir "logo.png"
$logoBmp.Save($logoPathOut, [System.Drawing.Imaging.ImageFormat]::Png)
Write-Host "  -> Generated $logoPathOut"

$lg.Dispose()
$logoBmp.Dispose()
$logoBgBrush.Dispose()
$logoPath.Dispose()


# --- 3. GENERATE README HEADER (1280x360) ---
Write-Host "Rendering readme-header.png (1280x360)..."
$hdrW = 1280
$hdrH = 360
$hdrBmp = [System.Drawing.Bitmap]::new($hdrW, $hdrH)
$hg = [System.Drawing.Graphics]::FromImage($hdrBmp)
$hg.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$hg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$hg.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit

# Header Background Canvas: Smooth, borderless, no clunky lines!
$hdrRect = [System.Drawing.RectangleF]::new(0, 0, $hdrW, $hdrH)
$hdrCanvasPath = New-RoundedPath $hdrRect 14
$hdrBgBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
    [System.Drawing.PointF]::new(0, 0),
    [System.Drawing.PointF]::new($hdrW, $hdrH),
    [System.Drawing.Color]::FromArgb(255, 18, 24, 20),
    [System.Drawing.Color]::FromArgb(255, 14, 20, 16)
)
$hg.FillPath($hdrBgBrush, $hdrCanvasPath)

# Warm Ambient Glow on Left
$hdrGlowPath = [System.Drawing.Drawing2D.GraphicsPath]::new()
$hdrGlowPath.AddEllipse(20, 20, 450, 300)
$hpbg = [System.Drawing.Drawing2D.PathGradientBrush]::new($hdrGlowPath)
$hpbg.CenterColor = [System.Drawing.Color]::FromArgb(35, 224, 90, 54)
$hpbg.SurroundColors = @([System.Drawing.Color]::FromArgb(0, 14, 20, 16))
$hg.FillPath($hpbg, $hdrCanvasPath)
$hpbg.Dispose()
$hdrGlowPath.Dispose()

# Subtle Dot Matrix (clean and discreet, NOT harsh lines)
$dotBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(35, 45, 61, 50))
for ($dx = 24; $dx -lt $hdrW; $dx += 24) {
    for ($dy = 24; $dy -lt $hdrH; $dy += 24) {
        $hg.FillEllipse($dotBrush, $dx, $dy, 2, 2)
    }
}
$dotBrush.Dispose()

# Minimal Frame Border
$hdrBorder = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 37, 53, 42), 1.5)
$hg.DrawPath($hdrBorder, $hdrCanvasPath)
$hdrBorder.Dispose()

# Left Column: Eyebrow + Emblem + Title + Description + Pills
# Eyebrow
$fontEyebrow = [System.Drawing.Font]::new("Consolas", 9.5, [System.Drawing.FontStyle]::Bold)
$eyeBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 140, 224, 160))
$hg.FillEllipse($eyeBrush, 64, 46, 8, 8)
$hg.DrawString("REVIEW ENGINE // BOUNDED CI CONTEXT", $fontEyebrow, $eyeBrush, 80, 42)
$eyeBrush.Dispose()
$fontEyebrow.Dispose()

# Emblem Box (68x68 at x=64, y=72)
$boxRect = [System.Drawing.RectangleF]::new(64, 72, 68, 68)
$boxPath = New-RoundedPath $boxRect 16
$boxBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
    [System.Drawing.PointF]::new(64, 72),
    [System.Drawing.PointF]::new(64, 140),
    [System.Drawing.Color]::FromArgb(255, 38, 52, 43),
    [System.Drawing.Color]::FromArgb(255, 20, 28, 23)
)
$hg.FillPath($boxBrush, $boxPath)
$boxBorder = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 55, 75, 63), 1.5)
$hg.DrawPath($boxBorder, $boxPath)
$boxBorder.Dispose()
$boxBrush.Dispose()
$boxPath.Dispose()

# Emblem Glyph in Box
$mbPen = [System.Drawing.Pen]::new($creamColor, 5.5)
$mbPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$mbPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$mbPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round

$mbPath = [System.Drawing.Drawing2D.GraphicsPath]::new()
$mbPath.AddLine(84, 89, 84, 123)
$mbPath.AddLine(84, 123, 91, 123)
$mbPath.AddBezier(91, 123, 101, 123, 101, 103, 91, 103)
$mbPath.AddLine(91, 103, 84, 103)
$hg.DrawPath($mbPen, $mbPath)
$hg.DrawLine($mbPen, 101, 89, 101, 123)
$mbPen.Dispose()
$mbPath.Dispose()

$mhArchPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 224, 90, 54), 5.5)
$mhArchPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$mhArchPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$mhArchPath = [System.Drawing.Drawing2D.GraphicsPath]::new()
$mhArchPath.AddBezier(101, 103, 101, 93, 114, 93, 114, 103)
$mhArchPath.AddLine(114, 103, 114, 123)
$hg.DrawPath($mhArchPen, $mhArchPath)
$mhArchPen.Dispose()
$mhArchPath.Dispose()

# Title "BusinessHarness."
$fontHdrTitle = [System.Drawing.Font]::new("Segoe UI", 34, [System.Drawing.FontStyle]::Bold)
$hdrTitleBrush = [System.Drawing.SolidBrush]::new($creamColor)
$hg.DrawString("BusinessHarness", $fontHdrTitle, $hdrTitleBrush, 146, 68)
$hdrTitleBrush.Dispose()

$hdrDotBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 224, 90, 54))
$titleMeasure = $hg.MeasureString("BusinessHarness", $fontHdrTitle)
$hg.DrawString(".", $fontHdrTitle, $hdrDotBrush, 146 + $titleMeasure.Width - 12, 68)
$hdrDotBrush.Dispose()
$fontHdrTitle.Dispose()

# Tagline
$fontHdrTag = [System.Drawing.Font]::new("Segoe UI", 13.5, [System.Drawing.FontStyle]::Bold)
$hdrTagBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 202, 212, 204))
$hg.DrawString("Put every change in context.", $fontHdrTag, $hdrTagBrush, 150, 118)
$hdrTagBrush.Dispose()
$fontHdrTag.Dispose()

# Description Paragraph
$fontHdrDesc = [System.Drawing.Font]::new("Segoe UI", 11.5)
$hdrDescBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 142, 159, 147))
$hg.DrawString("Connect code diffs to requirements and specifications.", $fontHdrDesc, $hdrDescBrush, 64, 162)
$hg.DrawString("Bounded AI review, reference validation, and opt-in memory.", $fontHdrDesc, $hdrDescBrush, 64, 184)
$hdrDescBrush.Dispose()
$fontHdrDesc.Dispose()

# Capability Pills
$fontPill = [System.Drawing.Font]::new("Consolas", 9, [System.Drawing.FontStyle]::Bold)
$pillBg = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 24, 34, 28))
$pillBorder = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 46, 63, 52), 1.2)

$pills = @(
    @{ text = ".NET 10"; color = [System.Drawing.Color]::FromArgb(255, 140, 224, 160); w = 84 },
    @{ text = "OpenAI + Claude"; color = [System.Drawing.Color]::FromArgb(255, 224, 90, 54); w = 138 },
    @{ text = "Jira & JSON APIs"; color = [System.Drawing.Color]::FromArgb(255, 157, 179, 164); w = 142 },
    @{ text = "Zero Leaks"; color = [System.Drawing.Color]::FromArgb(255, 157, 179, 164); w = 104 }
)
$curX = 64
foreach ($p in $pills) {
    $pRect = [System.Drawing.RectangleF]::new($curX, 230, $p.w, 28)
    $pPath = New-RoundedPath $pRect 5
    $hg.FillPath($pillBg, $pPath)
    $hg.DrawPath($pillBorder, $pPath)
    $pPath.Dispose()
    
    $pBrush = [System.Drawing.SolidBrush]::new($p.color)
    $pSize = $hg.MeasureString($p.text, $fontPill)
    $textX = $curX + ($p.w - $pSize.Width) / 2
    $hg.DrawString($p.text, $fontPill, $pBrush, $textX, 236)
    $pBrush.Dispose()
    $curX += $p.w + 10
}
$pillBg.Dispose()
$pillBorder.Dispose()
$fontPill.Dispose()

# Right Side: Terminal Window (580x272 at x=636, y=44)
$termRect = [System.Drawing.RectangleF]::new(636, 44, 580, 272)
$termPath = New-RoundedPath $termRect 10
$termBgBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 19, 25, 21))
$hg.FillPath($termBgBrush, $termPath)
$termBorderPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 39, 53, 45), 1.5)
$hg.DrawPath($termBorderPen, $termPath)
$termBorderPen.Dispose()
$termBgBrush.Dispose()

# Terminal Titlebar
$tbBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 27, 36, 30))
$hg.FillRectangle($tbBrush, 637, 45, 578, 36)
$tbBrush.Dispose()
$tbLinePen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 34, 47, 39), 1)
$hg.DrawLine($tbLinePen, 636, 81, 1216, 81)
$tbLinePen.Dispose()

# Window Dots
$c1 = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 224, 90, 54))
$c2 = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 212, 163, 89))
$c3 = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 88, 120, 100))
$hg.FillEllipse($c1, 654, 58, 9, 9)
$hg.FillEllipse($c2, 670, 58, 9, 9)
$hg.FillEllipse($c3, 686, 58, 9, 9)
$c1.Dispose()
$c2.Dispose()
$c3.Dispose()

# Tab title
$fontTermTab = [System.Drawing.Font]::new("Consolas", 9)
$tabTextBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 136, 157, 144))
$hg.DrawString("businessharness / diff-review-gate", $fontTermTab, $tabTextBrush, 706, 56)
$tabTextBrush.Dispose()
$fontTermTab.Dispose()

# PASS Badge in titlebar
$passRect = [System.Drawing.RectangleF]::new(1124, 52, 80, 22)
$passPath = New-RoundedPath $passRect 4
$passBg = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 34, 49, 38))
$hg.FillPath($passBg, $passPath)
$passBg.Dispose()
$passPath.Dispose()
$fontPass = [System.Drawing.Font]::new("Consolas", 8.5, [System.Drawing.FontStyle]::Bold)
$passBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 140, 224, 160))
$hg.DrawString("PASS [clean]", $fontPass, $passBrush, 1130, 56)
$passBrush.Dispose()
$fontPass.Dispose()

# Terminal Content
$fontTerm = [System.Drawing.Font]::new("Consolas", 9.5)
$cmdGreen = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 140, 224, 160))
$cmdWhite = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 232, 238, 233))
$cmdMuted = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 94, 117, 103))
$cmdLight = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 210, 222, 212))
$cmdOrange = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 224, 90, 54))

$hg.DrawString("$", $fontTerm, $cmdGreen, 656, 96)
$hg.DrawString(" businessharness review --base origin/main", $fontTerm, $cmdWhite, 666, 96)
$hg.DrawString("> Resolving commits: 8e4ed23..HEAD (3 files, +48 -6)", $fontTerm, $cmdMuted, 656, 118)

$hg.DrawString("√", $fontTerm, $cmdGreen, 656, 142)
$hg.DrawString(" Base ref resolved in trusted local git tree", $fontTerm, $cmdLight, 672, 142)

$hg.DrawString("√", $fontTerm, $cmdGreen, 656, 164)
$hg.DrawString(" Secret pattern scan: 0 credentials leaked", $fontTerm, $cmdLight, 672, 164)

$hg.DrawString("√", $fontTerm, $cmdGreen, 656, 186)
$hg.DrawString(" Spec ref BH-1042 verified against criteria", $fontTerm, $cmdLight, 672, 186)

$hg.DrawString("√", $fontTerm, $cmdGreen, 656, 208)
$hg.DrawString(" Model review: bounded additions validated", $fontTerm, $cmdLight, 672, 208)

# Divider line
$divPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 34, 49, 38), 1)
$divPen.DashPattern = @(4.0, 4.0)
$hg.DrawLine($divPen, 656, 236, 1200, 236)
$divPen.Dispose()

# Gate result
$hg.DrawString("GATE:", $fontTerm, $cmdMuted, 656, 248)
$fontTermBold = [System.Drawing.Font]::new("Consolas", 9.5, [System.Drawing.FontStyle]::Bold)
$hg.DrawString("PASS (EXIT 0)", $fontTermBold, $cmdGreen, 706, 248)
$hg.DrawString("SARIF: harness-results/", $fontTerm, $cmdLight, 836, 248)
$hg.DrawString("MEMORY: opt-in", $fontTerm, $cmdOrange, 1070, 248)
$fontTermBold.Dispose()

$cmdGreen.Dispose()
$cmdWhite.Dispose()
$cmdMuted.Dispose()
$cmdLight.Dispose()
$cmdOrange.Dispose()
$fontTerm.Dispose()

$hdrOutPath = Join-Path $OutputDir "readme-header.png"
$hdrBmp.Save($hdrOutPath, [System.Drawing.Imaging.ImageFormat]::Png)
Write-Host "  -> Generated $hdrOutPath"

$hg.Dispose()
$hdrBmp.Dispose()
$hdrBgBrush.Dispose()
$hdrCanvasPath.Dispose()


# --- 4. GENERATE REPO COVER (1280x640) ---
# NO CHEESY TOP/BOTTOM LINES! Ultra-clean modern dev-tool social preview.
Write-Host "Rendering repo-cover.png (1280x640)..."
$covW = 1280
$covH = 640
$covBmp = [System.Drawing.Bitmap]::new($covW, $covH)
$cg = [System.Drawing.Graphics]::FromImage($covBmp)
$cg.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$cg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$cg.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit

# Cover Background Canvas: Edge-to-edge deep dark obsidian
$covBgBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
    [System.Drawing.PointF]::new(0, 0),
    [System.Drawing.PointF]::new($covW, $covH),
    [System.Drawing.Color]::FromArgb(255, 16, 21, 18),
    [System.Drawing.Color]::FromArgb(255, 11, 15, 12)
)
$cg.FillRectangle($covBgBrush, 0, 0, $covW, $covH)
$covBgBrush.Dispose()

# Warm Ambient Glow on Left
$covGlowPath = [System.Drawing.Drawing2D.GraphicsPath]::new()
$covGlowPath.AddEllipse(40, 40, 500, 500)
$cpbg = [System.Drawing.Drawing2D.PathGradientBrush]::new($covGlowPath)
$cpbg.CenterColor = [System.Drawing.Color]::FromArgb(35, 224, 90, 54)
$cpbg.SurroundColors = @([System.Drawing.Color]::FromArgb(0, 11, 15, 12))
$cg.FillRectangle($cpbg, 0, 0, $covW, $covH)
$cpbg.Dispose()
$covGlowPath.Dispose()

# Cool Mint Ambient Glow on Right
$rightGlowPath = [System.Drawing.Drawing2D.GraphicsPath]::new()
$rightGlowPath.AddEllipse(800, 150, 450, 450)
$rpbg = [System.Drawing.Drawing2D.PathGradientBrush]::new($rightGlowPath)
$rpbg.CenterColor = [System.Drawing.Color]::FromArgb(20, 140, 224, 160)
$rpbg.SurroundColors = @([System.Drawing.Color]::FromArgb(0, 11, 15, 12))
$cg.FillRectangle($rpbg, 0, 0, $covW, $covH)
$rpbg.Dispose()
$rightGlowPath.Dispose()

# Elegant Dot Matrix
$covDotBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(30, 45, 61, 50))
for ($x = 28; $x -lt $covW; $x += 28) {
    for ($y = 28; $y -lt $covH; $y += 28) {
        $cg.FillEllipse($covDotBrush, $x, $y, 2, 2)
    }
}
$covDotBrush.Dispose()

# LEFT HERO: Floating App Icon Badge (84x84 at x=80, y=96)
$badgeX = 80
$badgeY = 96
$covBadgeRect = [System.Drawing.RectangleF]::new($badgeX, $badgeY, 84, 84)
$covBadgePath = New-RoundedPath $covBadgeRect 20
$covBadgeBg = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
    [System.Drawing.PointF]::new($badgeX, $badgeY),
    [System.Drawing.PointF]::new($badgeX, $badgeY + 84),
    [System.Drawing.Color]::FromArgb(255, 38, 52, 43),
    [System.Drawing.Color]::FromArgb(255, 20, 28, 23)
)
$cg.FillPath($covBadgeBg, $covBadgePath)
$covBadgeBorder = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 55, 75, 63), 1.5)
$cg.DrawPath($covBadgeBorder, $covBadgePath)
$covBadgeBorder.Dispose()
$covBadgeBg.Dispose()
$covBadgePath.Dispose()

# Glyph in Badge
$cbPen = [System.Drawing.Pen]::new($creamColor, 6.5)
$cbPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$cbPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$cbPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round

$cbPath = [System.Drawing.Drawing2D.GraphicsPath]::new()
$cbPath.AddLine($badgeX + 24, $badgeY + 21, $badgeX + 24, $badgeY + 63)
$cbPath.AddLine($badgeX + 24, $badgeY + 63, $badgeX + 33, $badgeY + 63)
$cbPath.AddBezier($badgeX + 33, $badgeY + 63, $badgeX + 45, $badgeY + 63, $badgeX + 45, $badgeY + 38, $badgeX + 33, $badgeY + 38)
$cbPath.AddLine($badgeX + 33, $badgeY + 38, $badgeX + 24, $badgeY + 38)
$cg.DrawPath($cbPen, $cbPath)
$cg.DrawLine($cbPen, $badgeX + 45, $badgeY + 21, $badgeX + 45, $badgeY + 63)
$cbPen.Dispose()
$cbPath.Dispose()

$chPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 224, 90, 54), 6.5)
$chPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$chPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$chPath = [System.Drawing.Drawing2D.GraphicsPath]::new()
$chPath.AddBezier($badgeX + 45, $badgeY + 38, $badgeX + 45, $badgeY + 26, $badgeX + 61, $badgeY + 26, $badgeX + 61, $badgeY + 38)
$chPath.AddLine($badgeX + 61, $badgeY + 38, $badgeX + 61, $badgeY + 63)
$cg.DrawPath($chPen, $chPath)
$chPen.Dispose()
$chPath.Dispose()

# Green Beacon in Badge
$cgGreen = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 140, 224, 160))
$cg.FillEllipse($cgGreen, $badgeX + 59, $badgeY + 61, 5, 5)
$cgGreen.Dispose()

# Main Title: "BusinessHarness."
$fontCovTitle = [System.Drawing.Font]::new("Segoe UI", 44, [System.Drawing.FontStyle]::Bold)
$covTitleBrush = [System.Drawing.SolidBrush]::new($creamColor)
$cg.DrawString("BusinessHarness", $fontCovTitle, $covTitleBrush, 80, 204)
$covTitleBrush.Dispose()

$covDotBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 224, 90, 54))
$covMeasure = $cg.MeasureString("BusinessHarness", $fontCovTitle)
$cg.DrawString(".", $fontCovTitle, $covDotBrush, 80 + $covMeasure.Width - 14, 204)
$covDotBrush.Dispose()
$fontCovTitle.Dispose()

# Headline
$fontCovTag = [System.Drawing.Font]::new("Segoe UI", 18, [System.Drawing.FontStyle]::Bold)
$tagBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 232, 237, 233))
$cg.DrawString("Put every change in context.", $fontCovTag, $tagBrush, 80, 274)
$tagBrush.Dispose()
$fontCovTag.Dispose()

# Subtitle / Value Proposition
$fontCovDesc = [System.Drawing.Font]::new("Segoe UI", 12.5)
$descBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 148, 168, 156))
$cg.DrawString("Bounded AI diff review, reference validation,", $fontCovDesc, $descBrush, 80, 314)
$cg.DrawString("and review memory for your CI pipeline.", $fontCovDesc, $descBrush, 80, 338)
$descBrush.Dispose()
$fontCovDesc.Dispose()

# Tech Badges
$fontCovPills = [System.Drawing.Font]::new("Consolas", 9.5, [System.Drawing.FontStyle]::Bold)
$covPills = @(
    @{ text = ".NET 10 CLI"; color = [System.Drawing.Color]::FromArgb(255, 140, 224, 160); w = 98 },
    @{ text = "OpenAI + Claude"; color = [System.Drawing.Color]::FromArgb(255, 224, 90, 54); w = 144 },
    @{ text = "Jira & JSON APIs"; color = [System.Drawing.Color]::FromArgb(255, 157, 179, 164); w = 140 }
)
$cCurX = 80
$pillBoxBg = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 24, 34, 28))
$pillBoxBorder = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 47, 66, 54), 1.2)

foreach ($cp in $covPills) {
    $cpRect = [System.Drawing.RectangleF]::new($cCurX, 390, $cp.w, 30)
    $cpPath = New-RoundedPath $cpRect 6
    $cg.FillPath($pillBoxBg, $cpPath)
    $cg.DrawPath($pillBoxBorder, $cpPath)
    $cpPath.Dispose()

    $cpBrush = [System.Drawing.SolidBrush]::new($cp.color)
    $cpSz = $cg.MeasureString($cp.text, $fontCovPills)
    $cg.DrawString($cp.text, $fontCovPills, $cpBrush, $cCurX + ($cp.w - $cpSz.Width) / 2, 396)
    $cpBrush.Dispose()
    $cCurX += $cp.w + 12
}
$pillBoxBg.Dispose()
$pillBoxBorder.Dispose()
$fontCovPills.Dispose()

# Platform Support Line
$fontPlat = [System.Drawing.Font]::new("Consolas", 9.5, [System.Drawing.FontStyle]::Bold)
$platBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 88, 110, 97))
$cg.DrawString("GitHub Actions  •  GitLab CI  •  Azure DevOps  •  Bitbucket", $fontPlat, $platBrush, 80, 480)
$platBrush.Dispose()
$fontPlat.Dispose()


# RIGHT SIDE: Minimal Elevated Terminal Window (584x428 at x=616, y=96)
$covTermRect = [System.Drawing.RectangleF]::new(616, 96, 584, 428)
$covTermPath = New-RoundedPath $covTermRect 12
$covTermBg = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 19, 25, 21))
$cg.FillPath($covTermBg, $covTermPath)
$covTermBorder = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 39, 53, 45), 1.5)
$cg.DrawPath($covTermBorder, $covTermPath)
$covTermBorder.Dispose()
$covTermBg.Dispose()
$covTermPath.Dispose()

# Terminal Titlebar
$covTbBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 27, 36, 30))
$cg.FillRectangle($covTbBrush, 617, 97, 582, 42)
$covTbBrush.Dispose()
$covTbLine = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 34, 47, 39), 1)
$cg.DrawLine($covTbLine, 616, 139, 1200, 139)
$covTbLine.Dispose()

# Dots
$d1 = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 224, 90, 54))
$d2 = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 212, 163, 89))
$d3 = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 88, 120, 100))
$cg.FillEllipse($d1, 640, 112, 10, 10)
$cg.FillEllipse($d2, 658, 112, 10, 10)
$cg.FillEllipse($d3, 676, 112, 10, 10)
$d1.Dispose()
$d2.Dispose()
$d3.Dispose()

# Tab title
$fontTab = [System.Drawing.Font]::new("Consolas", 9.5)
$tabBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 136, 157, 144))
$cg.DrawString("businessharness / diff-review-gate", $fontTab, $tabBrush, 698, 110)
$tabBrush.Dispose()
$fontTab.Dispose()

# PASS Badge
$cpBadgeRect = [System.Drawing.RectangleF]::new(1100, 107, 84, 22)
$cpBadgePath = New-RoundedPath $cpBadgeRect 4
$cpBadgeBg = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 34, 49, 38))
$cg.FillPath($cpBadgeBg, $cpBadgePath)
$cpBadgeBg.Dispose()
$cpBadgePath.Dispose()
$fontCpPass = [System.Drawing.Font]::new("Consolas", 8.5, [System.Drawing.FontStyle]::Bold)
$cpPassBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 140, 224, 160))
$cg.DrawString("PASS [clean]", $fontCpPass, $cpPassBrush, 1106, 111)
$cpPassBrush.Dispose()
$fontCpPass.Dispose()

# Terminal Execution Body
$fontCTerm = [System.Drawing.Font]::new("Consolas", 10.5)
$tcGreen = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 140, 224, 160))
$tcWhite = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 232, 238, 233))
$tcMuted = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 94, 117, 103))
$tcLight = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 210, 222, 212))
$tcOrange = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 224, 90, 54))

$cg.DrawString("$", $fontCTerm, $tcGreen, 644, 164)
$cg.DrawString(" businessharness review --base origin/main", $fontCTerm, $tcWhite, 656, 164)
$cg.DrawString("> Resolving commits: 8e4ed23..HEAD (3 files changed)", $fontCTerm, $tcMuted, 644, 192)

$cg.DrawString("√", $fontCTerm, $tcGreen, 644, 226)
$cg.DrawString(" Git base ref resolved in trusted local tree", $fontCTerm, $tcLight, 664, 226)

$cg.DrawString("√", $fontCTerm, $tcGreen, 644, 254)
$cg.DrawString(" Credential pattern scan: 0 secrets leaked", $fontCTerm, $tcLight, 664, 254)

$cg.DrawString("√", $fontCTerm, $tcGreen, 644, 282)
$cg.DrawString(" Ticket BH-1042 verified against API criteria", $fontCTerm, $tcLight, 664, 282)

$cg.DrawString("√", $fontCTerm, $tcGreen, 644, 310)
$cg.DrawString(" Model review: bounded additions validated", $fontCTerm, $tcLight, 664, 310)

# Divider
$covDivPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 34, 49, 38), 1)
$covDivPen.DashPattern = @(4.0, 4.0)
$cg.DrawLine($covDivPen, 644, 350, 1170, 350)
$covDivPen.Dispose()

# Gate result
$fontCTermBold = [System.Drawing.Font]::new("Consolas", 10.5, [System.Drawing.FontStyle]::Bold)
$cg.DrawString("GATE ASSESSMENT:", $fontCTerm, $tcMuted, 644, 372)
$cg.DrawString("PASS (EXIT 0)", $fontCTermBold, $tcGreen, 794, 372)

$cg.DrawString("SARIF ARTIFACTS:", $fontCTerm, $tcMuted, 644, 404)
$cg.DrawString("harness-results/report.sarif", $fontCTerm, $tcLight, 794, 404)

$cg.DrawString("REVIEW MEMORY:", $fontCTerm, $tcMuted, 644, 436)
$cg.DrawString("Scoped session cached", $fontCTerm, $tcOrange, 794, 436)
$fontCTermBold.Dispose()

$tcGreen.Dispose()
$tcWhite.Dispose()
$tcMuted.Dispose()
$tcLight.Dispose()
$tcOrange.Dispose()
$fontCTerm.Dispose()

$covOutPath = Join-Path $OutputDir "repo-cover.png"
$covBmp.Save($covOutPath, [System.Drawing.Imaging.ImageFormat]::Png)
Write-Host "  -> Generated $covOutPath"

$cg.Dispose()
$covBmp.Dispose()

Write-Host "All assets generated successfully with premium modern aesthetic!" -ForegroundColor Green
