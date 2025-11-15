# FINAL-Repository-Cleanup.ps1
# Consolidates duplicate documentation, removes obsolete files, organizes structure

param(
    [switch]$WhatIf
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "SentryXDR - Repository Cleanup Script" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Files to DELETE (duplicates/obsolete)
$filesToDelete = @(
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
    "README_DEPLOYMENT.md",
    "README_V2_ENHANCED.md",
    "DEPLOY_TO_AZURE.md",
    "Deployment\azuredeploy-complete.json",
    "Deployment\azuredeploy-fixed.json",
    "Deployment\scripts\Cleanup-Repository.ps1",
    "Deployment\scripts\Push-ToGitHub.ps1",
    "Deployment\scripts\Build-DeploymentPackage.ps1",
    "create-deployment-package.ps1",
    "setup-app-registration.ps1",
    "cleanup-repo.ps1"
)

# Files to KEEP (essential)
$essentialFiles = @(
    "README.md",                              # Main project page with Deploy button
    "LICENSE",                                # MIT License
    "CONTRIBUTING.md",                        # Contribution guidelines
    "DEPLOYMENT_GUIDE.md",                    # Complete deployment instructions
    "FINAL_PROJECT_STATUS.md",               # Project status and metrics
    "PRODUCTION_READINESS_CHECKLIST.md",     # Production readiness
    "WEB_DEPLOYMENT_PACKAGE_GUIDE.md",       # Package creation guide
    "DEPLOYMENT_PACKAGE_COMPLETE.md",        # Package details
    "V2_ROADMAP_API_WORKBOOK.md",            # Future roadmap
    "Deployment\azuredeploy.json",           # Main ARM template
    "Deployment\azuredeploy.parameters.json", # Parameter examples
    "Deployment\azure-pipelines.yml",        # DevOps pipeline
    "Deployment\scripts\Setup-SentryXDR-Permissions-COMPLETE.ps1",
    "Deployment\scripts\Deploy-SentryXDR.ps1",
    "Deployment\scripts\Build-FunctionAppPackage.ps1",
    "Deployment\scripts\Create-GitHubRelease.ps1",
    "Deployment\scripts\Final-Push-To-GitHub.ps1"
)

Write-Host "[CLEANUP] Analyzing repository structure...`n" -ForegroundColor Yellow

$deletedCount = 0
$keptCount = 0
$errors = @()

foreach ($file in $filesToDelete) {
    if (Test-Path $file) {
        try {
            if ($WhatIf) {
                Write-Host "[WOULD DELETE] $file" -ForegroundColor Yellow
            }
            else {
                Remove-Item $file -Force
                Write-Host "[DELETED] $file" -ForegroundColor Red
                $deletedCount++
            }
        }
        catch {
            $errors += "Failed to delete ${file}: $_"
            Write-Host "[ERROR] $file - $_" -ForegroundColor Red
        }
    }
}

Write-Host "`n[VERIFICATION] Essential files check..." -ForegroundColor Yellow
foreach ($file in $essentialFiles) {
    if (Test-Path $file) {
        Write-Host "[OK] $file" -ForegroundColor Green
        $keptCount++
    }
    else {
        Write-Host "[MISSING] $file" -ForegroundColor Red
        $errors += "Essential file missing: $file"
    }
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Cleanup Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Files deleted: $deletedCount" -ForegroundColor $(if ($deletedCount -gt 0) { "Yellow" } else { "Green" })
Write-Host "Essential files verified: $keptCount / $($essentialFiles.Count)" -ForegroundColor Green
Write-Host "Errors: $($errors.Count)" -ForegroundColor $(if ($errors.Count -gt 0) { "Red" } else { "Green" })

if ($errors.Count -gt 0) {
    Write-Host "`nErrors encountered:" -ForegroundColor Red
    $errors | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
}

if ($WhatIf) {
    Write-Host "`n[WHAT-IF MODE] No files were actually deleted." -ForegroundColor Cyan
    Write-Host "Run without -WhatIf to perform cleanup." -ForegroundColor Cyan
}
else {
    Write-Host "`n[COMPLETE] Repository cleanup finished!" -ForegroundColor Green
}
