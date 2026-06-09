#Requires -Version 5.1
<#
.SYNOPSIS
  Genera los PNG de Assets\ para el manifiesto UWP (quita fondo blanco externo).

.USAGE
  .\scripts\Generate-UwpAssets.ps1
  .\scripts\Generate-UwpAssets.ps1 -SourceImage "C:\ruta\mi-logo.png"
#>
param(
    [string]$SourceImage = ""
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$assetsDir = Join-Path $repoRoot 'src\GeminiAssistant\Assets'
$pythonScript = Join-Path $PSScriptRoot 'generate_uwp_assets.py'

if ([string]::IsNullOrWhiteSpace($SourceImage)) {
    $candidates = @(
        (Join-Path $repoRoot 'logo.png')
        (Join-Path $repoRoot 'src\GeminiAssistant\logo.png')
        (Join-Path $repoRoot 'assets\GeminiLogo-source.png')
        (Join-Path $assetsDir 'GeminiLogo-source.png')
    )
    foreach ($path in $candidates) {
        if (Test-Path $path) {
            $SourceImage = $path
            break
        }
    }
}

if ([string]::IsNullOrWhiteSpace($SourceImage) -or -not (Test-Path $SourceImage)) {
    throw "Indica -SourceImage con el PNG del logo."
}

$python = @(
    "$env:LOCALAPPDATA\Python\pythoncore-3.14-64\python.exe"
    "$env:LOCALAPPDATA\Programs\Python\Python313\python.exe"
    "$env:LOCALAPPDATA\Programs\Python\Python312\python.exe"
    'python'
) | Where-Object { $_ -eq 'python' -or (Test-Path $_) } | Select-Object -First 1

if ($python -ne 'python') {
    & $python -c "from PIL import Image" 2>$null
    if ($LASTEXITCODE -ne 0) {
        & $python -m pip install Pillow --quiet
    }
}
else {
    python -c "from PIL import Image" 2>$null
    if ($LASTEXITCODE -ne 0) {
        python -m pip install Pillow --quiet
    }
}

& $python $pythonScript --source $SourceImage --output-dir $assetsDir
if ($LASTEXITCODE -ne 0) {
    throw 'Fallo la generacion de assets.'
}

Write-Host ""
Write-Host "Assets generados en: $assetsDir" -ForegroundColor Green
Write-Host "  StoreLogo.png, Square44x44Logo.png, Square150x150Logo.png"
Write-Host "  Wide310x150Logo.png, SplashScreen.png, GeminiLogo.png"
Write-Host ""
Write-Host "Opcional (iconos Game Bar):" -ForegroundColor Yellow
Write-Host "  .\scripts\Generate-GameBarIcons.ps1"
