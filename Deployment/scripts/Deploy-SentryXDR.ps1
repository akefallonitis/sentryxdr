# Deploy-SentryXDR.ps1
# One-click deployment script for SentryXDR
# Prerequisites: Run Setup-SentryXDR-Permissions.ps1 first

#Requires -Modules Az.Accounts, Az.Resources

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$ResourceGroupName = "sentryxdr-rg",
    
    [Parameter(Mandatory = $false)]
    [string]$Location = "eastus",
    
    [Parameter(Mandatory = $false)]
    [string]$ProjectName = "sentryxdr",
    
    [Parameter(Mandatory = $false)]
    [string]$Environment = "prod",
    
    [Parameter(Mandatory = $false)]
    [string]$MultiTenantAppId,
    
    [Parameter(Mandatory = $false)]
    [securestring]$MultiTenantAppSecret,
    
    [Parameter(Mandatory = $false)]
    [switch]$DeployHybridWorker,
    
    [Parameter(Mandatory = $false)]
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "SentryXDR - One-Click Deployment" -ForegroundColor Cyan
Write-Host "Microsoft Security XDR Orchestration Platform" -ForegroundColor Cyan
Write-Host "================================================`n" -ForegroundColor Cyan

# Step 1: Check prerequisites
Write-Host "[STEP] Checking prerequisites..." -ForegroundColor Yellow

# Load deployment params if they exist
$paramsFile = Join-Path $PSScriptRoot "deployment-params.json"
if (Test-Path $paramsFile) {
    Write-Host "[OK] Found deployment-params.json" -ForegroundColor Green
    $params = Get-Content $paramsFile | ConvertFrom-Json
    
    if (-not $MultiTenantAppId) {
        $MultiTenantAppId = $params.multiTenantAppId
    }
    if (-not $MultiTenantAppSecret) {
        $MultiTenantAppSecret = ConvertTo-SecureString -String $params.multiTenantAppSecret -AsPlainText -Force
    }
}

# Validate required parameters
if (-not $MultiTenantAppId) {
    Write-Error "Missing MultiTenantAppId. Run Setup-SentryXDR-Permissions.ps1 first."
}
if (-not $MultiTenantAppSecret) {
    Write-Error "Missing MultiTenantAppSecret. Run Setup-SentryXDR-Permissions.ps1 first."
}

# Step 2: Connect to Azure
Write-Host "`n[STEP] Connecting to Azure..." -ForegroundColor Yellow
try {
    $context = Get-AzContext -ErrorAction SilentlyContinue
    if (-not $context) {
        Connect-AzAccount
    }
    Write-Host "[OK] Connected to Azure subscription: $($context.Subscription.Name)" -ForegroundColor Green
}
catch {
    Write-Error "Failed to connect to Azure: $_"
}

# Step 3: Create Resource Group
Write-Host "`n[STEP] Creating resource group..." -ForegroundColor Yellow

if ($WhatIf) {
    Write-Host "[WHATIF] Would create resource group: $ResourceGroupName in $Location" -ForegroundColor Cyan
}
else {
    $rg = Get-AzResourceGroup -Name $ResourceGroupName -ErrorAction SilentlyContinue
    if (-not $rg) {
        New-AzResourceGroup -Name $ResourceGroupName -Location $Location -Tag @{
            Project = "SentryXDR"
            Environment = $Environment
            CreatedBy = "Deployment Script"
        } | Out-Null
        Write-Host "[OK] Created resource group: $ResourceGroupName" -ForegroundColor Green
    }
    else {
        Write-Host "[INFO] Resource group already exists: $ResourceGroupName" -ForegroundColor Cyan
    }
}

# Step 4: Deploy ARM Template
Write-Host "`n[STEP] Deploying ARM template..." -ForegroundColor Yellow

$templateFile = Join-Path (Split-Path $PSScriptRoot -Parent) "azuredeploy.json"

if (-not (Test-Path $templateFile)) {
    Write-Error "ARM template not found: $templateFile"
}

$deploymentParams = @{
    projectName = $ProjectName
    location = $Location
    environment = $Environment
    multiTenantAppId = $MultiTenantAppId
    multiTenantAppSecret = $MultiTenantAppSecret
    deployHybridWorker = $DeployHybridWorker.IsPresent
}

Write-Host "[INFO] Deployment parameters:" -ForegroundColor Cyan
Write-Host "  Project Name: $ProjectName" -ForegroundColor White
Write-Host "  Location: $Location" -ForegroundColor White
Write-Host "  Environment: $Environment" -ForegroundColor White
Write-Host "  Deploy Hybrid Worker: $($DeployHybridWorker.IsPresent)" -ForegroundColor White
Write-Host ""

if ($WhatIf) {
    Write-Host "[WHATIF] Would deploy ARM template with above parameters" -ForegroundColor Cyan
    Test-AzResourceGroupDeployment -ResourceGroupName $ResourceGroupName `
        -TemplateFile $templateFile `
        -TemplateParameterObject $deploymentParams `
        -Verbose
    exit 0
}

try {
    $deployment = New-AzResourceGroupDeployment `
        -Name "SentryXDR-Deployment-$(Get-Date -Format 'yyyyMMddHHmmss')" `
        -ResourceGroupName $ResourceGroupName `
        -TemplateFile $templateFile `
        -TemplateParameterObject $deploymentParams `
        -Verbose
    
    Write-Host "[OK] Deployment completed successfully!" -ForegroundColor Green
}
catch {
    Write-Error "Deployment failed: $_"
}

# Step 5: Configure RBAC Permissions
Write-Host "`n[STEP] Configuring RBAC permissions..." -ForegroundColor Yellow

$functionAppName = $deployment.Outputs.functionAppName.Value
$functionAppPrincipalId = $deployment.Outputs.functionAppPrincipalId.Value
$keyVaultName = $deployment.Outputs.keyVaultName.Value

# Grant Function App access to Key Vault
Write-Host "[INFO] Granting Function App access to Key Vault..." -ForegroundColor Cyan
New-AzRoleAssignment `
    -ObjectId $functionAppPrincipalId `
    -RoleDefinitionName "Key Vault Secrets User" `
    -Scope "/subscriptions/$((Get-AzContext).Subscription.Id)/resourceGroups/$ResourceGroupName/providers/Microsoft.KeyVault/vaults/$keyVaultName" `
    -ErrorAction SilentlyContinue | Out-Null

Write-Host "[OK] Function App has Key Vault access" -ForegroundColor Green

# Grant Function App Contributor role for Azure operations
Write-Host "[INFO] Granting Function App Contributor role..." -ForegroundColor Cyan
New-AzRoleAssignment `
    -ObjectId $functionAppPrincipalId `
    -RoleDefinitionName "Contributor" `
    -Scope "/subscriptions/$((Get-AzContext).Subscription.Id)/resourceGroups/$ResourceGroupName" `
    -ErrorAction SilentlyContinue | Out-Null

Write-Host "[OK] Function App has Contributor access" -ForegroundColor Green

# Step 6: Output Summary
Write-Host "`n================================================" -ForegroundColor Cyan
Write-Host "Deployment Complete!" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Function App Name: " -NoNewline
Write-Host $functionAppName -ForegroundColor Green
Write-Host "Function App URL: " -NoNewline
Write-Host $deployment.Outputs.functionAppUrl.Value -ForegroundColor Green
Write-Host "Key Vault Name: " -NoNewline
Write-Host $keyVaultName -ForegroundColor Green
Write-Host ""
Write-Host "Storage Accounts:" -ForegroundColor Yellow
Write-Host "  Primary: $($deployment.Outputs.storageAccountName.Value)" -ForegroundColor White
Write-Host "  Forensics: $($deployment.Outputs.forensicsStorageAccountName.Value)" -ForegroundColor White
Write-Host ""

if ($DeployHybridWorker) {
    Write-Host "Automation Account: $($deployment.Outputs.automationAccountName.Value)" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Next Steps for Hybrid Worker:" -ForegroundColor Yellow
    Write-Host "1. Install Hybrid Worker on your on-premise server" -ForegroundColor White
    Write-Host "2. Register the worker with the Automation Account" -ForegroundColor White
    Write-Host "3. Test runbooks: Disable-OnPremUser, etc." -ForegroundColor White
    Write-Host ""
}

Write-Host "Next Steps:" -ForegroundColor Yellow
Write-Host "1. Test the API endpoint: $($deployment.Outputs.functionAppUrl.Value)/api/xdr/health" -ForegroundColor White
Write-Host "2. Deploy your function code (or use CI/CD)" -ForegroundColor White
Write-Host "3. Configure monitoring alerts in Application Insights" -ForegroundColor White
Write-Host ""

Write-Host "Deployment completed successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "API Endpoints:" -ForegroundColor Yellow
Write-Host "  POST $($deployment.Outputs.functionAppUrl.Value)/api/v1/remediation/submit" -ForegroundColor White
Write-Host "  GET  $($deployment.Outputs.functionAppUrl.Value)/api/v1/remediation/{id}/status" -ForegroundColor White
Write-Host "  POST $($deployment.Outputs.functionAppUrl.Value)/api/v1/remediation/batch" -ForegroundColor White
Write-Host ""
