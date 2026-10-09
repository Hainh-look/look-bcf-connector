# =====================================================================
# Look BCF - 1-Click Uninstaller for Autodesk Revit (2021 - 2026)
# =====================================================================

$ErrorActionPreference = "SilentlyContinue"
$revitAddinsBase = "$env:APPDATA\Autodesk\Revit\Addins"

Write-Host "==========================================================" -ForegroundColor Red
Write-Host "             GO CAI DAT LOOK BCF REVIT ADD-IN             " -ForegroundColor Yellow
Write-Host "==========================================================" -ForegroundColor Red

$supportedYears = @("2021", "2022", "2023", "2024", "2025", "2026")

foreach ($year in $supportedYears) {
    $destYearDir = Join-Path $revitAddinsBase $year
    $destModuleDir = Join-Path $destYearDir "LookBcf"
    $destAddinManifest = Join-Path $destYearDir "LookBcf.addin"

    if (Test-Path $destAddinManifest) {
        Remove-Item -Path $destAddinManifest -Force
        Write-Host "-> Da go bo manifest: $destAddinManifest" -ForegroundColor Green
    }
    if (Test-Path $destModuleDir) {
        Remove-Item -Path $destModuleDir -Recurse -Force
        Write-Host "-> Da xoa thu muc: $destModuleDir" -ForegroundColor Green
    }
}

Write-Host "`nHoan tat go cai dat Look BCF tren toan bo phien ban Revit." -ForegroundColor Cyan
Write-Host "Nhan phim bat ky de thoat..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
