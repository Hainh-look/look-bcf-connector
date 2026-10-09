# =====================================================================
# Look BCF - Full Release Packaging Script
# Generates both:
#   1. LookBcf-Setup-v1.0.0.exe        (Inno Setup Installer)
#   2. LookBcf-Revit-v1.0.0-Portable.zip (1-Click Portable ZIP)
# =====================================================================

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$distDir = Join-Path $scriptDir "dist"
$version = "1.0.0"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "         PACKAGING LOOK BCF RELEASE v$version             " -ForegroundColor Yellow
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Clean and prepare dist directory
if (Test-Path $distDir) {
    Remove-Item -Path $distDir -Recurse -Force
}
New-Item -ItemType Directory -Path $distDir -Force | Out-Null

# 2. Build Release assemblies for net48 and net8.0-windows
Write-Host "`n[1/5] Compiling OpenProject.Revit (Release)..." -ForegroundColor Cyan
& dotnet build "$scriptDir\src\OpenProject.Revit\OpenProject.Revit.csproj" -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet build failed with exit code $LASTEXITCODE"
    exit 1
}

$net48Bin = Join-Path $scriptDir "src\OpenProject.Revit\bin\Release\net48"
$net8Bin  = Join-Path $scriptDir "src\OpenProject.Revit\bin\Release\net8.0-windows"

# 3. Ensure WebView2Loader.dll is in bin roots
Write-Host "`n[2/5] Verifying WebView2 runtime loaders..." -ForegroundColor Cyan
$loader48 = Join-Path $net48Bin "runtimes\win-x64\native\WebView2Loader.dll"
$loader8  = Join-Path $net8Bin "runtimes\win-x64\native\WebView2Loader.dll"
if (Test-Path $loader48) { Copy-Item $loader48 -Destination (Join-Path $net48Bin "WebView2Loader.dll") -Force }
if (Test-Path $loader8)  { Copy-Item $loader8 -Destination (Join-Path $net8Bin "WebView2Loader.dll") -Force }

# 4. Compile Inno Setup Installer
Write-Host "`n[3/5] Compiling Inno Setup EXE Installer..." -ForegroundColor Cyan
$isccPath = "C:\Users\huuha\AppData\Local\Programs\Inno Setup 6\iscc.exe"
if (-not (Test-Path $isccPath)) {
    $isccCmd = Get-Command iscc.exe -ErrorAction SilentlyContinue
    if ($isccCmd) { $isccPath = $isccCmd.Source }
}

if (Test-Path $isccPath) {
    & $isccPath "$scriptDir\InnoSetup\LookBcf-Setup.iss"
    Write-Host "  -> Successfully generated LookBcf-Setup-v$version.exe" -ForegroundColor Green
} else {
    Write-Warning "ISCC compiler not found. Skipping EXE installer creation."
}

# 5. Assemble Portable Package
Write-Host "`n[4/5] Assembling Portable Package..." -ForegroundColor Cyan
$portableFolder = Join-Path $distDir "LookBcf-Revit-v$version-Portable"
New-Item -ItemType Directory -Path (Join-Path $portableFolder "bin\net48") -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $portableFolder "bin\net8.0-windows") -Force | Out-Null

# Copy scripts & documentation
Copy-Item (Join-Path $scriptDir "InnoSetup\Install-LookBcf.bat") -Destination $portableFolder -Force
Copy-Item (Join-Path $scriptDir "InnoSetup\Install-LookBcf.ps1") -Destination $portableFolder -Force
Copy-Item (Join-Path $scriptDir "InnoSetup\Uninstall-LookBcf.bat") -Destination $portableFolder -Force
Copy-Item (Join-Path $scriptDir "InnoSetup\Uninstall-LookBcf.ps1") -Destination $portableFolder -Force
Copy-Item (Join-Path $scriptDir "InnoSetup\LookBcf.addin") -Destination $portableFolder -Force
Copy-Item (Join-Path $scriptDir "InnoSetup\HUONG-DAN-CAI-DAT.txt") -Destination $portableFolder -Force

# Copy binaries
Copy-Item "$net48Bin\*" -Destination (Join-Path $portableFolder "bin\net48") -Recurse -Force
Copy-Item "$net8Bin\*" -Destination (Join-Path $portableFolder "bin\net8.0-windows") -Recurse -Force

# 6. Compress Portable Package to ZIP
Write-Host "`n[5/5] Compressing Portable ZIP archive..." -ForegroundColor Cyan
$zipFile = Join-Path $distDir "LookBcf-Revit-v$version-Portable.zip"
Compress-Archive -Path "$portableFolder\*" -DestinationPath $zipFile -CompressionLevel Optimal

# Cleanup unzipped temp folder in dist
Remove-Item -Path $portableFolder -Recurse -Force

# 7. Summary
Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host "           PACKAGING COMPLETED SUCCESSFULLY!              " -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
Get-ChildItem -Path $distDir | Select-Object Name, @{Name="Size (MB)"; Expression={"{0:N2} MB" -f ($_.Length / 1MB)}}, LastWriteTime | Format-Table -AutoSize
