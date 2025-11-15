# ?? ONE-CLICK DEPLOYMENT TEST GUIDE

**Test your deployment RIGHT NOW** to verify all 14 tags are applied correctly.

---

## ? **STEP 1: CLICK THE DEPLOY BUTTON**

[![Deploy to Azure](https://aka.ms/deploytoazurebutton)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fakefallonitis%2Fsentryxdr%2Fmain%2FDeployment%2Fazuredeploy.json)

---

## ? **STEP 2: FILL OUT THE FORM**

**Required Parameters (Only 2!):**

| Parameter | Example Value | Where to Get It |
|-----------|---------------|-----------------|
| **Multi Tenant App Id** | `12345678-1234-1234-1234-123456789012` | Azure AD ? App Registrations ? Your App ? Application (client) ID |
| **Multi Tenant App Secret** | `abc123~xYz789` | Azure AD ? App Registrations ? Your App ? Certificates & secrets |

**Optional Parameters (Use Defaults):**

| Parameter | Default | Description |
|-----------|---------|-------------|
| Project Name | `sentryxdr` | Keep as-is |
| Location | `[resourceGroup().location]` | Auto-detected from RG |
| Environment | `prod` | prod/test/staging/dev |
| Deploy Hybrid Worker | `false` | Only if you need on-prem AD |
| Function App Sku | `EP1` | EP1/EP2/EP3 (Premium) |

---

## ? **STEP 3: CLICK "REVIEW + CREATE"**

The Azure Portal will:
1. Validate the ARM template (should take ~30 seconds)
2. Show you a summary of resources to be created
3. Show estimated cost

**Expected Resources (6):**
- Function App (Premium EP1)
- App Service Plan (Premium EP1)
- Storage Account (Primary - LRS)
- Storage Account (Forensics - GRS)
- Application Insights
- Log Analytics Workspace

---

## ? **STEP 4: CLICK "CREATE"**

Deployment will take **~15 minutes**.

You'll see progress for each resource:
```
? Creating Log Analytics Workspace...
? Creating Application Insights...
? Creating Storage Account (Primary)...
? Creating Storage Account (Forensics)...
? Creating App Service Plan...
? Creating Function App...
? Deploying function code...
```

---

## ? **STEP 5: VERIFY TAGS WERE APPLIED**

### **Method 1: Azure Portal**

1. Go to **Resource Group** you deployed to
2. Click on **any resource** (e.g., Function App)
3. Click **Tags** in the left menu
4. You should see **13 tags** (deleteAtTag will be empty):

```
Project: SentryXDR
Environment: prod
CreatedBy: User:12345678-1234-1234-1234-123456789012
ManagedBy: ARM Template
Version: 1.0.0
ApplicationName: SentryXDR
BusinessUnit: Security Operations
CostCenter: SOC
DataClassification: Confidential
Criticality: High
DisasterRecovery: Required
MaintenanceWindow: Sunday 02:00-06:00 UTC
deleteAtTag: (empty)
```

### **Method 2: PowerShell**

```powershell
# Get all resources in the resource group
$resources = Get-AzResource -ResourceGroupName "your-rg-name"

# Check tags on each resource
foreach ($resource in $resources) {
    Write-Host "`n$($resource.Name) - Tags:" -ForegroundColor Cyan
    $resource.Tags | Format-Table -AutoSize
}
```

### **Method 3: Azure CLI**

```bash
# List all resources with tags
az resource list --resource-group "your-rg-name" --query "[].{name:name, tags:tags}" -o table
```

---

## ? **STEP 6: VERIFY FUNCTION APP IS RUNNING**

### **Test Health Endpoint:**

```powershell
# Get Function App URL
$functionAppName = "sentryxdr-func-XXXXX"  # Replace with your actual name
$functionAppUrl = "https://$functionAppName.azurewebsites.net"

# Test health endpoint
Invoke-RestMethod -Uri "$functionAppUrl/api/xdr/health" -Method Get
```

**Expected Response:**
```json
{
  "status": "healthy",
  "version": "1.0.0",
  "timestamp": "2025-01-15T10:30:00Z",
  "services": {
    "appInsights": "connected",
    "storage": "connected",
    "authentication": "configured"
  }
}
```

### **Test Swagger UI:**

Open in browser:
```
https://YOUR-FUNCTION-APP.azurewebsites.net/api/swagger/ui
```

You should see the complete API documentation with all 150+ actions.

---

## ? **TROUBLESHOOTING**

### **Problem: Deployment Fails**

**Common Causes:**

1. **App ID or Secret is wrong**
   ```
   Error: Failed to authenticate
   Fix: Double-check App ID and Secret
   ```

2. **Resource name conflict**
   ```
   Error: Storage account name already taken
   Fix: Change "Project Name" parameter to something unique
   ```

3. **Insufficient permissions**
   ```
   Error: Authorization failed
   Fix: Ensure you have "Contributor" role on subscription
   ```

### **Problem: Tags Not Showing**

**Check:**

1. **Did deployment complete successfully?**
   - Azure Portal ? Deployments ? Check status

2. **Are you looking at the right resource?**
   - Tags are on individual resources, not the Resource Group

3. **Refresh the page**
   - Portal sometimes caches tag data

---

## ? **VERIFICATION CHECKLIST**

After deployment, verify:

- [ ] All 6 resources created successfully
- [ ] Function App shows "Running" status
- [ ] **All resources have 13 tags** (deleteAtTag is empty)
- [ ] Health endpoint returns 200 OK
- [ ] Swagger UI loads successfully
- [ ] App Insights is receiving telemetry
- [ ] Storage accounts are accessible

---

## ?? **EXPECTED DEPLOYMENT TIME**

| Phase | Time | Status Indicator |
|-------|------|------------------|
| Template validation | 30 seconds | "Validating..." |
| Resource creation | 10-12 minutes | "Deploying..." |
| Function code deployment | 2-3 minutes | "Running deployment script..." |
| **TOTAL** | **~15 minutes** | "Deployment succeeded" |

---

## ?? **WHAT GETS DEPLOYED**

### **Infrastructure (6 Resources):**

```
Resource Group: your-rg-name
??? sentryxdr-func-abc123 (Function App)
??? sentryxdr-plan-abc123 (App Service Plan - EP1)
??? sentryxdrfuncabc123 (Storage - Primary LRS)
??? sentryxdrforensicsabc (Storage - Forensics GRS)
??? sentryxdr-ai-abc123 (Application Insights)
??? sentryxdr-logs-abc123 (Log Analytics)
```

### **Configuration (Environment Variables):**

```
MultiTenant:ClientId = YOUR_APP_ID
MultiTenant:ClientSecret = YOUR_APP_SECRET
APPINSIGHTS_INSTRUMENTATIONKEY = auto-generated
AzureWebJobsStorage = auto-generated
ForensicsStorage = auto-generated
```

### **Tags Applied to ALL Resources:**

```json
{
  "Project": "SentryXDR",
  "Environment": "prod",
  "CreatedBy": "User:YOUR_APP_ID",
  "ManagedBy": "ARM Template",
  "Version": "1.0.0",
  "ApplicationName": "SentryXDR",
  "BusinessUnit": "Security Operations",
  "CostCenter": "SOC",
  "DataClassification": "Confidential",
  "Criticality": "High",
  "DisasterRecovery": "Required",
  "MaintenanceWindow": "Sunday 02:00-06:00 UTC",
  "deleteAtTag": ""
}
```

---

## ?? **READY TO DEPLOY?**

**Click the button and let's test it!**

[![Deploy to Azure](https://aka.ms/deploytoazurebutton)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fakefallonitis%2Fsentryxdr%2Fmain%2FDeployment%2Fazuredeploy.json)

**After deployment, come back here and verify the tags using Step 5!**

---

## ?? **PROOF IT WORKS**

After deployment, send me a screenshot of:

1. **Azure Portal ? Your Resource Group ? Any Resource ? Tags**
   - Should show all 13 tags

2. **Function App ? Overview**
   - Should show "Running" status

3. **Browser:** `https://YOUR-APP.azurewebsites.net/api/swagger/ui`
   - Should show Swagger API documentation

---

**Let's do this! Click Deploy and report back!** ??

