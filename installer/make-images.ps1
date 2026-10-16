# Draws the MSI wizard's artwork from the app's icon mark (re-run after the
# icon changes; the BMPs are committed):
#   banner.bmp  493 x 58  - top strip of the inner pages; the wizard writes its
#                           black title text on the left, so that stays white.
#   dialog.bmp  493 x 312 - welcome/finish pages; the wizard writes on the right
#                           of a 164 px left panel, which carries the branding.
Add-Type -AssemblyName System.Drawing
$here = $PSScriptRoot
$mark = [System.Drawing.Image]::FromFile((Join-Path $here '../src/ArtifactX.WinUI3/Assets/AppIconMark.png'))
$dark = [System.Drawing.ColorTranslator]::FromHtml('#0C1024')
$gold = [System.Drawing.ColorTranslator]::FromHtml('#FFC62E')

function New-Canvas($w, $h) {
    $bmp = New-Object System.Drawing.Bitmap $w, $h, ([System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.InterpolationMode = 'HighQualityBicubic'; $g.TextRenderingHint = 'AntiAliasGridFit'
    $g.Clear([System.Drawing.Color]::White)
    return $bmp, $g
}

# Banner: white, with a dark square holding the mark at the right.
$bmp, $g = New-Canvas 493 58
$g.FillRectangle((New-Object System.Drawing.SolidBrush $dark), 435, 0, 58, 58)
$g.DrawImage($mark, 441, 5, 48, 48)
$bmp.Save((Join-Path $here 'banner.bmp'), [System.Drawing.Imaging.ImageFormat]::Bmp)
$g.Dispose(); $bmp.Dispose()

# Dialog: dark left panel with the mark and the name, white elsewhere.
$bmp, $g = New-Canvas 493 312
$g.FillRectangle((New-Object System.Drawing.SolidBrush $dark), 0, 0, 164, 312)
$g.FillRectangle((New-Object System.Drawing.SolidBrush $gold), 160, 0, 4, 312)
$g.DrawImage($mark, 26, 70, 108, 108)
$font = New-Object System.Drawing.Font 'Segoe UI Semibold', 15
$fmt = New-Object System.Drawing.StringFormat; $fmt.Alignment = 'Center'
$g.DrawString('ARTIFACTX', $font, (New-Object System.Drawing.SolidBrush $gold), (New-Object System.Drawing.RectangleF 0, 196, 160, 30), $fmt)
$small = New-Object System.Drawing.Font 'Segoe UI', 8.5
$g.DrawString("for No Man's Sky", $small, (New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(170, 180, 200))), (New-Object System.Drawing.RectangleF 0, 226, 160, 20), $fmt)
$bmp.Save((Join-Path $here 'dialog.bmp'), [System.Drawing.Imaging.ImageFormat]::Bmp)
$g.Dispose(); $bmp.Dispose(); $mark.Dispose()
Write-Host 'Wrote banner.bmp and dialog.bmp'
