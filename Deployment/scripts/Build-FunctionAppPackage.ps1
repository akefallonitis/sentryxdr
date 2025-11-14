# Build-FunctionAppPackage.ps1
# Creates a proper Azure Functions Web Deployment Package

param(
    [string]$OutputPath = "sentryxdr-deploy.zip",
    [string]$Configuration = "Release"
)

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "Creating Azure Functions Deployment Package" -ForegroundColor Cyan
Write-Host "================================================`n" -ForegroundColor Cyan

# Step 1: Clean and build
Write-Host "[1/5] Cleaning solution..." -ForegroundColor Yellow
dotnet clean --configuration $Configuration | Out-Null

Write-Host "[2/5] Restoring packages..." -ForegroundColor Yellow
dotnet restore | Out-Null

Write-Host "[3/5] Building solution ($Configuration)..." -ForegroundColor Yellow
dotnet build --configuration $Configuration --no-restore

if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] Build failed! Please fix compilation errors first." -ForegroundColor Red
    exit 1
}

# Step 4: Publish to temporary folder
Write-Host "[4/5] Publishing Function App..." -ForegroundColor Yellow
$publishFolder = ".\bin\publish"

if (Test-Path $publishFolder) {
    Remove-Item -Path $publishFolder -Recurse -Force
}

dotnet publish --configuration $Configuration --output $publishFolder --no-build

if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] Publish failed!" -ForegroundColor Red
    exit 1
}

# Step 5: Create ZIP (Azure Functions format)
Write-Host "[5/5] Creating deployment package..." -ForegroundColor Yellow

if (Test-Path $OutputPath) {
    Remove-Item -Path $OutputPath -Force
}

# Azure Functions expects files at root of ZIP, not in a subfolder
Compress-Archive -Path "$publishFolder\*" -DestinationPath $OutputPath -Force

# Cleanup
Remove-Item -Path $publishFolder -Recurse -Force

$zipSize = (Get-Item $OutputPath).Length / 1MB
Write-Host "`n================================================" -ForegroundColor Green
Write-Host "SUCCESS! Deployment package created" -ForegroundColor Green
Write-Host "================================================" -ForegroundColor Green
Write-Host "File: $OutputPath" -ForegroundColor Yellow
Write-Host "Size: $([math]::Round($zipSize, 2)) MB" -ForegroundColor Yellow
Write-Host "`nThis ZIP is ready for:" -ForegroundColor Cyan
Write-Host "  1. WEBSITE_RUN_FROM_PACKAGE (upload to storage/GitHub)" -ForegroundColor White
Write-Host "  2. Azure Functions Core Tools deployment" -ForegroundColor White
Write-Host "  3. Azure DevOps pipeline artifact" -ForegroundColor White
Write-Host ""
