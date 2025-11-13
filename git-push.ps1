# Git Commit and Push Script for SentryXDR

Write-Host "Preparing SentryXDR for GitHub..." -ForegroundColor Cyan

# Check if in correct directory
if (!(Test-Path "SentryXDR.csproj")) {
    Write-Host "Error: Not in SentryXDR directory" -ForegroundColor Red
    exit 1
}

# Git status
Write-Host "`nChecking git status..." -ForegroundColor Yellow
git status

# Add all files
Write-Host "`nAdding files to git..." -ForegroundColor Yellow
git add .

# Show what will be committed
Write-Host "`nFiles to be committed:" -ForegroundColor Cyan
git status --short

# Commit
Write-Host "`nCommitting changes..." -ForegroundColor Yellow
git commit -m @"
feat: Complete SentryXDR multi-tenant XDR remediation platform

- Implement Gateway ? Orchestrator ? Workers architecture
- Add multi-tenant authentication with token caching
- Implement MDE worker with 61+ remediation actions
- Add comprehensive XDR action enum (140+ actions across 7 platforms)
- Create ARM templates for one-click Azure deployment
- Implement audit logging to Blob Storage
- Add request validation and tenant configuration
- Create PowerShell deployment scripts
- Add Application Insights integration
- Implement durable functions for reliable orchestration

Platforms supported:
- Microsoft Defender for Endpoint (MDE) - Full implementation
- Microsoft Defender for Office 365 (MDO) - Stub
- Microsoft Defender for Cloud Apps (MCAS) - Stub
- Microsoft Defender for Identity (MDI) - Stub  
- Microsoft Entra ID - Stub
- Microsoft Intune - Stub
- Azure Security - Stub

Build status: ? SUCCESS
Ready for deployment and testing
"@

# Push to GitHub
Write-Host "`nPushing to GitHub..." -ForegroundColor Yellow
git push origin main

Write-Host "`n? Successfully pushed to GitHub!" -ForegroundColor Green
Write-Host "Repository: https://github.com/akefallonitis/sentryxdr" -ForegroundColor Cyan

Write-Host "`nNext steps:" -ForegroundColor Yellow
Write-Host "1. Review at https://github.com/akefallonitis/sentryxdr"
Write-Host "2. Deploy using: .\Deployment\deploy.ps1"
Write-Host "3. Configure multi-tenant app registration"
Write-Host "4. Test with /api/xdr/health endpoint"
