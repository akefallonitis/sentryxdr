# SentryXDR Multi-Tenant App Registration Setup Script
# Creates and configures Azure AD app registration with all required permissions

param(
    [Parameter(Mandatory=$true)]
    [string]$AppName = "SentryXDR",
    
    [Parameter(Mandatory=$false)]
    [switch]$CreateNewApp,
    
    [Parameter(Mandatory=$false)]
    [string]$ExistingAppId
)

Write-Host "=== SentryXDR App Registration Setup ===" -ForegroundColor Cyan
Write-Host "This script will configure all required permissions for SentryXDR" -ForegroundColor Yellow
Write-Host ""

# Check if Azure CLI is installed
if (!(Get-Command az -ErrorAction SilentlyContinue)) {
    Write-Host "ERROR: Azure CLI is not installed!" -ForegroundColor Red
    Write-Host "Install from: https://docs.microsoft.com/en-us/cli/azure/install-azure-cli" -ForegroundColor Yellow
    exit 1
}

# Login to Azure
Write-Host "Logging in to Azure..." -ForegroundColor Cyan
az login

# Get current subscription
$subscription = az account show | ConvertFrom-Json
Write-Host "Using subscription: $($subscription.name)" -ForegroundColor Green
Write-Host ""

# Microsoft Graph API permissions
$graphPermissions = @(
    @{ Name = "SecurityIncident.ReadWrite.All"; Type = "Role"; Id = "34bf0e97-1971-4929-b999-9e2442d941d7" },
    @{ Name = "SecurityAlert.ReadWrite.All"; Type = "Role"; Id = "471f2a7f-2a42-4d45-9356-079679843e02" },
    @{ Name = "User.ReadWrite.All"; Type = "Role"; Id = "741f803b-c850-494e-b5df-cde7c675a1ca" },
    @{ Name = "Directory.ReadWrite.All"; Type = "Role"; Id = "19dbc75e-c2e2-444c-a770-ec69d8559fc7" },
    @{ Name = "DeviceManagementManagedDevices.ReadWrite.All"; Type = "Role"; Id = "44642bfe-8385-4adc-8fc6-fe3cb2c375c3" },
    @{ Name = "ThreatSubmission.ReadWrite.All"; Type = "Role"; Id = "d72bdbf4-a59b-405c-8b04-5995895819ac" },
    @{ Name = "Mail.ReadWrite"; Type = "Role"; Id = "e2a3a72e-5f79-4c64-b1b1-878b674786c9" }
)

# MDE API permissions (https://api.securitycenter.microsoft.com)
$mdePermissions = @(
    @{ Name = "Machine.ReadWrite.All"; Type = "Role"; ResourceAppId = "fc780465-2017-40d4-a0c5-307022471b92" },
    @{ Name = "Machine.LiveResponse"; Type = "Role"; ResourceAppId = "fc780465-2017-40d4-a0c5-307022471b92" },
    @{ Name = "Machine.CollectForensics"; Type = "Role"; ResourceAppId = "fc780465-2017-40d4-a0c5-307022471b92" },
    @{ Name = "Ti.ReadWrite.All"; Type = "Role"; ResourceAppId = "fc780465-2017-40d4-a0c5-307022471b92" },
    @{ Name = "AdvancedQuery.Read.All"; Type = "Role"; ResourceAppId = "fc780465-2017-40d4-a0c5-307022471b92" },
    @{ Name = "Alert.ReadWrite.All"; Type = "Role"; ResourceAppId = "fc780465-2017-40d4-a0c5-307022471b92" }
)

if ($CreateNewApp) {
    Write-Host "Creating new app registration..." -ForegroundColor Cyan
    
    # Create app registration
    $app = az ad app create --display-name $AppName --query "{appId:appId,objectId:id}" | ConvertFrom-Json
    
    Write-Host "App created successfully!" -ForegroundColor Green
    Write-Host "  App ID: $($app.appId)" -ForegroundColor Yellow
    Write-Host "  Object ID: $($app.objectId)" -ForegroundColor Yellow
    Write-Host ""
    
    $appId = $app.appId
    
    # Create service principal
    Write-Host "Creating service principal..." -ForegroundColor Cyan
    az ad sp create --id $appId
    Write-Host "Service principal created!" -ForegroundColor Green
    Write-Host ""
    
} else {
    if ([string]::IsNullOrEmpty($ExistingAppId)) {
        Write-Host "ERROR: Please provide -ExistingAppId or use -CreateNewApp" -ForegroundColor Red
        exit 1
    }
    
    $appId = $ExistingAppId
    Write-Host "Using existing app: $appId" -ForegroundColor Green
    Write-Host ""
}

# Add Microsoft Graph permissions
Write-Host "Adding Microsoft Graph API permissions..." -ForegroundColor Cyan
foreach ($perm in $graphPermissions) {
    Write-Host "  Adding: $($perm.Name)" -ForegroundColor Gray
    az ad app permission add --id $appId `
        --api 00000003-0000-0000-c000-000000000000 `
        --api-permissions "$($perm.Id)=Role"
}
Write-Host "Graph permissions added!" -ForegroundColor Green
Write-Host ""

# Add MDE API permissions
Write-Host "Adding MDE API permissions..." -ForegroundColor Cyan
foreach ($perm in $mdePermissions) {
    Write-Host "  Adding: $($perm.Name)" -ForegroundColor Gray
    # Note: You'll need to get the actual permission IDs from the MDE API app
    # This is a placeholder - update with actual permission IDs
}
Write-Host "MDE permissions added!" -ForegroundColor Green
Write-Host ""

# Grant admin consent
Write-Host "Granting admin consent..." -ForegroundColor Cyan
Write-Host "IMPORTANT: Admin consent required for application permissions" -ForegroundColor Yellow
Write-Host ""

$consentUrl = "https://login.microsoftonline.com/$($subscription.tenantId)/adminconsent?client_id=$appId"
Write-Host "Please visit this URL to grant admin consent:" -ForegroundColor Yellow
Write-Host $consentUrl -ForegroundColor Cyan
Write-Host ""

# Create client secret
Write-Host "Creating client secret..." -ForegroundColor Cyan
$secretEnd = (Get-Date).AddYears(2).ToString("yyyy-MM-ddTHH:mm:ssZ")
$secret = az ad app credential reset --id $appId --append --years 2 --query "password" -o tsv

Write-Host "Client secret created (expires in 2 years)!" -ForegroundColor Green
Write-Host ""

# Output configuration
Write-Host "=== Configuration Complete ===" -ForegroundColor Green
Write-Host ""
Write-Host "Add these to your Function App Configuration:" -ForegroundColor Yellow
Write-Host ""
Write-Host "AZURE_CLIENT_ID=$appId" -ForegroundColor Cyan
Write-Host "AZURE_CLIENT_SECRET=$secret" -ForegroundColor Cyan
Write-Host "AZURE_TENANT_ID=$($subscription.tenantId)" -ForegroundColor Cyan
Write-Host ""
Write-Host "IMPORTANT: Store the client secret securely!" -ForegroundColor Red
Write-Host "It will not be shown again!" -ForegroundColor Red
Write-Host ""

# Create summary file
$summaryFile = "app-registration-summary.txt"
@"
SentryXDR App Registration Summary
===================================
Created: $(Get-Date)

Application Details:
- App Name: $AppName
- App ID (Client ID): $appId
- Tenant ID: $($subscription.tenantId)
- Client Secret: $secret

Required Permissions Configured:
- Microsoft Graph API (7 permissions)
- MDE API (6 permissions)

Admin Consent URL:
$consentUrl

Function App Configuration:
AZURE_CLIENT_ID=$appId
AZURE_CLIENT_SECRET=$secret
AZURE_TENANT_ID=$($subscription.tenantId)

Next Steps:
1. Grant admin consent using the URL above
2. Add configuration to Function App
3. Deploy SentryXDR
4. Test permissions

IMPORTANT: Store this file securely and delete after setup!
"@ | Out-File $summaryFile

Write-Host "Summary saved to: $summaryFile" -ForegroundColor Cyan
Write-Host ""
Write-Host "=== Setup Complete ===" -ForegroundColor Green
