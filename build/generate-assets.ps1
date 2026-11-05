Add-Type -AssemblyName System.Drawing

$outputDir = Join-Path $PSScriptRoot "..\src\HabitGrid.Package\Images"
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
}

function Create-GridIcon {
    param(
        [int]$Width,
        [int]$Height,
        [string]$FileName
    )

    $bmp = New-Object System.Drawing.Bitmap($Width, $Height)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::FromArgb(14, 16, 20))

    $brushInactive = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(35, 40, 52))
    $brushAccent = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(110, 231, 183))

    $minDim = [Math]::Min($Width, $Height)
    $padding = [int]($minDim * 0.15)
    $gridSize = $minDim - ($padding * 2)
    $gap = [Math]::Max(2, [int]($gridSize * 0.08))
    $cellSize = ($gridSize - ($gap * 2)) / 3

    $startX = [int](($Width - $gridSize) / 2)
    $startY = [int](($Height - $gridSize) / 2)

    for ($r = 0; $r -lt 3; $r++) {
        for ($c = 0; $c -lt 3; $c++) {
            $x = $startX + ($c * ($cellSize + $gap))
            $y = $startY + ($r * ($cellSize + $gap))
            $brush = if (($r -eq 1 -and $c -eq 1) -or ($r -eq 0 -and $c -eq 2) -or ($r -eq 2 -and $c -eq 0)) {
                $brushAccent
            } else {
                $brushInactive
            }
            $g.FillRectangle($brush, [float]$x, [float]$y, [float]$cellSize, [float]$cellSize)
        }
    }

    $g.Dispose()
    $dest = Join-Path $outputDir $FileName
    $bmp.Save($dest, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "Created: $FileName ($Width x $Height)"
}

Create-GridIcon -Width 50 -Height 50 -FileName "StoreLogo.png"
Create-GridIcon -Width 44 -Height 44 -FileName "Square44x44Logo.png"
Create-GridIcon -Width 150 -Height 150 -FileName "Square150x150Logo.png"
Create-GridIcon -Width 310 -Height 150 -FileName "Wide310x150Logo.png"
Create-GridIcon -Width 620 -Height 300 -FileName "SplashScreen.png"
