#Requires -Version 5.1
<#
.SYNOPSIS
  Crea GeminiAssistant_TemporaryKey.pfx para firmar el MSIX de depuración (Publisher CN=Developer).
#>
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$pfxPath = Join-Path $repoRoot 'src\GeminiAssistant\GeminiAssistant_TemporaryKey.pfx'
$password = 'GeminiAssistant'

if (Test-Path $pfxPath) {
    Write-Host "Certificate already exists: $pfxPath" -ForegroundColor Green
    exit 0
}

$cert = New-SelfSignedCertificate `
    -Subject 'CN=Developer' `
    -Type Custom `
    -KeyUsage DigitalSignature `
    -FriendlyName 'Gemini Assistant (dev)' `
    -CertStoreLocation 'Cert:\CurrentUser\My' `
    -TextExtension @(
        '2.5.29.37={text}1.3.6.1.5.5.7.3.3'
        '2.5.29.19={text}'
    )

$secure = ConvertTo-SecureString -String $password -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password $secure | Out-Null

Write-Host "Created: $pfxPath" -ForegroundColor Green
Write-Host 'Rebuild the solution in Visual Studio (x64, Debug).'
