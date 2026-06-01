# Limpia salidas UWP (evita errores CS0102 por .g.i.cs duplicados tras migrar el proyecto).
$projectDir = Join-Path (Split-Path -Parent $PSScriptRoot) 'src\GeminiAssistant'
if (Test-Path $projectDir) {
    @('bin', 'obj', 'AppPackages') | ForEach-Object {
        $path = Join-Path $projectDir $_
        if (Test-Path $path) {
            Write-Host "Removing $path"
            Remove-Item -LiteralPath $path -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}
Write-Host 'Done. Next: run Install-GameBarWidget.ps1 as Administrator (uses MSBuild from Visual Studio).'
