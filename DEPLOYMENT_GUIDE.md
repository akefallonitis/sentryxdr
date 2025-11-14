# ?? **SENTRYXDR - DEPLOYMENT GUIDE**

> **Complete guide for deploying SentryXDR to Azure**  
> **Status:** Production-Ready  
> **Version:** 1.0  
> **Last Updated:** 2025-01

---

## ?? **TABLE OF CONTENTS**

1. [Prerequisites](#prerequisites)
2. [Quick Start (5 Minutes)](#quick-start)
3. [Detailed Deployment Steps](#detailed-deployment)
4. [Post-Deployment Configuration](#post-deployment)
5. [Hybrid Worker Setup](#hybrid-worker-setup)
6. [Troubleshooting](#troubleshooting)

---

## ? **PREREQUISITES**

### **Required Azure Permissions**
- **Global Administrator** OR **Application Administrator** (for app registration)
- **Contributor** role on target Azure subscription
- **User Access Administrator** (for RBAC assignments)

### **Required Software**
```powershell
# Install Azure PowerShell
Install-Module -Name Az -Repository PSGallery -Force

# Install Microsoft Graph PowerShell
Install-Module -Name Microsoft.Graph -Repository PSGallery -Force

# Verify installations
Get-Module -ListAvailable Az, Microsoft.Graph
```

### **Azure Subscription Requirements**
- ? Azure Subscription with sufficient quota
- ? Ability to create:
  - Azure Functions (Premium Plan)
  - Storage Accounts (2x)
  - Key Vault
  - Application Insights
  - Automation Account (optional, for on-prem AD)

---

## ? **QUICK START (5 MINUTES)**

### **Step 1: Clone Repository**
```powershell
git clone https://github.com/akefallonitis/sentryxdr.git
cd sentryxdr
```

### **Step 2: Run Permission Setup**
```powershell
cd deployment/scripts
.\Setup-SentryXDR-Permissions.ps1
```

**What this does:**
- Creates multi-tenant Azure AD app registration
- Configures LEAST PRIVILEGE Microsoft Graph permissions
- Generates client secret (valid for 2 years)
- Saves deployment parameters

**Important:** Grant admin consent when prompted!

### **Step 3: Deploy Infrastructure**
```powershell
.\Deploy-SentryXDR.ps1 -ResourceGroupName "sentryxdr-rg" -Location "eastus"
```

**What this deploys:**
- ? Azure Functions (Premium EP1 plan)
- ? Storage Account (primary + forensics)
- ? Key Vault (with app secret)
- ? Application Insights
- ? Log Analytics Workspace
- ? Automation Account (if -DeployHybridWorker specified)

### **Step 4: Deploy Function Code**
```powershell
# Build and publish
dotnet publish --configuration Release

# Deploy to Azure
func azure functionapp publish <your-function-app-name>
```

### **Step 5: Test**
```powershell
$functionUrl = "https://<your-function-app>.azurewebsites.net"
Invoke-RestMethod -Uri "$functionUrl/api/xdr/health" -Method Get
```

**Expected Response:**
```json
{
  "status": "healthy",
  "version": "1.0",
  "workers": {
    "mde": "ready",
    "mdo": "ready",
    "entraId": "ready",
    "azure": "ready",
    "intune": "ready",
    "mcas": "ready"
  }
}
```

---

## ?? **DETAILED DEPLOYMENT STEPS**

### **Phase 1: App Registration Setup (10 minutes)**

#### **1.1: Run Setup Script**
```powershell
cd deployment/scripts
.\Setup-SentryXDR-Permissions.ps1 -AppName "SentryXDR-MultiTenant"
```

#### **1.2: Review Created Permissions**

The script configures these **LEAST PRIVILEGE** permissions:

| API | Permission | Type | Purpose |
|-----|------------|------|---------|
| Microsoft Graph | `SecurityEvents.Read.All` | Application | Read security events |
| Microsoft Graph | `Machine.ReadWrite.All` | Application | MDE device isolation |
| Microsoft Graph | `Mail.ReadWrite` | Application | MDO email remediation |
| Microsoft Graph | `User.ReadWrite.All` | Application | Entra ID user management |
| Microsoft Graph | `DeviceManagementManagedDevices.ReadWrite.All` | Application | Intune device actions |
| Microsoft Graph | `SecurityIncident.ReadWrite.All` | Application | XDR incident management |

**Total:** 15 permissions (all application-level, no delegated)

#### **1.3: Grant Admin Consent**

**Option A: Via Azure Portal**
1. Navigate to: https://portal.azure.com
2. Go to **Azure Active Directory** ? **App registrations**
3. Find **SentryXDR-MultiTenant**
4. Click **API permissions**
5. Click **Grant admin consent for [Your Tenant]**

**Option B: Via PowerShell**
```powershell
$appId = "<your-app-id>"
Connect-MgGraph -Scopes "AppRoleAssignment.ReadWrite.All"

# Grant consent for Microsoft Graph permissions
$graphSP = Get-MgServicePrincipal -Filter "displayName eq 'Microsoft Graph'"
$appSP = Get-MgServicePrincipal -Filter "appId eq '$appId'"

$graphSP.AppRoles | Where-Object { $_.Value -in @(
    'SecurityEvents.Read.All',
    'Machine.ReadWrite.All',
    'Mail.ReadWrite'
    # ... (rest of permissions)
)} | ForEach-Object {
    New-MgServicePrincipalAppRoleAssignment `
        -ServicePrincipalId $appSP.Id `
        -PrincipalId $appSP.Id `
        -AppRoleId $_.Id `
        -ResourceId $graphSP.Id
}
```

#### **1.4: Save Credentials**

The script outputs:
```
Application (Client) ID: <guid>
Client Secret: <secret>
```

**?? CRITICAL:** Save these securely! They're needed for deployment.

---

### **Phase 2: Infrastructure Deployment (15 minutes)**

#### **2.1: Prepare Parameters**

**Option A: Use generated params (recommended)**
```powershell
# deployment-params.json is auto-created by Setup script
.\Deploy-SentryXDR.ps1 -ResourceGroupName "sentryxdr-prod-rg"
```

**Option B: Manual parameters**
```powershell
$appId = "<your-app-id>"
$appSecret = ConvertTo-SecureString "<your-secret>" -AsPlainText -Force

.\Deploy-SentryXDR.ps1 `
    -ResourceGroupName "sentryxdr-prod-rg" `
    -Location "eastus" `
    -ProjectName "sentryxdr" `
    -Environment "prod" `
    -MultiTenantAppId $appId `
    -MultiTenantAppSecret $appSecret `
    -DeployHybridWorker  # Optional: for on-prem AD integration
```

#### **2.2: Monitor Deployment**

Deployment typically takes **10-15 minutes**. You'll see:

```
[STEP] Creating resource group...
[OK] Created resource group: sentryxdr-prod-rg

[STEP] Deploying ARM template...
[INFO] Deployment parameters:
  Project Name: sentryxdr
  Location: eastus
  Environment: prod
  Deploy Hybrid Worker: False

[OK] Deployment completed successfully!

[STEP] Configuring RBAC permissions...
[OK] Function App has Key Vault access
[OK] Function App has Contributor access
```

#### **2.3: Verify Deployment**

```powershell
# Check resource group
Get-AzResource -ResourceGroupName "sentryxdr-prod-rg" | Select-Object Name, ResourceType

# Expected resources:
# - Function App (Microsoft.Web/sites)
# - App Service Plan (Microsoft.Web/serverfarms)
# - Storage Account x2 (Microsoft.Storage/storageAccounts)
# - Key Vault (Microsoft.KeyVault/vaults)
# - Application Insights (Microsoft.Insights/components)
# - Log Analytics (Microsoft.OperationalInsights/workspaces)
```

---

### **Phase 3: Code Deployment (10 minutes)**

#### **3.1: Build Solution**
```powershell
cd C:\path\to\sentryxdr

# Restore and build
dotnet restore
dotnet build --configuration Release

# Publish
dotnet publish --configuration Release --output ./publish
```

#### **3.2: Deploy via Azure Functions Core Tools**

```powershell
# Install if needed
npm install -g azure-functions-core-tools@4 --unsafe-perm true

# Deploy
func azure functionapp publish <your-function-app-name>
```

**Alternative: Deploy via VS Code**
1. Open project in VS Code
2. Install Azure Functions extension
3. Right-click on project ? **Deploy to Function App...**
4. Select your function app

#### **3.3: Deploy via Azure DevOps (CI/CD)**

The included pipeline (`azure-pipelines.yml`) provides:
- ? Automated build on commit
- ? Unit test execution
- ? Code coverage reports
- ? Deployment to Dev/Prod

**Setup:**
1. Import pipeline to Azure DevOps
2. Create service connection to Azure subscription
3. Update `azureSubscription` variable
4. Commit to `develop` or `main` branch

---

### **Phase 4: Verification (5 minutes)**

#### **4.1: Test Health Endpoint**
```powershell
$functionUrl = "https://<your-function-app>.azurewebsites.net"

# Health check
$health = Invoke-RestMethod -Uri "$functionUrl/api/xdr/health" -Method Get
$health | ConvertTo-Json
```

#### **4.2: Test Remediation Submit**
```powershell
$body = @{
    tenantId = "<target-tenant-id>"
    platform = "MDE"
    action = "IsolateDevice"
    parameters = @{
        deviceId = "<device-id>"
    }
    justification = "Test isolation from deployment verification"
} | ConvertTo-Json

$response = Invoke-RestMethod `
    -Uri "$functionUrl/api/v1/remediation/submit" `
    -Method Post `
    -Body $body `
    -ContentType "application/json" `
    -Headers @{ "x-functions-key" = "<your-function-key>" }

$response
```

#### **4.3: Monitor Application Insights**
```powershell
# Get AI instrumentation key
$ai = Get-AzApplicationInsights -ResourceGroupName "sentryxdr-prod-rg"
Write-Host "Application Insights: https://portal.azure.com/#resource$($ai.Id)/overview"
```

---

## ?? **POST-DEPLOYMENT CONFIGURATION**

### **1. Configure Function Keys**

**Retrieve Function Key:**
```powershell
$functionApp = Get-AzWebApp -ResourceGroupName "sentryxdr-prod-rg" -Name "<function-app-name>"
$keys = Invoke-AzResourceAction `
    -ResourceId "$($functionApp.Id)/functions/XDRGateway" `
    -Action listKeys `
    -Force

Write-Host "Function Key: $($keys.default)"
```

### **2. Configure CORS (if needed)**

```powershell
$functionApp = Get-AzWebApp -ResourceGroupName "sentryxdr-prod-rg" -Name "<function-app-name>"

Set-AzWebApp -ResourceGroupName $functionApp.ResourceGroup -Name $functionApp.Name `
    -AllowedOrigins @("https://portal.azure.com", "https://your-custom-domain.com")
```

### **3. Configure Managed Identity Permissions**

The deployment script automatically grants:
- ? **Key Vault Secrets User** (for reading app secrets)
- ? **Contributor** (for Azure resource operations)

**Additional permissions may be needed for:**

```powershell
$functionAppPrincipalId = (Get-AzWebApp -ResourceGroupName "sentryxdr-prod-rg" -Name "<function-app-name>").Identity.PrincipalId

# Grant Reader role at subscription level (for Azure operations)
New-AzRoleAssignment `
    -ObjectId $functionAppPrincipalId `
    -RoleDefinitionName "Reader" `
    -Scope "/subscriptions/<subscription-id>"

# Grant VM Contributor (for VM isolation)
New-AzRoleAssignment `
    -ObjectId $functionAppPrincipalId `
    -RoleDefinitionName "Virtual Machine Contributor" `
    -Scope "/subscriptions/<subscription-id>"
```

---

## ?? **HYBRID WORKER SETUP (OPTIONAL)**

For **on-premise Active Directory** integration:

### **Prerequisites**
- Windows Server with:
  - PowerShell 5.1 or later
  - ActiveDirectory PowerShell module
  - Network access to Azure
  - Domain Administrator credentials (for AD operations)

### **Step 1: Deploy Automation Account**
```powershell
.\Deploy-SentryXDR.ps1 `
    -ResourceGroupName "sentryxdr-prod-rg" `
    -DeployHybridWorker
```

### **Step 2: Install Hybrid Worker**

**On your on-premise server:**

```powershell
# Download and install Hybrid Worker
$DownloadUrl = "https://aka.ms/hybridworkerdownload"
Invoke-WebRequest -Uri $DownloadUrl -OutFile "HybridWorkerSetup.msi"
Start-Process msiexec.exe -ArgumentList "/i HybridWorkerSetup.msi /quiet" -Wait

# Register Hybrid Worker
Add-HybridRunbookWorker `
    -GroupName "SentryXDR-HybridWorkers" `
    -Url "https://management.azure.com" `
    -Key "<primary-key-from-automation-account>"
```

### **Step 3: Configure Service Account**

Create a dedicated AD service account:
```powershell
New-ADUser -Name "SentryXDR-Service" `
    -SamAccountName "svc-sentryxdr" `
    -UserPrincipalName "svc-sentryxdr@yourdomain.com" `
    -AccountPassword (ConvertTo-SecureString "ComplexPassword!" -AsPlainText -Force) `
    -Enabled $true `
    -PasswordNeverExpires $true

# Grant minimal permissions
Add-ADGroupMember -Identity "Account Operators" -Members "svc-sentryxdr"
```

### **Step 4: Test Runbooks**
```powershell
# Test user disable
Start-AzAutomationRunbook `
    -ResourceGroupName "sentryxdr-prod-rg" `
    -AutomationAccountName "<automation-account-name>" `
    -Name "Disable-OnPremUser" `
    -Parameters @{
        UserPrincipalName = "test.user@yourdomain.com"
        IncidentId = "TEST-001"
    } `
    -RunOn "SentryXDR-HybridWorkers"
```

---

## ?? **TROUBLESHOOTING**

### **Common Issues**

#### **1. Permission Setup Fails**

**Error:** `Insufficient privileges to complete the operation`

**Solution:**
- Ensure you're Global Administrator or Application Administrator
- Run PowerShell as Administrator
- Disconnect and reconnect to Microsoft Graph:
  ```powershell
  Disconnect-MgGraph
  Connect-MgGraph -Scopes "Application.ReadWrite.All"
  ```

#### **2. ARM Template Deployment Fails**

**Error:** `Resource name already in use`

**Solution:**
```powershell
# Use different project name
.\Deploy-SentryXDR.ps1 -ProjectName "sentryxdr2"

# Or delete existing resources
Remove-AzResourceGroup -Name "sentryxdr-rg" -Force
```

#### **3. Function App Won't Start**

**Error:** `Function app is in stopped state`

**Solution:**
```powershell
# Check application settings
$app = Get-AzWebApp -ResourceGroupName "sentryxdr-rg" -Name "<function-app-name>"
$app.SiteConfig.AppSettings

# Restart function app
Restart-AzWebApp -ResourceGroupName "sentryxdr-rg" -Name "<function-app-name>"

# Check logs
Get-AzWebAppLogStream -ResourceGroupName "sentryxdr-rg" -Name "<function-app-name>"
```

#### **4. Authentication Errors**

**Error:** `AADSTS700016: Application not found in the directory`

**Solution:**
- Wait 5-10 minutes for permissions to propagate
- Verify admin consent was granted
- Check app registration exists:
  ```powershell
  Connect-MgGraph
  Get-MgApplication -Filter "displayName eq 'SentryXDR-MultiTenant'"
  ```

#### **5. Forensics Storage Access Denied**

**Error:** `This request is not authorized to perform this operation`

**Solution:**
```powershell
# Grant Function App access to forensics storage
$functionAppPrincipalId = (Get-AzWebApp -Name "<function-app>").Identity.PrincipalId
$storageAccount = Get-AzStorageAccount -Name "<forensics-storage-name>"

New-AzRoleAssignment `
    -ObjectId $functionAppPrincipalId `
    -RoleDefinitionName "Storage Blob Data Contributor" `
    -Scope $storageAccount.Id
```

---

## ?? **DEPLOYMENT CHECKLIST**

Use this checklist to verify deployment:

### **Pre-Deployment**
- [ ] Azure subscription with sufficient quota
- [ ] Global Administrator or Application Administrator rights
- [ ] PowerShell modules installed (Az, Microsoft.Graph)
- [ ] Repository cloned locally

### **App Registration**
- [ ] Setup script executed successfully
- [ ] App registration created
- [ ] Client secret generated and saved
- [ ] Admin consent granted
- [ ] Permissions verified in Azure Portal

### **Infrastructure**
- [ ] Resource group created
- [ ] ARM template deployed successfully
- [ ] Function App running
- [ ] Storage accounts created (primary + forensics)
- [ ] Key Vault created with secret
- [ ] Application Insights configured
- [ ] Managed Identity enabled

### **RBAC**
- [ ] Function App has Key Vault access
- [ ] Function App has Contributor role
- [ ] Storage account permissions configured

### **Code Deployment**
- [ ] Solution built successfully (zero errors)
- [ ] Function code deployed to Azure
- [ ] All worker services deployed

### **Verification**
- [ ] Health endpoint returns 200 OK
- [ ] API endpoints respond correctly
- [ ] Application Insights receiving telemetry
- [ ] Storage containers exist (forensics-*)

### **Optional: Hybrid Worker**
- [ ] Automation account deployed
- [ ] Hybrid worker installed on server
- [ ] Runbooks imported successfully
- [ ] Service account configured
- [ ] Test runbook execution successful

---

## ?? **NEXT STEPS**

After successful deployment:

1. **Configure Monitoring**
   - Set up Application Insights alerts
   - Configure Log Analytics queries
   - Enable diagnostic logs

2. **Security Hardening**
   - Enable Azure AD authentication (instead of function keys)
   - Configure private endpoints for storage
   - Enable Key Vault firewall

3. **Integration**
   - Connect to Microsoft Sentinel
   - Configure Logic Apps triggers
   - Set up Power Automate flows

4. **Testing**
   - Run integration tests
   - Perform security testing
   - Conduct load testing

5. **Documentation**
   - Document tenant-specific configurations
   - Create runbooks for common operations
   - Train security operations team

---

## ?? **SUPPORT**

For issues or questions:
- **GitHub Issues:** https://github.com/akefallonitis/sentryxdr/issues
- **Documentation:** https://github.com/akefallonitis/sentryxdr/wiki
- **Email:** support@sentryxdr.com

---

**Last Updated:** 2025-01  
**Version:** 1.0  
**Status:** Production Ready  

**?? DEPLOYMENT GUIDE COMPLETE**
