# Cleanup-Repository.ps1
# Removes outdated documentation and ensures clean repository structure

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "SentryXDR - Repository Cleanup Script" -ForegroundColor Cyan
Write-Host "================================================`n" -ForegroundColor Cyan

# Files to remove (outdated/incorrect documentation)
$filesToRemove = @(
    "ANALYSIS_SUMMARY.md",           # Wrong information (claims 75% complete, actually 97%)
    "IMPLEMENTATION_MILESTONE.md",   # Superseded by FINAL_PROJECT_STATUS.md
    "IMPLEMENTATION_ROADMAP.md",     # Outdated roadmap
    "CORRECTED_STATUS_AUDIT.md"      # Intermediate document, no longer needed
)

# Files to keep (current/accurate)
$filesToKeep = @(
    "README.md",
    "FINAL_PROJECT_STATUS.md",
    "FINAL_ACCURATE_STATUS.md",
    "DEPLOYMENT_GUIDE.md",
    "LICENSE",
    ".gitignore"
)

Write-Host "[STEP] Analyzing repository..." -ForegroundColor Yellow

$rootPath = Split-Path $PSScriptRoot -Parent
Set-Location $rootPath

# Remove outdated files
Write-Host "`n[STEP] Removing outdated files..." -ForegroundColor Yellow

foreach ($file in $filesToRemove) {
    $filePath = Join-Path $rootPath $file
    
    if (Test-Path $filePath) {
        if ($WhatIf) {
            Write-Host "[WHATIF] Would remove: $file" -ForegroundColor Cyan
        }
        else {
            Remove-Item -Path $filePath -Force
            Write-Host "[OK] Removed: $file" -ForegroundColor Green
        }
    }
    else {
        Write-Host "[INFO] Already removed: $file" -ForegroundColor Gray
    }
}

# Verify essential files exist
Write-Host "`n[STEP] Verifying essential files..." -ForegroundColor Yellow

$missingFiles = @()

foreach ($file in $filesToKeep) {
    $filePath = Join-Path $rootPath $file
    
    if (Test-Path $filePath) {
        Write-Host "[OK] Found: $file" -ForegroundColor Green
    }
    else {
        Write-Host "[WARN] Missing: $file" -ForegroundColor Magenta
        $missingFiles += $file
    }
}

# Check deployment files
Write-Host "`n[STEP] Verifying deployment infrastructure..." -ForegroundColor Yellow

$deploymentFiles = @(
    "deployment/azuredeploy.json",
    "deployment/scripts/Setup-SentryXDR-Permissions.ps1",
    "deployment/scripts/Deploy-SentryXDR.ps1",
    "deployment/azure-pipelines.yml"
)

foreach ($file in $deploymentFiles) {
    $filePath = Join-Path $rootPath $file
    
    if (Test-Path $filePath) {
        Write-Host "[OK] Found: $file" -ForegroundColor Green
    }
    else {
        Write-Host "[ERROR] Missing critical file: $file" -ForegroundColor Red
        $missingFiles += $file
    }
}

# Clean up temporary files
Write-Host "`n[STEP] Removing temporary files..." -ForegroundColor Yellow

$tempPatterns = @(
    "*.tmp",
    "*.bak",
    "*~",
    "deployment-params.json"  # Contains secrets, should not be committed
)

foreach ($pattern in $tempPatterns) {
    $files = Get-ChildItem -Path $rootPath -Filter $pattern -Recurse -File -ErrorAction SilentlyContinue
    
    foreach ($file in $files) {
        if ($WhatIf) {
            Write-Host "[WHATIF] Would remove temp file: $($file.FullName)" -ForegroundColor Cyan
        }
        else {
            Remove-Item -Path $file.FullName -Force
            Write-Host "[OK] Removed temp file: $($file.Name)" -ForegroundColor Green
        }
    }
}

# Update .gitignore
Write-Host "`n[STEP] Updating .gitignore..." -ForegroundColor Yellow

$gitignorePath = Join-Path $rootPath ".gitignore"
$gitignoreContent = @"
# Build results
bin/
obj/
[Dd]ebug/
[Rr]elease/
*.user
*.suo

# Deployment secrets
deployment-params.json
*.secrets.json

# VS Code
.vscode/

# Rider
.idea/

# Function App
local.settings.json

# Azure Functions
__blobstorage__
__queuestorage__
__azurite_db*__.json

# Logs
*.log

# Temporary files
*.tmp
*.bak
*~

# OS
.DS_Store
Thumbs.db
"@

if ($WhatIf) {
    Write-Host "[WHATIF] Would update .gitignore" -ForegroundColor Cyan
}
else {
    Set-Content -Path $gitignorePath -Value $gitignoreContent -Force
    Write-Host "[OK] Updated .gitignore" -ForegroundColor Green
}

# Summary
Write-Host "`n================================================" -ForegroundColor Cyan
Write-Host "Cleanup Summary" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan

Write-Host "`nFiles Removed:" -ForegroundColor Yellow
foreach ($file in $filesToRemove) {
    $filePath = Join-Path $rootPath $file
    if (-not (Test-Path $filePath)) {
        Write-Host "  ? $file" -ForegroundColor Green
    }
}

Write-Host "`nEssential Files Verified:" -ForegroundColor Yellow
foreach ($file in $filesToKeep) {
    $filePath = Join-Path $rootPath $file
    if (Test-Path $filePath) {
        Write-Host "  ? $file" -ForegroundColor Green
    }
}

Write-Host "`nDeployment Infrastructure:" -ForegroundColor Yellow
foreach ($file in $deploymentFiles) {
    $filePath = Join-Path $rootPath $file
    if (Test-Path $filePath) {
        Write-Host "  ? $file" -ForegroundColor Green
    }
}

if ($missingFiles.Count -gt 0) {
    Write-Host "`nMissing Files:" -ForegroundColor Red
    foreach ($file in $missingFiles) {
        Write-Host "  ? $file" -ForegroundColor Red
    }
    Write-Host "`nPlease create missing files before deployment!" -ForegroundColor Red
}
else {
    Write-Host "`n? Repository is clean and ready for deployment!" -ForegroundColor Green
}

Write-Host "`n================================================" -ForegroundColor Cyan
Write-Host "Recommended Git Commands:" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "# Commit cleanup changes" -ForegroundColor Yellow
Write-Host "git add ." -ForegroundColor White
Write-Host "git commit -m 'Repository cleanup - removed outdated docs, added deployment infrastructure'" -ForegroundColor White
Write-Host "git push origin main" -ForegroundColor White
Write-Host ""
Write-Host "# Tag release" -ForegroundColor Yellow
Write-Host "git tag -a v1.0.0 -m 'SentryXDR v1.0 - Production Ready (97% complete)'" -ForegroundColor White
Write-Host "git push origin v1.0.0" -ForegroundColor White
Write-Host ""

Write-Host "Cleanup completed!" -ForegroundColor Green
