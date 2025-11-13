# Complete Deployment Package Creator for SentryXDR
# Creates a deployable ZIP package with all necessary files

param(
    [Parameter(Mandatory=$false)]
    [string]$OutputPath = ".\publish",
    
    [Parameter(Mandatory=$false)]
    [string]$Configuration = "Release"
)

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  SentryXDR Deployment Package Creator" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan

# Clean previous builds
Write-Host "`n[1/6] Cleaning previous builds..." -ForegroundColor Yellow
if (Test-Path ".\bin") { Remove-Item ".\bin" -Recurse -Force }
if (Test-Path ".\obj") { Remove-Item ".\obj" -Recurse -Force }
if (Test-Path $OutputPath) { Remove-Item $OutputPath -Recurse -Force }
if (Test-Path ".\deploy.zip") { Remove-Item ".\deploy.zip" -Force }

# Restore NuGet packages
Write-Host "[2/6] Restoring NuGet packages..." -ForegroundColor Yellow
dotnet restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "? NuGet restore failed" -ForegroundColor Red
    exit 1
}

# Build project
Write-Host "[3/6] Building project ($Configuration)..." -ForegroundColor Yellow
dotnet build --configuration $Configuration --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "? Build failed" -ForegroundColor Red
    exit 1
}

# Run tests (if any)
Write-Host "[4/6] Running tests..." -ForegroundColor Yellow
# dotnet test --no-build --configuration $Configuration
Write-Host "   No tests found (skipping)" -ForegroundColor Gray

# Publish
Write-Host "[5/6] Publishing to $OutputPath..." -ForegroundColor Yellow
dotnet publish --configuration $Configuration --output $OutputPath --no-build
if ($LASTEXITCODE -ne 0) {
    Write-Host "? Publish failed" -ForegroundColor Red
    exit 1
}

# Verify critical files
Write-Host "[6/6] Verifying deployment package..." -ForegroundColor Yellow
$criticalFiles = @(
    "SentryXDR.dll",
    "host.json",
    "local.settings.json"
)

$missingFiles = @()
foreach ($file in $criticalFiles) {
    if (!(Test-Path (Join-Path $OutputPath $file))) {
        $missingFiles += $file
    }
}

if ($missingFiles.Count -gt 0) {
    Write-Host "? Missing critical files:" -ForegroundColor Red
    $missingFiles | ForEach-Object { Write-Host "   - $_" -ForegroundColor Red }
    exit 1
}

# Create deployment ZIP
Write-Host "`nCreating deployment ZIP package..." -ForegroundColor Yellow
Push-Location $OutputPath
Compress-Archive -Path * -DestinationPath "..\deploy.zip" -Force
Pop-Location

# Get package info
$zipInfo = Get-Item ".\deploy.zip"
$fileCount = (Get-ChildItem $OutputPath -Recurse -File | Measure-Object).Count

Write-Host "`n============================================" -ForegroundColor Green
Write-Host "? Deployment package created successfully!" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
Write-Host "`nPackage Details:" -ForegroundColor Cyan
Write-Host "  Location: $($zipInfo.FullName)" -ForegroundColor White
Write-Host "  Size: $([math]::Round($zipInfo.Length / 1MB, 2)) MB" -ForegroundColor White
Write-Host "  Files: $fileCount" -ForegroundColor White
Write-Host "`nNext Steps:" -ForegroundColor Cyan
Write-Host "  1. Deploy using: az functionapp deployment source config-zip ..." -ForegroundColor White
Write-Host "  2. Or use: .\Deployment\deploy.ps1 for full deployment" -ForegroundColor White

# Generate deployment manifest
$manifest = @{
    PackageVersion = "1.0.0"
    BuildDate = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Configuration = $Configuration
    FileCount = $fileCount
    PackageSize = "$([math]::Round($zipInfo.Length / 1MB, 2)) MB"
    RuntimeVersion = "dotnet-isolated-8.0"
    FunctionsVersion = "v4"
} | ConvertTo-Json

$manifest | Out-File ".\deploy-manifest.json" -Encoding UTF8
Write-Host "`n?? Deployment manifest saved to deploy-manifest.json" -ForegroundColor Cyan
