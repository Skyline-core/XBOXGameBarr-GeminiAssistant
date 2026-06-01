# Verifies Gemini Assistant is installed and shows how Game Bar identifies the package.
$packageName = "GeminiAssistant"

Write-Host "=== Gemini Assistant / Game Bar ===" -ForegroundColor Cyan

$pkg = Get-AppxPackage -Name $packageName -ErrorAction SilentlyContinue
if (-not $pkg) {
    $pkg = Get-AppxPackage -ErrorAction SilentlyContinue | Where-Object {
        $_.DisplayName -eq 'Gemini Assistant' -or $_.PackageFamilyName -like 'GeminiAssistant_*'
    } | Select-Object -First 1
}
if (-not $pkg) {
    Write-Host "NOT INSTALLED: no AppX package named '$packageName'." -ForegroundColor Red
    Write-Host ""
    Write-Host "Fix (Administrator PowerShell):" -ForegroundColor Yellow
    Write-Host "  1. scripts\Clean-UwpBuild.ps1"
    Write-Host "  2. Rebuild in VS (x64, Debug)"
    Write-Host "  3. scripts\Install-GameBarWidget.ps1"
    exit 1
}

Write-Host "Installed:" -ForegroundColor Green
Write-Host "  Name:        $($pkg.Name)"
Write-Host "  Version:     $($pkg.Version)"
Write-Host "  PFN:         $($pkg.PackageFamilyName)"
Write-Host "  InstallPath: $($pkg.InstallLocation)"
Write-Host ""

$manifestPath = Join-Path $pkg.InstallLocation "AppxManifest.xml"
if (Test-Path $manifestPath) {
    [xml]$manifest = Get-Content $manifestPath
    $ns = @{ uap3 = "http://schemas.microsoft.com/appx/manifest/uap/windows10/3" }
    $extensions = Select-Xml -Xml $manifest -XPath "//uap3:AppExtension[@Name='microsoft.gameBarUIExtension']" -Namespace $ns
    if ($extensions.Count -gt 0) {
        Write-Host "Game Bar widgets registered:" -ForegroundColor Green
        foreach ($node in $extensions) {
            $id = $node.Node.GetAttribute("Id")
            $display = $node.Node.GetAttribute("DisplayName")
            Write-Host "  - $display (Id=$id)"
        }
    }
    else {
        Write-Host "WARNING: No microsoft.gameBarUIExtension entries in installed manifest." -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "How to open the widget:" -ForegroundColor Cyan
Write-Host "  1. Open any game or app (Game Bar often needs a foreground app)."
Write-Host "  2. Press Win+G."
Write-Host "  3. Click the widget grid icon (top bar) -> Widget library / Biblioteca."
Write-Host "  4. Search 'Gemini Assistant' and Pin / Fijar."
Write-Host "  5. If missing: close Game Bar completely, redeploy from VS, Win+G again."
Write-Host ""
Write-Host "VS tip: Project Properties -> Debug -> Launch application = No, then F5 and open widget from Game Bar."
