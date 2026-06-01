#Requires -RunAsAdministrator
<#
.SYNOPSIS
  Cierra procesos colgados, desinstala el paquete y reinstala (util si el widget dejo de abrir).
#>
param(
    [switch]$SkipReinstall
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$installScript = Join-Path $repoRoot 'scripts\Install-GameBarWidget.ps1'

Write-Host '=== Reset Gemini Assistant ===' -ForegroundColor Cyan

Get-Process -Name 'GeminiAssistant' -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Host "Stopping PID $($_.Id)" -ForegroundColor Yellow
    Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
}

Get-AppxPackage -ErrorAction SilentlyContinue | Where-Object {
    $_.Name -eq 'GeminiAssistant' -or $_.DisplayName -eq 'Gemini Assistant'
} | ForEach-Object {
    Write-Host "Removing package $($_.PackageFullName)" -ForegroundColor Yellow
    Remove-AppxPackage -Package $_.PackageFullName -ErrorAction SilentlyContinue
}

Start-Sleep -Seconds 2

if (-not $SkipReinstall) {
    & $installScript
}

Write-Host ''
Write-Host 'Done. Win+G -> Biblioteca de widgets -> fija Gemini Assistant de nuevo.' -ForegroundColor Green
