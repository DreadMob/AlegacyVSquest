# Generate trial boss texture overlays (64x64 semi-transparent color tints)
# Run this script once to create the PNG overlay files

Add-Type -AssemblyName System.Drawing

$outDir = "$PSScriptRoot\assets\albase\textures\entity\overlay"
if (!(Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }

$overlays = @{
    "void-purple"    = @{ R=120; G=40; B=200; A=80 }
    "void-ice"       = @{ R=100; G=180; B=255; A=70 }
    "void-blood"     = @{ R=200; G=30; B=30; A=75 }
    "void-ash"       = @{ R=80; G=70; B=60; A=60 }
    "void-bone"      = @{ R=220; G=210; B=180; A=50 }
    "void-deep"      = @{ R=60; G=20; B=160; A=90 }
    "void-sovereign" = @{ R=180; G=50; B=255; A=100 }
}

foreach ($name in $overlays.Keys) {
    $color = $overlays[$name]
    $bmp = New-Object System.Drawing.Bitmap(64, 64)
    
    $fillColor = [System.Drawing.Color]::FromArgb($color.A, $color.R, $color.G, $color.B)
    
    for ($x = 0; $x -lt 64; $x++) {
        for ($y = 0; $y -lt 64; $y++) {
            $bmp.SetPixel($x, $y, $fillColor)
        }
    }
    
    $path = Join-Path $outDir "$name.png"
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    
    Write-Host "Created: $path"
}

Write-Host "Done! $($overlays.Count) overlays generated."
