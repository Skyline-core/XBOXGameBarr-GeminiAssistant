#Requires -Version 5.1
<#
.SYNOPSIS
  Certificado de firma para Release/Store (debe coincidir con Package.appxmanifest Publisher).

.USAGE
  .\scripts\Configure-StoreSigning.ps1
  .\scripts\Configure-StoreSigning.ps1 -CreateIfMissing
  .\scripts\Configure-StoreSigning.ps1 -ExportPfx -PfxPassword "TuContraseñaSegura"
#>
param(
    [switch]$CreateIfMissing,
    [switch]$ExportPfx,
    [string]$PfxPassword = ""
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$projectDir = Join-Path $repoRoot 'src\GeminiAssistant'
$csprojUser = Join-Path $projectDir 'GeminiAssistant.csproj.user'
$publisherCn = 'CN=F501727E-B88A-44F3-99F9-D0461D2E1C36'
$publisherGuid = 'F501727E-B88A-44F3-99F9-D0461D2E1C36'

function Get-StoreCert {
    Get-ChildItem Cert:\CurrentUser\My |
        Where-Object { $_.HasPrivateKey -and $_.Subject -like "*$publisherGuid*" } |
        Sort-Object NotAfter -Descending |
        Select-Object -First 1
}

function Get-DevCert {
    Get-ChildItem Cert:\CurrentUser\My |
        Where-Object {
            $_.HasPrivateKey -and
            ($_.Subject -eq 'CN=Developer' -or $_.FriendlyName -like 'Gemini Assistant*')
        } |
        Sort-Object NotAfter -Descending |
        Select-Object -First 1
}

Write-Host "=== Diagnostico de certificados ===" -ForegroundColor Cyan
Write-Host "Manifiesto exige Publisher: $publisherCn"
Write-Host ""

$devCert = Get-DevCert
if ($devCert) {
    Write-Host "Certificado de DESARROLLO (no sirve para Store):" -ForegroundColor Yellow
    Write-Host "  Subject:    $($devCert.Subject)"
    Write-Host "  Thumbprint: $($devCert.Thumbprint)"
    Write-Host ""
}

$cert = Get-StoreCert
if (-not $cert) {
    Write-Host "Certificado de STORE: NO encontrado en certmgr (Personal)." -ForegroundColor Red
    Write-Host "Por eso Visual muestra el Publisher de la Store pero certmgr solo tiene 'Developer'."
    Write-Host ""

    if (-not $CreateIfMissing) {
        Write-Host "Opciones:" -ForegroundColor Yellow
        Write-Host "  A) Ejecuta: .\scripts\Configure-StoreSigning.ps1 -CreateIfMissing"
        Write-Host "  B) Visual Studio -> Publicar -> Asociar la aplicacion con la Store (otra vez)"
        exit 1
    }

    Write-Host "Creando certificado con Publisher de la Store..." -ForegroundColor Cyan
    $cert = New-SelfSignedCertificate `
        -Subject $publisherCn `
        -Type Custom `
        -KeyUsage DigitalSignature `
        -FriendlyName 'Gemini Assistant (Store ITSOFTSYS)' `
        -CertStoreLocation 'Cert:\CurrentUser\My' `
        -TextExtension @(
            '2.5.29.37={text}1.3.6.1.5.5.7.3.3'
            '2.5.29.19={text}'
        )

    Write-Host "Certificado creado." -ForegroundColor Green
}

Write-Host "Certificado correcto para empaquetar:" -ForegroundColor Green
Write-Host "  Subject:     $($cert.Subject)"
Write-Host "  Thumbprint:  $($cert.Thumbprint)"
Write-Host "  Valido hasta: $($cert.NotAfter)"

if ($ExportPfx) {
    if ([string]::IsNullOrWhiteSpace($PfxPassword)) {
        throw "Usa -PfxPassword 'tu contraseña' al exportar el PFX."
    }

    $pfxPath = Join-Path $projectDir 'GeminiAssistant_StoreKey.pfx'
    $secure = ConvertTo-SecureString -String $PfxPassword -Force -AsPlainText
    Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password $secure | Out-Null
    Write-Host ""
    Write-Host "Exportado: $pfxPath" -ForegroundColor Green
    Write-Host "Contraseña del PFX = la que pusiste en -PfxPassword"
}

$thumbprint = $cert.Thumbprint
$userXml = @"
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="Current" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup Condition="'`$(Configuration)' == 'Release'">
    <PackageCertificateThumbprint>$thumbprint</PackageCertificateThumbprint>
    <AppxPackageSigningEnabled>true</AppxPackageSigningEnabled>
  </PropertyGroup>
</Project>
"@
Set-Content -Path $csprojUser -Value $userXml -Encoding UTF8
Write-Host ""
Write-Host "Actualizado: GeminiAssistant.csproj.user (thumbprint Release)" -ForegroundColor Green

Write-Host ""
Write-Host "=== En Visual Studio ===" -ForegroundColor Yellow
Write-Host "1. Package.appxmanifest -> Empaquetado -> Elegir certificado..."
Write-Host "2. Selecciona el que dice: $($cert.Subject)"
Write-Host "   (NO el de CN=Developer)"
Write-Host "3. Release | x64 -> Limpiar -> Compilar -> Crear paquetes (Microsoft Store)"
