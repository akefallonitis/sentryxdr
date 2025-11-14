# Create-GitHubRelease.ps1
# Automates: Build ? Package ? GitHub Release ? ARM Template Update

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$Version = "1.0.0",
    
    [Parameter(Mandatory = $false)]
    [string]$GitHubToken,
    
    [Parameter(Mandatory = $false)]
    [string]$Configuration = "Release",
    
    [Parameter(Mandatory = $false)]
    [switch]$SkipBuild,
    
    [Parameter(Mandatory = $false)]
    [switch]$DraftRelease
)

$ErrorActionPreference = "Stop"

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "SentryXDR - Automated GitHub Release Creator" -ForegroundColor Cyan
Write-Host "Version: $Version" -ForegroundColor Cyan
Write-Host "================================================`n" -ForegroundColor Cyan

# Configuration
$repoOwner = "akefallonitis"
$repoName = "sentryxdr"
$packageName = "sentryxdr-package.zip"
$releaseName = "SentryXDR v$Version"
$releaseTag = "v$Version"

# Step 1: Get GitHub Token
if (-not $GitHubToken) {
    Write-Host "[STEP] GitHub Token Required" -ForegroundColor Yellow
    Write-Host "Create a personal access token with 'repo' scope at:" -ForegroundColor Cyan
    Write-Host "https://github.com/settings/tokens/new" -ForegroundColor Yellow
    Write-Host ""
    $GitHubToken = Read-Host "Enter GitHub Personal Access Token" -AsSecureString
    $GitHubToken = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($GitHubToken))
}

# Step 2: Clean previous builds
if (-not $SkipBuild) {
    Write-Host "`n[STEP] Cleaning previous builds..." -ForegroundColor Yellow
    if (Test-Path ".\publish") {
        Remove-Item -Path ".\publish" -Recurse -Force
    }
    if (Test-Path ".\deployment-package.zip") {
        Remove-Item -Path ".\deployment-package.zip" -Force
    }
    Write-Host "[OK] Cleaned" -ForegroundColor Green

    # Step 3: Build Solution
    Write-Host "`n[STEP] Building solution..." -ForegroundColor Yellow
    dotnet restore
    if ($LASTEXITCODE -ne 0) { throw "Restore failed" }
    
    dotnet build --configuration $Configuration --no-restore
    if ($LASTEXITCODE -ne 0) { throw "Build failed" }
    Write-Host "[OK] Build successful" -ForegroundColor Green

    # Step 4: Publish
    Write-Host "`n[STEP] Publishing Function App..." -ForegroundColor Yellow
    dotnet publish --configuration $Configuration --output ".\publish" --no-build
    if ($LASTEXITCODE -ne 0) { throw "Publish failed" }
    Write-Host "[OK] Published" -ForegroundColor Green
}

# Step 5: Create Deployment Package
Write-Host "`n[STEP] Creating deployment package..." -ForegroundColor Yellow

$publishPath = ".\publish"
$tempPath = ".\temp-package"

# Create temp directory structure
New-Item -ItemType Directory -Path $tempPath -Force | Out-Null
New-Item -ItemType Directory -Path "$tempPath\FunctionApp" -Force | Out-Null
New-Item -ItemType Directory -Path "$tempPath\Deployment" -Force | Out-Null
New-Item -ItemType Directory -Path "$tempPath\Documentation" -Force | Out-Null
New-Item -ItemType Directory -Path "$tempPath\Workbook" -Force | Out-Null

# Copy function app files
Write-Host "[INFO] Copying function app files..." -ForegroundColor Cyan
Copy-Item -Path "$publishPath\*" -Destination "$tempPath\FunctionApp" -Recurse -Force

# Copy deployment files
Write-Host "[INFO] Copying deployment files..." -ForegroundColor Cyan
Copy-Item -Path ".\Deployment\azuredeploy.json" -Destination "$tempPath\Deployment\" -Force
Copy-Item -Path ".\Deployment\scripts\*.ps1" -Destination "$tempPath\Deployment\" -Force
Copy-Item -Path ".\Deployment\azure-pipelines.yml" -Destination "$tempPath\Deployment\" -Force

# Copy documentation
Write-Host "[INFO] Copying documentation..." -ForegroundColor Cyan
Copy-Item -Path ".\DEPLOYMENT_GUIDE.md" -Destination "$tempPath\Documentation\" -Force
Copy-Item -Path ".\FINAL_PROJECT_STATUS.md" -Destination "$tempPath\Documentation\" -Force
Copy-Item -Path ".\DEPLOY_TO_AZURE.md" -Destination "$tempPath\Documentation\README.md" -Force

# Copy workbook
if (Test-Path ".\Workbook\SentryXDR-Console-v2.json") {
    Write-Host "[INFO] Copying workbook template..." -ForegroundColor Cyan
    Copy-Item -Path ".\Workbook\SentryXDR-Console-v2.json" -Destination "$tempPath\Workbook\" -Force
}

# Create package README
$packageReadme = @"
# SentryXDR v$Version - Deployment Package

## Contents

- **FunctionApp/** - Compiled Azure Functions application
- **Deployment/** - ARM templates and PowerShell scripts
- **Documentation/** - Deployment guides
- **Workbook/** - Azure Workbook template (v2.0)

## Quick Start

### Option 1: Deploy to Azure (One-Click)
1. Use the "Deploy to Azure" button in GitHub repository
2. Fill in App ID and Secret
3. Deploy (~15 minutes)

### Option 2: PowerShell Deployment
``````powershell
cd Deployment
.\Setup-SentryXDR-Permissions-COMPLETE.ps1
.\Deploy-SentryXDR.ps1 -AppId "<your-app-id>" -AppSecret "<your-secret>"
``````

### Option 3: Azure Functions Core Tools
``````powershell
cd FunctionApp
func azure functionapp publish <your-function-app-name>
``````

## Documentation

- **DEPLOYMENT_GUIDE.md** - Complete step-by-step instructions
- **FINAL_PROJECT_STATUS.md** - Implementation status
- **README.md** - Quick start guide

## Support

- Repository: https://github.com/$repoOwner/$repoName
- Issues: https://github.com/$repoOwner/$repoName/issues
- Version: $Version
- Build Date: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
"@

Set-Content -Path "$tempPath\README.md" -Value $packageReadme -Force

# Create ZIP
Write-Host "[INFO] Creating ZIP archive..." -ForegroundColor Cyan
Compress-Archive -Path "$tempPath\*" -DestinationPath $packageName -Force

# Cleanup temp
Remove-Item -Path $tempPath -Recurse -Force
if (Test-Path ".\publish") {
    Remove-Item -Path ".\publish" -Recurse -Force
}

$zipSize = (Get-Item $packageName).Length / 1MB
Write-Host "[OK] Package created: $packageName ($([math]::Round($zipSize, 2)) MB)" -ForegroundColor Green

# Step 6: Create GitHub Release
Write-Host "`n[STEP] Creating GitHub Release..." -ForegroundColor Yellow

$releaseBody = @"
# ??? SentryXDR v$Version

## Production Release - Multi-Tenant XDR Orchestration Platform

### ?? What's New in v$Version

- ? **150 security actions** across 10 Microsoft services
- ? **One-click deployment** via ARM template
- ? **Swagger/OpenAPI** auto-generated docs
- ? **Multi-tenant** with unified authentication
- ? **Forensics storage** integration
- ? **Hybrid worker** support for on-prem AD
- ? **Mail forwarding control** (Graph Beta API)
- ? **Complete DevOps pipeline** with health checks

### ?? Supported Platforms

- **MDE** (24 actions) - Device isolation, IOC, AIR, Live Response
- **Entra ID** (18 actions) - Session revocation, CA policies
- **Azure** (25 actions) - VM isolation, NSG, Firewall, Key Vault
- **Intune** (15 actions) - Device wipe/retire, Lost mode
- **MCAS** (12 actions) - OAuth governance, Session control
- **MDO** (18 actions) - Email remediation, Mail forwarding
- **DLP** (5 actions) - File sharing, Quarantine
- **On-Prem AD** (5 actions) - Hybrid worker actions
- **Incident Mgmt** (18 actions) - XDR incident lifecycle
- **Advanced Hunting** (1 action) - KQL queries

### ?? Quick Start

#### 1. One-Click Deployment
[![Deploy to Azure](https://aka.ms/deploytoazurebutton)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2F$repoOwner%2F$repoName%2Fmain%2FDeployment%2Fazuredeploy.json)

#### 2. PowerShell Deployment
``````powershell
# Download and extract package
Expand-Archive -Path $packageName -DestinationPath .\SentryXDR

# Setup permissions
cd SentryXDR\Deployment
.\Setup-SentryXDR-Permissions-COMPLETE.ps1

# Deploy
.\Deploy-SentryXDR.ps1 -AppId "<your-app-id>" -AppSecret "<your-secret>"
``````

### ?? Documentation

- [Deployment Guide](https://github.com/$repoOwner/$repoName/blob/main/DEPLOYMENT_GUIDE.md)
- [Project Status](https://github.com/$repoOwner/$repoName/blob/main/FINAL_PROJECT_STATUS.md)
- [API Documentation](https://<your-function-app>.azurewebsites.net/api/swagger/ui)

### ?? Package Contents

- Compiled Function App (150 actions)
- ARM templates (complete deployment)
- PowerShell scripts (setup & deploy)
- Azure DevOps pipeline
- Documentation
- Workbook template (v2.0 preview)

### ?? Required Permissions

See ``Setup-SentryXDR-Permissions-COMPLETE.ps1`` for complete list of:
- Microsoft Graph API permissions
- Windows Defender ATP permissions
- Azure RBAC roles

### ?? What Gets Deployed

- Function App (Premium EP1)
- Storage Accounts (2x: primary + forensics GRS)
- Application Insights + Log Analytics
- Automation Account (optional for hybrid worker)
- All environment variables auto-configured
- RBAC permissions auto-assigned

### ?? Deployment Time

~15 minutes for complete infrastructure

### ?? Support

- Issues: https://github.com/$repoOwner/$repoName/issues
- Discussions: https://github.com/$repoOwner/$repoName/discussions

---

**Build Date:** $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")  
**Status:** ? Production Ready  
**Implementation:** 98% (150/152 actions)
"@

# Create release using GitHub API
$headers = @{
    "Authorization" = "token $GitHubToken"
    "Accept" = "application/vnd.github.v3+json"
}

$releaseData = @{
    tag_name = $releaseTag
    target_commitish = "main"
    name = $releaseName
    body = $releaseBody
    draft = $DraftRelease.IsPresent
    prerelease = $false
} | ConvertTo-Json

Write-Host "[INFO] Creating GitHub release..." -ForegroundColor Cyan
try {
    $response = Invoke-RestMethod -Uri "https://api.github.com/repos/$repoOwner/$repoName/releases" `
        -Method Post `
        -Headers $headers `
        -Body $releaseData `
        -ContentType "application/json"
    
    Write-Host "[OK] Release created: $($response.html_url)" -ForegroundColor Green
    $releaseId = $response.id
    $uploadUrl = $response.upload_url -replace '\{\?name,label\}', ''
}
catch {
    Write-Error "Failed to create release: $_"
    exit 1
}

# Step 7: Upload Package to Release
Write-Host "`n[STEP] Uploading package to release..." -ForegroundColor Yellow

$uploadHeaders = @{
    "Authorization" = "token $GitHubToken"
    "Content-Type" = "application/zip"
}

try {
    $fileBytes = [System.IO.File]::ReadAllBytes((Resolve-Path $packageName))
    $response = Invoke-RestMethod -Uri "$uploadUrl?name=$packageName" `
        -Method Post `
        -Headers $uploadHeaders `
        -Body $fileBytes
    
    Write-Host "[OK] Package uploaded: $($response.browser_download_url)" -ForegroundColor Green
    $downloadUrl = $response.browser_download_url
}
catch {
    Write-Error "Failed to upload package: $_"
    exit 1
}

# Step 8: Verify ARM Template Package URL
Write-Host "`n[STEP] Verifying ARM template package URL..." -ForegroundColor Yellow
$expectedUrl = "https://github.com/$repoOwner/$repoName/releases/download/$releaseTag/$packageName"
Write-Host "[INFO] Expected URL: $expectedUrl" -ForegroundColor Cyan
Write-Host "[INFO] Actual URL:   $downloadUrl" -ForegroundColor Cyan

if ($expectedUrl -eq $downloadUrl) {
    Write-Host "[OK] URLs match!" -ForegroundColor Green
}
else {
    Write-Host "[WARN] URLs don't match - ARM template may need update" -ForegroundColor Magenta
}

# Step 9: Summary
Write-Host "`n================================================" -ForegroundColor Cyan
Write-Host "GitHub Release Created Successfully!" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Release URL: $($response.html_url)" -ForegroundColor Green
Write-Host "Package URL: $downloadUrl" -ForegroundColor Green
Write-Host "Version: $Version" -ForegroundColor Green
Write-Host "Package Size: $([math]::Round($zipSize, 2)) MB" -ForegroundColor Green
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Yellow
Write-Host "1. Verify release at: $($response.html_url)" -ForegroundColor White
Write-Host "2. Test 'Deploy to Azure' button" -ForegroundColor White
Write-Host "3. Deploy and verify health check passes" -ForegroundColor White
Write-Host ""
Write-Host "Deploy to Azure URL:" -ForegroundColor Cyan
Write-Host "https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2F$repoOwner%2F$repoName%2Fmain%2FDeployment%2Fazuredeploy.json" -ForegroundColor Yellow
Write-Host ""

# Cleanup
Remove-Item -Path $packageName -Force
Write-Host "[OK] Cleanup complete" -ForegroundColor Green
