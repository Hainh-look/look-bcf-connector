# =====================================================================
# Look BCF - 1-Click Installer for Autodesk Revit (2021 - 2026)
# =====================================================================

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$revitAddinsBase = "$env:APPDATA\Autodesk\Revit\Addins"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "      LOOK BCF - BCF CONNECTOR FOR AUTODESK REVIT         " -ForegroundColor Yellow
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "Target Server: https://bim.lookbim.com" -ForegroundColor Gray
Write-Host "Install Target: $revitAddinsBase`n" -ForegroundColor Gray

if (-not (Test-Path $revitAddinsBase)) {
    New-Item -ItemType Directory -Path $revitAddinsBase -Force | Out-Null
}

$net48Source = Join-Path $scriptDir "bin\net48"
$net8Source  = Join-Path $scriptDir "bin\net8.0-windows"
$manifestSource = Join-Path $scriptDir "LookBcf.addin"

if (-not (Test-Path $net48Source) -or -not (Test-Path $net8Source)) {
    Write-Error "Could not find 'bin\net48' or 'bin\net8.0-windows' in $scriptDir. Please ensure package is extracted completely."
    exit 1
}

$supportedYears = @("2021", "2022", "2023", "2024", "2025", "2026")
$detectedYears = $supportedYears | Where-Object { 
    (Test-Path (Join-Path $revitAddinsBase $_)) -or 
    (Test-Path "C:\ProgramData\Autodesk\Revit\Addins\$_") -or
    (Test-Path "HKLM:\SOFTWARE\Autodesk\Revit\$_")
}

# If no specific version detected, ask user or install to existing folders, or default to all
if ($detectedYears.Count -eq 0) {
    Write-Host "No existing Revit version folders detected. Installing for all versions (2021-2026)..." -ForegroundColor Yellow
    $detectedYears = $supportedYears
} else {
    Write-Host "Detected installed Revit versions: $($detectedYears -join ', ')" -ForegroundColor Green
}

# Check if Revit is running
while (Get-Process Revit -ErrorAction SilentlyContinue) {
    Write-Warning "Phat hien Autodesk Revit dang chay!"
    Write-Host "Vui long luu file va DONG PHAN MEM REVIT de tiep tuc cai dat." -ForegroundColor Yellow
    $ans = Read-Host "Nhan [Enter] sau khi da dong Revit (hoac go 'Q' de huy)"
    if ($ans -eq 'Q' -or $ans -eq 'q') {
        Write-Host "Da huy cai dat." -ForegroundColor Red
        exit 0
    }
}

$installedCount = 0

foreach ($year in $detectedYears) {
    $destYearDir = Join-Path $revitAddinsBase $year
    $destModuleDir = Join-Path $destYearDir "LookBcf"
    $destAddinManifest = Join-Path $destYearDir "LookBcf.addin"

    $sourceDir = if ([int]$year -ge 2025) { $net8Source } else { $net48Source }
    $runtimeLabel = if ([int]$year -ge 2025) { ".NET 8.0 Windows" } else { ".NET Framework 4.8" }

    Write-Host "`n-> Installing Look BCF for Revit $year ($runtimeLabel)..." -ForegroundColor Cyan

    if (-not (Test-Path $destYearDir)) {
        New-Item -ItemType Directory -Path $destYearDir -Force | Out-Null
    }
    if (-not (Test-Path $destModuleDir)) {
        New-Item -ItemType Directory -Path $destModuleDir -Force | Out-Null
    }

    # Copy files
    Get-ChildItem -Path $sourceDir -Exclude "*.addin" | ForEach-Object {
        try {
            Copy-Item -Path $_.FullName -Destination $destModuleDir -Recurse -Force -ErrorAction Stop
        } catch {
            Write-Warning "File $($_.Name) locked (Revit $year may be currently running). Skipping locked file."
        }
    }

    # Copy .addin manifest
    Copy-Item -Path $manifestSource -Destination $destAddinManifest -Force
    Write-Host "   [OK] Installed successfully to $destModuleDir" -ForegroundColor Green
    $installedCount++
}

Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host "   DA CAI DAT THANH CONG LOOK BCF CHO $installedCount PHIEN BAN REVIT!" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
Write-Host "Khoi dong Autodesk Revit va click vao tab 'Look BCF' tren thanh Ribbon." -ForegroundColor White
Write-Host "Neu gap thong bao yeu cau cho phep Add-in, vui long chon 'Always Load'." -ForegroundColor Yellow
Write-Host "`nNhan phim bat ky de hoan tat..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
