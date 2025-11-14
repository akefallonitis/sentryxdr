# Build-DeploymentPackage.ps1
# Creates a complete deployment package ZIP for SentryXDR

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$OutputPath = ".\deployment-package.zip",
    
    [Parameter(Mandatory = $false)]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "SentryXDR - Deployment Package Builder" -ForegroundColor Cyan
Write-Host "================================================`n" -ForegroundColor Cyan

# Step 1: Clean previous builds
Write-Host "[STEP] Cleaning previous builds..." -ForegroundColor Yellow
if (Test-Path ".\publish") {
    Remove-Item -Path ".\publish" -Recurse -Force
}
if (Test-Path $OutputPath) {
    Remove-Item -Path $OutputPath -Force
}

# Step 2: Restore dependencies
Write-Host "`n[STEP] Restoring NuGet packages..." -ForegroundColor Yellow
dotnet restore
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to restore packages"
}

# Step 3: Build solution
Write-Host "`n[STEP] Building solution ($Configuration)..." -ForegroundColor Yellow
dotnet build --configuration $Configuration --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to build solution"
}

# Step 4: Publish
Write-Host "`n[STEP] Publishing Function App..." -ForegroundColor Yellow
dotnet publish --configuration $Configuration --output ".\publish" --no-build
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to publish"
}

# Step 5: Create deployment package
Write-Host "`n[STEP] Creating deployment package..." -ForegroundColor Yellow

$publishPath = ".\publish"
$tempPath = ".\temp-package"

# Create temp directory structure
New-Item -ItemType Directory -Path $tempPath -Force | Out-Null
New-Item -ItemType Directory -Path "$tempPath\FunctionApp" -Force | Out-Null
New-Item -ItemType Directory -Path "$tempPath\Deployment" -Force | Out-Null
New-Item -ItemType Directory -Path "$tempPath\Documentation" -Force | Out-Null

# Copy function app files
Write-Host "[INFO] Copying function app files..." -ForegroundColor Cyan
Copy-Item -Path "$publishPath\*" -Destination "$tempPath\FunctionApp" -Recurse -Force

# Copy deployment files
Write-Host "[INFO] Copying deployment files..." -ForegroundColor Cyan
Copy-Item -Path ".\Deployment\azuredeploy-complete.json" -Destination "$tempPath\Deployment\azuredeploy.json" -Force
Copy-Item -Path ".\Deployment\scripts\*.ps1" -Destination "$tempPath\Deployment" -Force
Copy-Item -Path ".\Deployment\azure-pipelines.yml" -Destination "$tempPath\Deployment" -Force

# Copy documentation
Write-Host "[INFO] Copying documentation..." -ForegroundColor Cyan
Copy-Item -Path ".\DEPLOYMENT_GUIDE.md" -Destination "$tempPath\Documentation" -Force
Copy-Item -Path ".\FINAL_PROJECT_STATUS.md" -Destination "$tempPath\Documentation" -Force
Copy-Item -Path ".\DEPLOY_TO_AZURE.md" -Destination "$tempPath\Documentation\README.md" -Force

# Create README for package
$readmeContent = @"
# SentryXDR Deployment Package

## Contents

- **FunctionApp/** - Compiled Azure Functions application
- **Deployment/** - ARM templates and PowerShell scripts
- **Documentation/** - Deployment guides and status docs

## Quick Start

### Option 1: Deploy to Azure (Portal)
1. Go to Azure Portal
2. Create new resource
3. Search for "Template Deployment"
4. Upload ``Deployment/azuredeploy.json``
5. Fill in parameters
6. Deploy

### Option 2: Deploy via PowerShell
``````powershell
cd Deployment
.\Setup-SentryXDR-Permissions.ps1 -ExistingAppId "<your-app-id>"
.\Deploy-SentryXDR.ps1 -ResourceGroupName "sentryxdr-rg"
``````

### Option 3: Deploy Function Code Only
``````powershell
cd FunctionApp
func azure functionapp publish <your-function-app-name>
``````

## Documentation

See ``Documentation/`` folder for:
- DEPLOYMENT_GUIDE.md - Complete deployment instructions
- FINAL_PROJECT_STATUS.md - Project status and features
- README.md - Deploy to Azure button

## Support

- GitHub: https://github.com/akefallonitis/sentryxdr
- Issues: https://github.com/akefallonitis/sentryxdr/issues

---
**Version:** 1.0
**Build Date:** $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
"@

Set-Content -Path "$tempPath\README.md" -Value $readmeContent -Force

# Create ZIP
Write-Host "[INFO] Creating ZIP archive..." -ForegroundColor Cyan
Compress-Archive -Path "$tempPath\*" -DestinationPath $OutputPath -Force

# Cleanup
Remove-Item -Path $tempPath -Recurse -Force
Remove-Item -Path ".\publish" -Recurse -Force

$zipSize = (Get-Item $OutputPath).Length / 1MB

Write-Host "`n================================================" -ForegroundColor Cyan
Write-Host "Deployment Package Created!" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Package Location: $OutputPath" -ForegroundColor Green
Write-Host "Package Size: $([math]::Round($zipSize, 2)) MB" -ForegroundColor Green
Write-Host ""
Write-Host "Contents:" -ForegroundColor Yellow
Write-Host "  - Function App (compiled)" -ForegroundColor White
Write-Host "  - ARM Templates" -ForegroundColor White
Write-Host "  - PowerShell Scripts" -ForegroundColor White
Write-Host "  - Documentation" -ForegroundColor White
Write-Host ""
Write-Host "Ready for deployment!" -ForegroundColor Green
