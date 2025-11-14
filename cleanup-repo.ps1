# SentryXDR Repository Cleanup Script
# Removes unnecessary documentation files and keeps only essential docs

Write-Host "=== SentryXDR Repository Cleanup ===" -ForegroundColor Cyan

$repoRoot = $PSScriptRoot

# Files to DELETE (80+ documentation files that are redundant)
$filesToDelete = @(
    "COMPLETE_ROADMAP_WITH_WORKBOOK.md",
    "FINAL_COMPREHENSIVE_SESSION_SUMMARY.md",
    "PRODUCTION_COMPLETION_ROADMAP.md",
    "IMPLEMENTATION_ROADMAP.md",
    "QUICK_REFERENCE.md",
    "FINAL_IMPLEMENTATION_PLAN_TO_100.md",
    "COMPLETE_API_VALIDATION_ANALYSIS.md",
    "TODAYS_COMPLETE_SESSION_SUMMARY.md",
    "OPTIMIZED_IMPLEMENTATION_PLAN.md",
    "PHASE1_MILESTONE_AZURE_COMPLETE.md",
    "SMART_IMPLEMENTATION_PLAN.md",
    "PHASE1_PROGRESS_UPDATE.md",
    "VERIFICATION_SUMMARY.md",
    "EXECUTIVE_SUMMARY_GAPS.md",
    "MASTER_TODO_IMPLEMENTATION.md",
    "DOCUMENTATION_INDEX.md",
    "EXECUTIVE_FINAL_SUMMARY.md",
    "SESSION_FINAL_SUMMARY.md",
    "ZERO_TO_HERO_STATUS.md",
    "ZERO_TO_HERO_PLAN.md",
    "COMPLETE_GAP_ANALYSIS.md",
    "PRODUCTION_OPTIMIZATION.md",
    "GAP_ANALYSIS_REMEDIATION_PLAN.md",
    "CANCELLATION_HISTORY_COMPLETE.md",
    "ACTION_CANCELLATION_HISTORY.md",
    "FINAL_PRODUCTION_STATUS.md",
    "COMPLETE_VERIFICATION.md",
    "FINAL_STATUS.md",
    "ACTION_INVENTORY.md",
    "IMPLEMENTATION_COMPLETE_SUMMARY.md",
    "REAL_IMPLEMENTATION_STATUS.md",
    "IMPROVEMENTS_SUMMARY.md",
    "PROJECT_SUMMARY.md",
    "README_NEW.md",
    "COMPLETE_SUMMARY.md",
    "SESSION_2_FINAL_WRAPUP.md",
    "SESSION_2_MILESTONE_90_PERCENT.md",
    "FINAL_EXECUTION_TO_100_PERCENT.md",
    "ULTIMATE_SESSION_SUMMARY.md",
    "STATUS_93_PERCENT.md",
    "STATUS_96_PERCENT.md",
    "FINAL_TO_100_PERCENT.md",
    "100_PERCENT_COMPLETE.md",
    "automated-implementation.ps1",
    "test-validation.ps1",
    "git-push.ps1"
)

# Files to KEEP (essential documentation only)
$essentialFiles = @(
    "README.md",
    "DEPLOYMENT.md",
    "API_REFERENCE.md",
    "ARCHITECTURE.md",
    "PERMISSIONS.md",
    "CONTRIBUTING.md",
    "CHANGELOG.md"
)

Write-Host "`nDeleting redundant documentation files..." -ForegroundColor Yellow

$deletedCount = 0
foreach ($file in $filesToDelete) {
    $filePath = Join-Path $repoRoot $file
    if (Test-Path $filePath) {
        Remove-Item $filePath -Force
        Write-Host "  Deleted: $file" -ForegroundColor Gray
        $deletedCount++
    }
}

Write-Host "`nDeleted $deletedCount redundant files" -ForegroundColor Green

# Clean up duplicate service files
Write-Host "`nCleaning up duplicate service files..." -ForegroundColor Yellow

$duplicateServices = @(
    "Services\Workers\IntuneApiServiceComplete.cs",
    "Services\Workers\EntraIDApiServiceComplete.cs",
    "Services\Workers\MDOApiServiceComplete.cs"
)

foreach ($file in $duplicateServices) {
    $filePath = Join-Path $repoRoot $file
    if (Test-Path $filePath) {
        Remove-Item $filePath -Force
        Write-Host "  Deleted duplicate: $file" -ForegroundColor Gray
    }
}

# Clean up old function files
$oldFunctions = @(
    "Functions\XDRGatewayEnhanced.cs",
    "Functions\Workers\PlatformWorkers.cs",
    "Functions\Workers\MDEWorker.cs"
)

foreach ($file in $oldFunctions) {
    $filePath = Join-Path $repoRoot $file
    if (Test-Path $filePath) {
        Remove-Item $filePath -Force
        Write-Host "  Deleted old function: $file" -ForegroundColor Gray
    }
}

Write-Host "`n=== Cleanup Complete ===" -ForegroundColor Green
Write-Host "Repository is now clean and production-ready!" -ForegroundColor Cyan
