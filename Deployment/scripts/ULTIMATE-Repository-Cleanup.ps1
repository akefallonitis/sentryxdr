# ULTIMATE-Repository-Cleanup.ps1
# Final comprehensive cleanup - removes ALL duplicates and obsolete files

param(
    [switch]$Execute,
    [switch]$WhatIf = $true
)

Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "SentryXDR - ULTIMATE Repository Cleanup" -ForegroundColor Cyan
Write-Host "Final consolidation before production deployment" -ForegroundColor Cyan
Write-Host "================================================================`n" -ForegroundColor Cyan

# ==================== FILES TO DELETE ====================
$filesToDelete = @(
    # Duplicate status/summary files (keeping only FINAL_STATUS.md)
    "CORRECTED_STATUS_AUDIT.md",
    "FINAL_ACCURATE_STATUS.md",
    "ANALYSIS_SUMMARY.md",
    "COMPLETE_DELIVERY_SUMMARY.md",
    "EXECUTION_CHECKLIST.md",
    "FINAL_DEPLOYMENT_STATUS.md",
    "PROJECT_COMPLETE_SUMMARY.md",
    "COMPARISON_CHECKLIST.md",
    "COMPREHENSIVE_FINAL_SUMMARY.md",
    "EXECUTIVE_SUMMARY_v1.0.md",
    "FINAL_PROJECT_STATUS.md",  # Consolidated into FINAL_STATUS.md
    
    # Duplicate deployment docs (keeping only DEPLOYMENT_GUIDE.md)
    "README_DEPLOYMENT.md",
    "README_V2_ENHANCED.md",
    "DEPLOY_TO_AZURE.md",
    "DEPLOYMENT_VERIFICATION.md",
    "DEPLOYMENT_PACKAGE_STATUS.md",  # Info in FINAL_STATUS.md
    
    # Duplicate ARM templates (keeping only azuredeploy.json)
    "Deployment\azuredeploy-complete.json",
    "Deployment\azuredeploy-fixed.json",
    
    # Obsolete scripts (keeping only essential ones)
    "Deployment\scripts\Cleanup-Repository.ps1",
    "Deployment\scripts\Push-ToGitHub.ps1",
    "Deployment\scripts\Build-DeploymentPackage.ps1",
    "Deployment\scripts\Setup-SentryXDR-Permissions.ps1",  # Replaced by COMPLETE version
    "Deployment\scripts\Final-Push-To-GitHub.ps1",  # Obsolete
    "Deployment\scripts\FINAL-Repository-Cleanup.ps1",  # This will replace itself
    "create-deployment-package.ps1",
    "setup-app-registration.ps1",
    "cleanup-repo.ps1"
)

# ==================== ESSENTIAL FILES TO KEEP ====================
$essentialFiles = @{
    "Core Documentation" = @(
        "README.md",                                    # Main project page with Deploy button
        "LICENSE",                                      # MIT License
        "CONTRIBUTING.md",                              # Contribution guidelines
        "FINAL_STATUS.md"                              # Consolidated status (replaces all others)
    )
    
    "Deployment Documentation" = @(
        "DEPLOYMENT_GUIDE.md",                         # Complete deployment instructions
        "WEB_DEPLOYMENT_PACKAGE_GUIDE.md",            # Package creation guide
        "DEPLOYMENT_PACKAGE_COMPLETE.md",             # Package details
        "PRODUCTION_READINESS_CHECKLIST.md"           # Production checklist
    )
    
    "Architecture & Roadmap" = @(
        "ARCHITECTURE.md",                             # To be created - system architecture
        "V2_ROADMAP_API_WORKBOOK.md"                  # Future enhancements
    )
    
    "ARM Templates" = @(
        "Deployment\azuredeploy.json",                # Main ARM template
        "Deployment\azuredeploy.parameters.json"      # Parameter examples
    )
    
    "DevOps" = @(
        "Deployment\azure-pipelines.yml"              # CI/CD pipeline
    )
    
    "Essential Scripts" = @(
        "Deployment\scripts\Setup-SentryXDR-Permissions-COMPLETE.ps1",  # Permissions setup
        "Deployment\scripts\Deploy-SentryXDR.ps1",                       # Deployment
        "Deployment\scripts\Build-FunctionAppPackage.ps1",               # Package builder
        "Deployment\scripts\Create-GitHubRelease.ps1",                   # Release automation
        "Deployment\scripts\FINAL-Comprehensive-Validation.ps1"          # Validation
    )
    
    "Workbook" = @(
        "Workbook\SentryXDR-Console-v2.json"          # Azure Workbook
    )
}

# ==================== EXECUTION ====================
$deletedCount = 0
$keptCount = 0
$missingCount = 0
$errors = @()

Write-Host "[1/3] Analyzing files to delete...`n" -ForegroundColor Yellow

foreach ($file in $filesToDelete) {
    if (Test-Path $file) {
        if ($Execute) {
            try {
                Remove-Item $file -Force
                Write-Host "  [DELETED] $file" -ForegroundColor Red
                $deletedCount++
            }
            catch {
                Write-Host "  [ERROR] $file - $_" -ForegroundColor Red
                $errors += "Failed to delete $file"
            }
        }
        else {
            Write-Host "  [WOULD DELETE] $file" -ForegroundColor Yellow
        }
    }
}

Write-Host "`n[2/3] Verifying essential files...`n" -ForegroundColor Yellow

foreach ($category in $essentialFiles.Keys) {
    Write-Host "  Category: $category" -ForegroundColor Cyan
    foreach ($file in $essentialFiles[$category]) {
        if (Test-Path $file) {
            Write-Host "    ? $file" -ForegroundColor Green
            $keptCount++
        }
        else {
            Write-Host "    ??  MISSING: $file" -ForegroundColor Yellow
            $missingCount++
        }
    }
}

Write-Host "`n[3/3] Repository structure analysis...`n" -ForegroundColor Yellow

# Count source files
$sourceFiles = (Get-ChildItem -Path "Services" -Filter "*.cs" -Recurse -ErrorAction SilentlyContinue).Count
$functionFiles = (Get-ChildItem -Path "Functions" -Filter "*.cs" -Recurse -ErrorAction SilentlyContinue).Count
$modelFiles = (Get-ChildItem -Path "Models" -Filter "*.cs" -Recurse -ErrorAction SilentlyContinue).Count

Write-Host "  Source Code:" -ForegroundColor Cyan
Write-Host "    Services: $sourceFiles files" -ForegroundColor Green
Write-Host "    Functions: $functionFiles files" -ForegroundColor Green
Write-Host "    Models: $modelFiles files" -ForegroundColor Green

# ==================== SUMMARY ====================
Write-Host "`n================================================================" -ForegroundColor Cyan
Write-Host "Cleanup Summary" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan

if ($Execute) {
    Write-Host "Files deleted: $deletedCount" -ForegroundColor $(if ($deletedCount -gt 0) { "Yellow" } else { "Green" })
}
else {
    Write-Host "Files to delete: $($filesToDelete.Count)" -ForegroundColor Yellow
}

Write-Host "Essential files verified: $keptCount" -ForegroundColor Green
Write-Host "Missing files: $missingCount" -ForegroundColor $(if ($missingCount -gt 0) { "Yellow" } else { "Green" })
Write-Host "Errors: $($errors.Count)" -ForegroundColor $(if ($errors.Count -gt 0) { "Red" } else { "Green" })

if ($errors.Count -gt 0) {
    Write-Host "`nErrors:" -ForegroundColor Red
    $errors | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
}

if (-not $Execute) {
    Write-Host "`n[PREVIEW MODE] No files were deleted." -ForegroundColor Cyan
    Write-Host "Run with -Execute to perform cleanup." -ForegroundColor Cyan
    Write-Host "Example: .\ULTIMATE-Repository-Cleanup.ps1 -Execute" -ForegroundColor Cyan
}
else {
    Write-Host "`n? Cleanup complete!" -ForegroundColor Green
    Write-Host "`nRecommended next steps:" -ForegroundColor Cyan
    Write-Host "  1. Review remaining files" -ForegroundColor White
    Write-Host "  2. Run validation: .\Deployment\scripts\FINAL-Comprehensive-Validation.ps1 -All" -ForegroundColor White
    Write-Host "  3. Commit changes: git add -A && git commit -m 'chore: Final repository cleanup'" -ForegroundColor White
    Write-Host "  4. Push to GitHub: git push origin main" -ForegroundColor White
}

Write-Host "`n================================================================`n" -ForegroundColor Cyan
