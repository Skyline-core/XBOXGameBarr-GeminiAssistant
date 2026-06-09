#Requires -Version 5.1
<#
.SYNOPSIS
  Genera el .msixupload para Microsoft Store (Release | x64).
#>
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [ValidateSet('x64', 'x86', 'ARM64')]
    [string]$Platform = 'x64'
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
        "${env:ProgramFiles}\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1
}

$msbuild = Get-MsBuildPath
if (-not $msbuild) {
    throw 'No se encontro MSBuild. Instala Visual Studio con herramientas UWP y Windows SDK.'
}

Write-Host "Compilando paquete Store ($Configuration | $Platform)..." -ForegroundColor Cyan
& $msbuild $projectPath `
    /restore `
    /t:Rebuild `
    /p:Configuration=$Configuration `
    /p:Platform=$Platform `
    /p:GenerateAppxPackageOnBuild=true `
    /p:UapAppxPackageBuildMode=StoreUpload `
    /v:m

if ($LASTEXITCODE -ne 0) {
    throw 'MSBuild fallo. Revisa la lista de errores en Visual Studio o en la salida anterior.'
}

$appPackages = Join-Path (Split-Path -Parent $projectPath) 'AppPackages'
$upload = Get-ChildItem $appPackages -Filter '*.msixupload' -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if (-not $upload) {
    throw "No se genero ningun .msixupload en $appPackages"
}

Write-Host ""
Write-Host "Paquete listo para Partner Center:" -ForegroundColor Green
Write-Host "  $($upload.FullName)"
Write-Host ""
Write-Host "Subelo en Partner Center -> Gemini Assistant -> Paquetes."
