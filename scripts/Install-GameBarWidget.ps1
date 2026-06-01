#Requires -RunAsAdministrator
<#
.SYNOPSIS
  Compila con MSBuild (Visual Studio) e instala Gemini Assistant para Xbox Game Bar.
#>
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [ValidateSet('x64', 'x86', 'ARM64')]
    [string]$Platform = 'x64',
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot 'src\GeminiAssistant\GeminiAssistant.csproj'

function Get-MsBuildPath {
    $vswhere = @(
        "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\Installer\vswhere.exe"
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1

    if ($vswhere) {
        $msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
        if ($msbuild) { return $msbuild }
    }

    @(
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\18\Professional\MSBuild\Current\Bin\MSBuild.exe"
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1
}

function Find-ManifestPath {
    $projectDir = Split-Path -Parent $projectPath
    $layoutCandidates = @(
        (Join-Path $projectDir "bin\$Platform\$Configuration\AppX")
        (Join-Path $projectDir "bin\$Platform\$Configuration\net10.0-windows10.0.26100.0\AppX")
        (Join-Path $projectDir "AppPackages\GeminiAssistant_1.0.1.0_${Platform}_${Configuration}_Test")
        (Join-Path $projectDir "AppPackages\GeminiAssistant_1.0.0.0_${Platform}_${Configuration}_Test")
    )
    foreach ($dir in $layoutCandidates) {
        if (-not (Test-Path $dir)) { continue }
        $manifest = Join-Path $dir 'AppxManifest.xml'
        if (Test-Path $manifest) { return $manifest }
        $coreManifest = Join-Path $dir 'Core\AppxManifest.xml'
        if (Test-Path $coreManifest) { return $coreManifest }
    }
    return $null
}

function Remove-OldPackages {
    Get-AppxPackage -ErrorAction SilentlyContinue | Where-Object {
        $_.Name -eq 'GeminiAssistant' -or
        $_.DisplayName -eq 'Gemini Assistant' -or
        $_.PackageFamilyName -like 'GeminiAssistant_*'
    } | ForEach-Object {
        Write-Host "Removing: $($_.PackageFullName)" -ForegroundColor Yellow
        Remove-AppxPackage -Package $_.PackageFullName -ErrorAction SilentlyContinue
    }
}

Write-Host '=== Install Gemini Assistant (Game Bar) ===' -ForegroundColor Cyan

if (-not $SkipBuild) {
    $msbuild = Get-MsBuildPath
    if (-not $msbuild) {
        Write-Host 'MSBuild (Visual Studio) not found.' -ForegroundColor Red
        Write-Host 'Install: Universal Windows Platform tools + Windows 11 SDK, then retry.'
        exit 1
    }

    Write-Host "Building with: $msbuild" -ForegroundColor Gray
    # Rebuild limpia evita .g.i.cs duplicados de migraciones anteriores.
    & $msbuild $projectPath `
        /restore `
        /t:Rebuild `
        /p:Configuration=$Configuration `
        /p:Platform=$Platform `
        /p:DeployOnBuild=false `
        /p:GenerateAppxPackageOnBuild=true `
        /v:m

    if ($LASTEXITCODE -ne 0) {
        Write-Host ''
        Write-Host 'MSBuild failed. Do NOT use dotnet build alone for UWP packaging.' -ForegroundColor Red
        Write-Host 'Fix compile errors in Visual Studio (Build -> Rebuild), then run this script again.'
        exit $LASTEXITCODE
    }
}

$manifestPath = Find-ManifestPath
if (-not $manifestPath) {
    Write-Host 'Appx layout not found after build.' -ForegroundColor Red
    Write-Host "Expected under bin\$Platform\$Configuration\AppX"
    exit 1
}

$layoutDir = Split-Path -Parent $manifestPath
if ($layoutDir.EndsWith('\Core')) {
    $layoutDir = Split-Path -Parent $layoutDir
}

$registerManifest = Join-Path $layoutDir 'AppxManifest.xml'
if (-not (Test-Path $registerManifest)) {
    $registerManifest = $manifestPath
}

Write-Host "Registering: $registerManifest" -ForegroundColor Green
Remove-OldPackages
Add-AppxPackage -Register $registerManifest -ForceUpdateFromAnyVersion -ForceApplicationShutdown

$installed = Get-AppxPackage -Name 'GeminiAssistant' -ErrorAction SilentlyContinue
if (-not $installed) {
    $installed = Get-AppxPackage -ErrorAction SilentlyContinue | Where-Object {
        $_.DisplayName -eq 'Gemini Assistant'
    } | Select-Object -First 1
}

if ($installed) {
    Write-Host ''
    Write-Host 'INSTALLED OK' -ForegroundColor Green
    Write-Host "  PFN: $($installed.PackageFamilyName)"
    Write-Host '  Win+G -> Widget library -> Gemini Assistant -> Pin'
}
else {
    Write-Host 'Package registration finished but GeminiAssistant is not listed.' -ForegroundColor Yellow
    exit 1
}
