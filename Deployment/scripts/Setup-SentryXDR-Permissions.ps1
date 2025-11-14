# Setup-SentryXDR-Permissions.ps1
# Configures Azure AD App Registration with LEAST PRIVILEGE permissions for SentryXDR
# Supports both NEW and EXISTING app registrations

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
Write-Host "Least Privilege Permission Configuration" -ForegroundColor Cyan
Write-Host "================================================`n" -ForegroundColor Cyan

# Required Microsoft Graph Permissions (LEAST PRIVILEGE)
$requiredPermissions = @{
    "Microsoft Graph" = @{
        Application = @(
            # MDE - Endpoint Protection
            "SecurityEvents.Read.All",
            "Machine.Read.All",
            "Machine.ReadWrite.All",
            
            # MDO - Email Security
            "Mail.Read",
            "Mail.ReadWrite",
            "ThreatSubmission.ReadWrite.All",
            "MailboxSettings.ReadWrite",        # For mail forwarding control
            
            # Entra ID - Identity Protection
            "User.Read.All",
            "User.ReadWrite.All",
            "UserAuthenticationMethod.ReadWrite.All",
            "Policy.ReadWrite.ConditionalAccess",
            "RoleManagement.ReadWrite.Directory",
            
            # Intune - Device Management
            "DeviceManagementManagedDevices.ReadWrite.All",
            "DeviceManagementConfiguration.ReadWrite.All",
            
            # MCAS - Cloud App Security
            "CloudApp-Discovery.Read.All",
            
            # DLP - Data Loss Prevention
            "InformationProtectionPolicy.Read",
            
            # Azure - Infrastructure
            "Directory.Read.All",
            
            # Incident Management
            "SecurityIncident.Read.All",
            "SecurityIncident.ReadWrite.All",
            "SecurityAlert.ReadWrite.All"
        )
    }
}

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

# Step 2: Find or create app registration
Write-Step "Finding app registration..."

if ($ExistingAppId) {
    Write-Info "Looking for existing app by ID: $ExistingAppId"
    $app = Get-MgApplication -Filter "appId eq '$ExistingAppId'" -ErrorAction SilentlyContinue
    
    if (-not $app) {
        Write-Error "App with ID $ExistingAppId not found!"
        exit 1
    }
    
    Write-Success "Found existing app: $($app.DisplayName) ($ExistingAppId)"
    $appId = $app.AppId
}
else {
    $app = Get-MgApplication -Filter "displayName eq '$AppName'" -ErrorAction SilentlyContinue
    
    if ($app) {
        Write-Warning "App registration already exists: $($app.AppId)"
        Write-Host "Use -ExistingAppId parameter to update this app, or use -CleanupExisting to remove it first" -ForegroundColor Yellow
        
        if ($CleanupExisting) {
            Write-Step "Removing existing app registration..."
            if ($WhatIf) {
                Write-Info "Would remove existing app: $($app.AppId)"
            }
            else {
                Remove-MgApplication -ApplicationId $app.Id
                Write-Success "Removed existing app"
                $app = $null
            }
        }
        else {
            $appId = $app.AppId
        }
    }
    
    if (-not $app -and -not $WhatIf) {
        Write-Step "Creating new app registration: $AppName..."
        $appParams = @{
            DisplayName = $AppName
            SignInAudience = "AzureADMultipleOrgs"
            Description = "SentryXDR - Microsoft Security XDR Orchestration Platform (Multi-Tenant)"
        }
        
        $app = New-MgApplication @appParams
        $appId = $app.AppId
        Write-Success "Created app registration: $appId"
    }
}

# Step 3: Clean up existing permissions (if requested)
if ($CleanupExisting -and $app) {
    Write-Step "Cleaning up existing permissions..."
    if ($WhatIf) {
        Write-Info "Would clean up existing permissions"
    }
    else {
        Update-MgApplication -ApplicationId $app.Id -RequiredResourceAccess @()
        Write-Success "Cleared existing permissions"
        Start-Sleep -Seconds 2
    }
}

# Step 4: Create Client Secret (if not skipped)
if (-not $SkipSecretGeneration -and $app) {
    Write-Step "Creating client secret..."
    $secretDescription = "SentryXDR Secret (Created $(Get-Date -Format 'yyyy-MM-dd'))"
    $secretEnd = (Get-Date).AddYears(2)

    if ($WhatIf) {
        Write-Info "Would create client secret valid until $secretEnd"
    }
    else {
        $secret = Add-MgApplicationPassword -ApplicationId $app.Id -PasswordCredential @{
            DisplayName = $secretDescription
            EndDateTime = $secretEnd
        }
        
        Write-Success "Created client secret (expires: $secretEnd)"
        Write-Host ""
        Write-Host "===============================================" -ForegroundColor Red
        Write-Host "IMPORTANT: Save these credentials securely!" -ForegroundColor Red
        Write-Host "===============================================" -ForegroundColor Red
        Write-Host "Application (Client) ID: " -NoNewline
        Write-Host $appId -ForegroundColor Green
        Write-Host "Client Secret: " -NoNewline
        Write-Host $secret.SecretText -ForegroundColor Green
        Write-Host "===============================================`n" -ForegroundColor Red
    }
}

# Step 5: Configure API Permissions
Write-Step "Configuring Microsoft Graph API permissions..."

$graphServicePrincipal = Get-MgServicePrincipal -Filter "displayName eq 'Microsoft Graph'" -ErrorAction Stop

$requiredResourceAccess = @()

foreach ($permission in $requiredPermissions["Microsoft Graph"].Application) {
    $appRole = $graphServicePrincipal.AppRoles | Where-Object { $_.Value -eq $permission }
    
    if ($appRole) {
        Write-Info "Adding permission: $permission"
        $requiredResourceAccess += @{
            Id = $appRole.Id
            Type = "Role"
        }
    }
    else {
        Write-Warning "Permission not found: $permission (may require manual configuration)"
    }
}

if ($WhatIf) {
    Write-Info "Would configure $($requiredResourceAccess.Count) API permissions"
}
else {
    Update-MgApplication -ApplicationId $app.Id -RequiredResourceAccess @{
        ResourceAppId = "00000003-0000-0000-c000-000000000000"  # Microsoft Graph
        ResourceAccess = $requiredResourceAccess
    }
    Write-Success "Configured $($requiredResourceAccess.Count) API permissions"
}

# Step 6: Grant Admin Consent
Write-Step "Admin consent required..."
Write-Warning "You must grant admin consent for the configured permissions"
Write-Host ""
Write-Host "Please navigate to:" -ForegroundColor Cyan
Write-Host "https://portal.azure.com/#blade/Microsoft_AAD_RegisteredApps/ApplicationMenuBlade/CallAnAPI/appId/$appId" -ForegroundColor Yellow
Write-Host ""
Write-Host "Then click: 'Grant admin consent for <Your Tenant>'" -ForegroundColor Yellow
Write-Host ""

# Step 7: Output deployment parameters
Write-Step "Generating deployment parameters..."

$deploymentParams = @{
    multiTenantAppId = $appId
    multiTenantAppSecret = if ($secret) { $secret.SecretText } else { "<USE_EXISTING_SECRET_OR_CREATE_NEW>" }
}

$outputPath = Join-Path $PSScriptRoot "deployment-params.json"

if ($WhatIf) {
    Write-Info "Would save deployment parameters to: $outputPath"
}
else {
    $deploymentParams | ConvertTo-Json -Depth 10 | Out-File -FilePath $outputPath -Encoding UTF8
    Write-Success "Saved deployment parameters to: $outputPath"
}

# Step 8: Summary
Write-Host "`n================================================" -ForegroundColor Cyan
Write-Host "Setup Complete!" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Application ID: $appId" -ForegroundColor Green
Write-Host "Permissions Configured: $($requiredResourceAccess.Count)" -ForegroundColor Green
Write-Host "Deployment params saved: $outputPath" -ForegroundColor Green
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Yellow
Write-Host "1. Grant admin consent in Azure Portal" -ForegroundColor White
Write-Host "2. Wait 5-10 minutes for permissions to propagate" -ForegroundColor White
Write-Host "3. Run: ./Deploy-SentryXDR.ps1" -ForegroundColor White
Write-Host ""

# Disconnect
Disconnect-MgGraph | Out-Null

Write-Host "Setup script completed successfully!" -ForegroundColor Green
