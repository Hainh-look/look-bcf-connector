# Deploy Look BCF to Autodesk Revit Add-ins directory
param(
    [string]$RevitVersion = "All",
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$revitAddinsBase = "$env:APPDATA\Autodesk\Revit\Addins"

if (-not (Test-Path $revitAddinsBase)) {
    Write-Error "Revit Addins base directory not found at $revitAddinsBase"
    exit 1
}

$net48Source = Join-Path $scriptDir "src\OpenProject.Revit\bin\$Configuration\net48"
$net8Source  = Join-Path $scriptDir "src\OpenProject.Revit\bin\$Configuration\net8.0-windows"

if (-not (Test-Path $net48Source)) {
    Write-Host "Building net48 and net8.0-windows projects ($Configuration)..." -ForegroundColor Cyan
    & dotnet build "$scriptDir\src\OpenProject.Revit\OpenProject.Revit.csproj" -c $Configuration
}

$supportedYears = @("2021", "2022", "2023", "2024", "2025", "2026")

$targetYears = if ($RevitVersion -eq "All") {
    $supportedYears | Where-Object { Test-Path (Join-Path $revitAddinsBase $_) }
} else {
    @($RevitVersion)
}

if ($targetYears.Count -eq 0) {
    Write-Warning "No installed Revit version folders found in $revitAddinsBase matching: $($supportedYears -join ', ')"
    exit 0
}

Write-Host "=== Deploying Look BCF Add-In ===" -ForegroundColor Green
Write-Host "Target Revit versions: $($targetYears -join ', ')" -ForegroundColor Yellow

foreach ($year in $targetYears) {
    $destYearDir = Join-Path $revitAddinsBase $year
    $destModuleDir = Join-Path $destYearDir "LookBcf"
    $destAddinManifest = Join-Path $destYearDir "LookBcf.addin"

    $sourceDir = if ([int]$year -ge 2025) { $net8Source } else { $net48Source }
    $runtimeLabel = if ([int]$year -ge 2025) { ".NET 8.0-windows" } else { ".NET Framework 4.8" }

    Write-Host "`nDeploying for Revit $year ($runtimeLabel)..." -ForegroundColor Cyan

    # Ensure destination module folder exists
    if (-not (Test-Path $destModuleDir)) {
        New-Item -ItemType Directory -Path $destModuleDir -Force | Out-Null
    }

    # Copy binary files
    Get-ChildItem -Path $sourceDir -Exclude "*.addin" | ForEach-Object {
        Copy-Item -Path $_.FullName -Destination $destModuleDir -Recurse -Force
    }

    # Write .addin manifest file
    $addinXml = @"
<?xml version="1.0" encoding="utf-8" standalone="no"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>Look BCF for Revit $year</Name>
    <Assembly>LookBcf\OpenProject.Revit.dll</Assembly>
    <AddInId>5f96a79f-0e28-4d02-be10-251c8032a270</AddInId>
    <FullClassName>OpenProject.Revit.Entry.AppMain</FullClassName>
    <VendorId>LookSpace</VendorId>
    <VendorDescription>LookSpace BIM - Look BCF Connector</VendorDescription>
  </AddIn>
</RevitAddIns>
"@

    Set-Content -Path $destAddinManifest -Value $addinXml -Encoding UTF8
    Write-Host "  -> Installed manifest: $destAddinManifest" -ForegroundColor Green
    Write-Host "  -> Installed binaries: $destModuleDir" -ForegroundColor Green
}

Write-Host "`n=== Deployment Completed Successfully! ===" -ForegroundColor Green
Write-Host "You can now start Autodesk Revit and see 'Look BCF' on the Ribbon tab." -ForegroundColor Cyan
