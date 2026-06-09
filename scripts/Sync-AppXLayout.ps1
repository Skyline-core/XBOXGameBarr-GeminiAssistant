#Requires -Version 5.1
param(
    [Parameter(Mandatory = $true)]
    [string]$RecipePath,
    [Parameter(Mandatory = $true)]
    [string]$LayoutDir,
    [int]$MaxRetries = 8,
    [int]$RetryDelayMs = 250
)

$ErrorActionPreference = 'Stop'

function Get-FileSha256 {
    param([string]$Path)
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

function Test-SameFileContent {
    param(
        [string]$Source,
        [string]$Destination
    )

    if (-not (Test-Path -LiteralPath $Source) -or -not (Test-Path -LiteralPath $Destination)) {
        return $false
    }

    try {
        return (Get-FileSha256 -Path $Source) -eq (Get-FileSha256 -Path $Destination)
    }
    catch {
        return $false
    }
}

function Copy-PackageFile {
    param(
        [string]$Source,
        [string]$Destination
    )

    if (Test-SameFileContent -Source $Source -Destination $Destination) {
        return
    }

    $destParent = Split-Path -Parent $Destination
    if ($destParent -and -not (Test-Path $destParent)) {
        New-Item -ItemType Directory -Path $destParent -Force | Out-Null
    }

    for ($attempt = 1; $attempt -le $MaxRetries; $attempt++) {
        try {
            Copy-Item -LiteralPath $Source -Destination $Destination -Force
            return
        }
        catch [System.IO.IOException] {
            if (Test-SameFileContent -Source $Source -Destination $Destination) {
                return
            }

            if ($attempt -ge $MaxRetries) {
                throw "No se pudo copiar '$Source' -> '$Destination'. " +
                      "Cierra el widget de Game Bar / Gemini Assistant y vuelve a compilar. " +
                      "Detalle: $($_.Exception.Message)"
            }

            Start-Sleep -Milliseconds ($RetryDelayMs * $attempt)
        }
    }
}

if (-not (Test-Path $RecipePath)) {
    throw "No existe appxrecipe: $RecipePath"
}

[xml]$recipe = Get-Content -LiteralPath $RecipePath -Encoding UTF8
if (-not (Test-Path $LayoutDir)) {
    New-Item -ItemType Directory -Path $LayoutDir -Force | Out-Null
}

foreach ($group in $recipe.Project.ItemGroup) {
    foreach ($node in $group.ChildNodes) {
        if ($node.LocalName -eq 'AppXManifest') {
            $dest = Join-Path $LayoutDir 'AppxManifest.xml'
            Copy-PackageFile -Source $node.Include -Destination $dest
            continue
        }

        if ($node.LocalName -ne 'AppxPackagedFile') {
            continue
        }

        $source = $node.Include
        $packagePath = $node.PackagePath
        if ([string]::IsNullOrWhiteSpace($source) -or [string]::IsNullOrWhiteSpace($packagePath)) {
            continue
        }
        if (-not (Test-Path -LiteralPath $source)) {
            Write-Warning "Falta archivo del paquete: $source"
            continue
        }

        $dest = Join-Path $LayoutDir $packagePath
        Copy-PackageFile -Source $source -Destination $dest
    }
}

if (-not (Test-Path (Join-Path $LayoutDir 'AppxManifest.xml'))) {
    throw "No se pudo crear AppX layout en $LayoutDir"
}
