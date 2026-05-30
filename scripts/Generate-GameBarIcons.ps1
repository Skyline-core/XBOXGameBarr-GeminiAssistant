# Generates Game Bar widget icons under GameBar\<Widget>\Icons\
param(
    [string]$ProjectDir = (Join-Path $PSScriptRoot "..\src\GeminiAssistant")
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$sizes = @(16, 20, 24, 32, 44, 256)
$source = Join-Path $ProjectDir "Assets\Square150x150Logo.png"
if (-not (Test-Path $source)) {
    $source = Join-Path $ProjectDir "Assets\Square44x44Logo.png"
}
if (-not (Test-Path $source)) {
    throw "No logo PNG found in Assets. Build placeholder assets first."
}

function Save-IconSet {
    param([string]$TargetIconsDir)
    New-Item -ItemType Directory -Force -Path $TargetIconsDir | Out-Null
    $srcBmp = [System.Drawing.Image]::FromFile((Resolve-Path $source))
    try {
        foreach ($size in $sizes) {
            $dest = Join-Path $TargetIconsDir ("icon.targetsize-{0}.png" -f $size)
            $bmp = New-Object System.Drawing.Bitmap($size, $size)
            $g = [System.Drawing.Graphics]::FromImage($bmp)
            $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $g.Clear([System.Drawing.Color]::Transparent)
            $g.DrawImage($srcBmp, 0, 0, $size, $size)
            $bmp.Save($dest, [System.Drawing.Imaging.ImageFormat]::Png)
            $g.Dispose()
            $bmp.Dispose()
        }
    }
    finally {
        $srcBmp.Dispose()
    }
}

Save-IconSet (Join-Path $ProjectDir "GameBar\Chat\Icons")
Save-IconSet (Join-Path $ProjectDir "GameBar\Settings\Icons")
Write-Host "Game Bar icons generated for Chat and Settings."
