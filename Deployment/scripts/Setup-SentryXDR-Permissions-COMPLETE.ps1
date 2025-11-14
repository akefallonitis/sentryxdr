# Setup-SentryXDR-Permissions.ps1
# Configures Azure AD App Registration with COMPLETE permissions for SentryXDR
# Supports both NEW and EXISTING app registrations
# Updated with ALL required permissions for 150 actions

#Requires -Modules Az.Accounts, Az.Resources, Microsoft.Graph

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$AppName = "SentryXDR-MultiTenant",
    
    [Parameter(Mandatory = $false)]
    [string]$ExistingAppId,
    
    [Parameter(Mandatory = $false)]
    [switch]$CleanupExisting,
    
    [Parameter(Mandatory = $false)]
    [switch]$SkipSecretGeneration,
    
    [Parameter(Mandatory = $false)]
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "SentryXDR - App Registration Setup Script" -ForegroundColor Cyan
Write-Host "Complete Permission Configuration (150 Actions)" -ForegroundColor Cyan
Write-Host "================================================`n" -ForegroundColor Cyan

# COMPLETE Microsoft Graph & Azure Permissions
$requiredPermissions = @{
    "Microsoft Graph" = @{
        Application = @(
            # ============================================================
            # MDE - Microsoft Defender for Endpoint (24 actions)
            # ============================================================
            "SecurityEvents.Read.All",              # Read security events
            "Machine.Read.All",                     # Read device information
            "Machine.ReadWrite.All",                # Device isolation, actions
            "SecurityAction.Read.All",              # Read security actions
            "SecurityAction.ReadWrite.All",         # Execute security actions
            "ThreatIndicators.ReadWrite.OwnedBy",   # IOC management
            
            # ============================================================
            # MDO - Microsoft Defender for Office 365 (18 actions)
            # ============================================================
            "Mail.Read",                            # Read email
            "Mail.ReadWrite",                       # Email remediation
            "Mail.Send",                            # Email actions
            "MailboxSettings.Read",                 # Read mailbox settings
            "MailboxSettings.ReadWrite",            # Mail forwarding control
            "ThreatSubmission.ReadWrite.All",       # Threat submission
            "ThreatAssessment.ReadWrite.All",       # Threat assessment
            "SecurityEvents.ReadWrite.All",         # Email security events
            
            # ============================================================
            # Entra ID - Identity & Access (18 actions)
            # ============================================================
            "User.Read.All",                        # Read users
            "User.ReadWrite.All",                   # User management
            "User.EnableDisableAccount.All",        # Disable users
            "User.ManageIdentities.All",            # Identity management
            "UserAuthenticationMethod.Read.All",    # Read auth methods
            "UserAuthenticationMethod.ReadWrite.All", # MFA reset
            "Directory.Read.All",                   # Read directory
            "Directory.ReadWrite.All",              # Directory operations
            "Policy.Read.All",                      # Read policies
            "Policy.ReadWrite.ConditionalAccess",   # Conditional Access
            "Policy.ReadWrite.AuthenticationMethod", # Auth method policies
            "RoleManagement.Read.Directory",        # Read roles
            "RoleManagement.ReadWrite.Directory",   # Role management
            "IdentityRiskyUser.Read.All",           # Read risky users
            "IdentityRiskyUser.ReadWrite.All",      # Manage risky users
            "IdentityRiskEvent.Read.All",           # Read risk events
            "IdentityUserFlow.ReadWrite.All",       # User flows
            
            # ============================================================
            # Intune - Device Management (15 actions)
            # ============================================================
            "DeviceManagementManagedDevices.Read.All",       # Read devices
            "DeviceManagementManagedDevices.ReadWrite.All",  # Device actions
            "DeviceManagementManagedDevices.PrivilegedOperations.All", # Wipe/retire
            "DeviceManagementConfiguration.Read.All",        # Read config
            "DeviceManagementConfiguration.ReadWrite.All",   # Write config
            "DeviceManagementApps.Read.All",                 # Read apps
            "DeviceManagementApps.ReadWrite.All",            # Manage apps
            "DeviceManagementServiceConfig.Read.All",        # Service config
            "DeviceManagementServiceConfig.ReadWrite.All",   # Write service config
            
            # ============================================================
            # MCAS - Cloud App Security (12 actions)
            # ============================================================
            "CloudApp-Discovery.Read.All",          # Cloud discovery
            "CloudPC.ReadWrite.All",                # Cloud PC management
            
            # ============================================================
            # DLP - Data Loss Prevention (5 actions)
            # ============================================================
            "InformationProtectionPolicy.Read",     # Read DLP policies
            "Files.Read.All",                       # Read files
            "Files.ReadWrite.All",                  # File quarantine
            "Sites.Read.All",                       # Read SharePoint
            "Sites.ReadWrite.All",                  # SharePoint security
            
            # ============================================================
            # Incident Management (18 actions)
            # ============================================================
            "SecurityIncident.Read.All",            # Read incidents
            "SecurityIncident.ReadWrite.All",       # Manage incidents
            "SecurityAlert.Read.All",               # Read alerts
            "SecurityAlert.ReadWrite.All",          # Manage alerts
            "ThreatHunting.Read.All",               # Advanced hunting
            
            # ============================================================
            # Azure AD (General)
            # ============================================================
            "Application.Read.All",                 # Read applications
            "Group.Read.All",                       # Read groups
            "Group.ReadWrite.All",                  # Manage groups
            "AuditLog.Read.All",                    # Read audit logs
            "Reports.Read.All",                     # Read reports
            
            # ============================================================
            # Additional for v2.0
            # ============================================================
            "IdentityProtection.Read.All",          # Identity protection
            "IdentityProtection.ReadWrite.All",     # Write identity protection
            "AccessReview.Read.All",                # Access reviews
            "AccessReview.ReadWrite.All",           # Manage access reviews
            "EntitlementManagement.Read.All",       # Entitlement management
            "EntitlementManagement.ReadWrite.All"   # Write entitlement
        )
    },
    "Windows Defender ATP" = @{
        Application = @(
            # MDE API (separate from Graph)
            "AdvancedQuery.Read.All",               # Advanced hunting
            "Alert.Read.All",                       # Read alerts
            "Alert.ReadWrite.All",                  # Manage alerts
            "Machine.Read.All",                     # Read machines
            "Machine.ReadWrite.All",                # Machine actions
            "Machine.Isolate",                      # Isolate machines
            "Machine.RestrictExecution",            # Restrict execution
            "Machine.Scan",                         # Scan machines
            "Machine.StopAndQuarantine",            # Stop and quarantine
            "Machine.LiveResponse",                 # Live response
            "Machine.CollectForensics",             # Collect forensics
            "Ti.ReadWrite.All",                     # Threat intelligence
            "Vulnerability.Read.All",               # Vulnerability mgmt
            "Score.Read.All",                       # Secure score
            "RemediationTasks.Read.All",            # Remediation tasks
            "Software.Read.All"                     # Software inventory
        )
    }
}

# Azure RBAC Roles (for Azure resource management - 25 actions)
$requiredAzureRoles = @(
    @{
        Name = "Contributor"
        Scope = "Subscription"
        Purpose = "Manage Azure resources (VMs, NSGs, Firewalls, Storage)"
    },
    @{
        Name = "Security Admin"
        Scope = "Subscription"
        Purpose = "Manage security policies and configurations"
    },
    @{
        Name = "Network Contributor"
        Scope = "Subscription"
        Purpose = "Manage network security (NSGs, Firewalls)"
    }
)

function Write-Step {
    param([string]$Message)
    Write-Host "`n[STEP] $Message" -ForegroundColor Yellow
}

function Write-Success {
    param([string]$Message)
    Write-Host "[OK] $Message" -ForegroundColor Green
}

function Write-Info {
    param([string]$Message)
    Write-Host "[INFO] $Message" -ForegroundColor Cyan
}

function Write-Warning {
    param([string]$Message)
    Write-Host "[WARN] $Message" -ForegroundColor Magenta
}

function Write-Error {
    param([string]$Message)
    Write-Host "[ERROR] $Message" -ForegroundColor Red
}

# Step 1: Connect to Microsoft Graph
Write-Step "Connecting to Microsoft Graph..."
try {
    Connect-MgGraph -Scopes "Application.ReadWrite.All", "AppRoleAssignment.ReadWrite.All", "Directory.Read.All" -ErrorAction Stop
    Write-Success "Connected to Microsoft Graph"
}
catch {
    Write-Error "Failed to connect to Microsoft Graph: $_"
    exit 1
}

# Step 2: Connect to Azure
Write-Step "Connecting to Azure..."
try {
    Connect-AzAccount -ErrorAction Stop | Out-Null
    Write-Success "Connected to Azure"
}
catch {
    Write-Error "Failed to connect to Azure: $_"
    exit 1
}

# Step 3: Get or Create App Registration
if ($ExistingAppId) {
    Write-Step "Using existing app registration: $ExistingAppId"
    try {
        $app = Get-MgApplication -ApplicationId $ExistingAppId -ErrorAction Stop
        Write-Success "Found existing app: $($app.DisplayName)"
        
        if ($CleanupExisting) {
            Write-Warning "Cleaning up existing permissions..."
            # Remove old API permissions
            Update-MgApplication -ApplicationId $app.Id -RequiredResourceAccess @() -ErrorAction Stop
            Write-Success "Cleaned up existing permissions"
        }
    }
    catch {
        Write-Error "Failed to get existing app: $_"
        exit 1
    }
}
else {
    Write-Step "Creating new app registration: $AppName"
    try {
        $app = New-MgApplication -DisplayName $AppName -SignInAudience "AzureADMultipleOrgs" -ErrorAction Stop
        Write-Success "Created app: $($app.DisplayName) (ID: $($app.AppId))"
    }
    catch {
        Write-Error "Failed to create app: $_"
        exit 1
    }
}

# Step 4: Add Required API Permissions
Write-Step "Adding required API permissions..."

$resourceAccessList = @()

# Microsoft Graph permissions
$graphServicePrincipal = Get-MgServicePrincipal -Filter "displayName eq 'Microsoft Graph'" -Top 1
$graphResourceAccess = @()

foreach ($permission in $requiredPermissions["Microsoft Graph"].Application) {
    $appRole = $graphServicePrincipal.AppRoles | Where-Object { $_.Value -eq $permission }
    if ($appRole) {
        $graphResourceAccess += @{
            Id = $appRole.Id
            Type = "Role"
        }
        Write-Info "Added: $permission"
    }
    else {
        Write-Warning "Permission not found: $permission"
    }
}

$resourceAccessList += @{
    ResourceAppId = $graphServicePrincipal.AppId
    ResourceAccess = $graphResourceAccess
}

# Windows Defender ATP permissions
try {
    $mdeServicePrincipal = Get-MgServicePrincipal -Filter "displayName eq 'WindowsDefenderATP'" -Top 1
    if ($mdeServicePrincipal) {
        $mdeResourceAccess = @()
        
        foreach ($permission in $requiredPermissions["Windows Defender ATP"].Application) {
            $appRole = $mdeServicePrincipal.AppRoles | Where-Object { $_.Value -eq $permission }
            if ($appRole) {
                $mdeResourceAccess += @{
                    Id = $appRole.Id
                    Type = "Role"
                }
                Write-Info "Added MDE: $permission"
            }
            else {
                Write-Warning "MDE permission not found: $permission"
            }
        }
        
        $resourceAccessList += @{
            ResourceAppId = $mdeServicePrincipal.AppId
            ResourceAccess = $mdeResourceAccess
        }
    }
}
catch {
    Write-Warning "Windows Defender ATP permissions skipped (not available in tenant)"
}

# Update app with permissions
try {
    Update-MgApplication -ApplicationId $app.Id -RequiredResourceAccess $resourceAccessList -ErrorAction Stop
    Write-Success "Added all API permissions"
}
catch {
    Write-Error "Failed to add API permissions: $_"
    exit 1
}

# Step 5: Create Client Secret
if (-not $SkipSecretGeneration) {
    Write-Step "Creating client secret..."
    try {
        $secretName = "SentryXDR-Secret-$(Get-Date -Format 'yyyyMMdd')"
        $secretExpiry = (Get-Date).AddYears(2)
        
        $passwordCredential = @{
            displayName = $secretName
            endDateTime = $secretExpiry
        }
        
        $secret = Add-MgApplicationPassword -ApplicationId $app.Id -PasswordCredential $passwordCredential
        Write-Success "Created client secret (expires: $secretExpiry)"
        Write-Host "`n================================================" -ForegroundColor Green
        Write-Host "SAVE THESE VALUES - THEY WON'T BE SHOWN AGAIN!" -ForegroundColor Green
        Write-Host "================================================" -ForegroundColor Green
        Write-Host "Application (client) ID: $($app.AppId)" -ForegroundColor Yellow
        Write-Host "Client Secret: $($secret.SecretText)" -ForegroundColor Yellow
        Write-Host "Tenant ID: $((Get-MgContext).TenantId)" -ForegroundColor Yellow
        Write-Host "================================================`n" -ForegroundColor Green
    }
    catch {
        Write-Error "Failed to create client secret: $_"
        exit 1
    }
}

# Step 6: Display Admin Consent URL
Write-Step "Admin consent required for permissions"
$tenantId = (Get-MgContext).TenantId
$consentUrl = "https://login.microsoftonline.com/$tenantId/adminconsent?client_id=$($app.AppId)"
Write-Host "`nAdmin Consent URL:" -ForegroundColor Cyan
Write-Host $consentUrl -ForegroundColor Yellow
Write-Host "`nOpen this URL in a browser and grant admin consent.`n" -ForegroundColor Cyan

# Step 7: Display Azure RBAC instructions
Write-Step "Azure RBAC Role Assignments Required"
Write-Info "To complete Azure resource management capabilities, assign these roles:"
Write-Host ""
foreach ($role in $requiredAzureRoles) {
    Write-Host "  Role: $($role.Name)" -ForegroundColor Yellow
    Write-Host "  Scope: $($role.Scope)" -ForegroundColor Cyan
    Write-Host "  Purpose: $($role.Purpose)" -ForegroundColor White
    Write-Host ""
}

Write-Host "Azure CLI commands:" -ForegroundColor Cyan
Write-Host "az role assignment create --assignee $($app.AppId) --role Contributor --scope /subscriptions/<subscription-id>" -ForegroundColor Yellow
Write-Host "az role assignment create --assignee $($app.AppId) --role 'Security Admin' --scope /subscriptions/<subscription-id>" -ForegroundColor Yellow
Write-Host "az role assignment create --assignee $($app.AppId) --role 'Network Contributor' --scope /subscriptions/<subscription-id>" -ForegroundColor Yellow

# Step 8: Summary
Write-Host "`n================================================" -ForegroundColor Cyan
Write-Host "Setup Complete!" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Application ID: $($app.AppId)" -ForegroundColor Green
Write-Host "Display Name: $($app.DisplayName)" -ForegroundColor Green
Write-Host "Permissions Added: $($graphResourceAccess.Count + $mdeResourceAccess.Count)" -ForegroundColor Green
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Yellow
Write-Host "1. Grant admin consent using the URL above" -ForegroundColor White
Write-Host "2. Assign Azure RBAC roles using Azure CLI commands" -ForegroundColor White
Write-Host "3. Save the Application ID and Client Secret" -ForegroundColor White
Write-Host "4. Use these values in ARM template deployment" -ForegroundColor White
Write-Host ""
Write-Host "Deploy Command:" -ForegroundColor Cyan
Write-Host ".\Deploy-SentryXDR.ps1 -AppId '$($app.AppId)' -AppSecret '<your-secret>'" -ForegroundColor Yellow
Write-Host ""

# Save details to file
$outputFile = "SentryXDR-AppRegistration-$(Get-Date -Format 'yyyyMMddHHmmss').txt"
$output = @"
SentryXDR App Registration Details
====================================
Application ID: $($app.AppId)
Display Name: $($app.DisplayName)
$(if (-not $SkipSecretGeneration) { "Client Secret: $($secret.SecretText)" })
Tenant ID: $tenantId
Created: $(Get-Date)

Admin Consent URL:
$consentUrl

Permissions Added: $($graphResourceAccess.Count + $mdeResourceAccess.Count)

Azure RBAC Commands:
az role assignment create --assignee $($app.AppId) --role Contributor --scope /subscriptions/<subscription-id>
az role assignment create --assignee $($app.AppId) --role 'Security Admin' --scope /subscriptions/<subscription-id>
az role assignment create --assignee $($app.AppId) --role 'Network Contributor' --scope /subscriptions/<subscription-id>

Deployment Command:
.\Deploy-SentryXDR.ps1 -AppId '$($app.AppId)' -AppSecret '<your-secret>'
"@

$output | Out-File -FilePath $outputFile -Encoding UTF8
Write-Success "Details saved to: $outputFile"
