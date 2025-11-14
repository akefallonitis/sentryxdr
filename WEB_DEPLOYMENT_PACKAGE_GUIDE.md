# ?? WEB DEPLOYMENT PACKAGE - COMPLETE GUIDE

**Status:** ? **ARM TEMPLATE READY** | ? **PACKAGE PENDING**  
**Updated:** 2025-01-XX

---

## ? **WHAT'S READY**

### **1. ARM Template (Complete)**
**File:** `Deployment/azuredeploy.json`

**Features:**
- ? All 12 parameters (including `packageUrl`)
- ? Auto-configured environment variables
- ? RBAC permissions auto-assigned
- ? All tags applied
- ? `WEBSITE_RUN_FROM_PACKAGE` properly configured

**Package URL Parameter:**
```json
"packageUrl": {
  "type": "string",
  "defaultValue": "https://github.com/akefallonitis/sentryxdr/releases/download/v1.0.0/sentryxdr-deploy.zip",
  "metadata": {
    "description": "Direct URL to deployment package ZIP"
  }
}
```

**How It Works:**
```json
{
  "name": "WEBSITE_RUN_FROM_PACKAGE",
  "value": "[parameters('packageUrl')]"
}
```

When Function App starts, it:
1. Downloads ZIP from `packageUrl`
2. Extracts to memory
3. Runs directly (no disk I/O)
4. Auto-updates when URL changes

---

## ?? **DEPLOYMENT PACKAGE STRUCTURE**

### **Required Format for Azure Functions**

```
sentryxdr-deploy.zip
??? bin/
?   ??? SentryXDR.dll (and all dependencies)
??? host.json
??? local.settings.json (optional)
??? [function folders]/
    ??? function.json
    ??? ...
```

**IMPORTANT:** Files must be at ZIP root, NOT in a subfolder!

? **Wrong:**
```
sentryxdr-deploy.zip
??? sentryxdr/
    ??? bin/
    ??? host.json
    ??? ...
```

? **Correct:**
```
sentryxdr-deploy.zip
??? bin/
??? host.json
??? ...
```

---

## ??? **HOW TO CREATE PACKAGE**

### **Option 1: Automated Script (Recommended)**
**File:** `Deployment/scripts/Build-FunctionAppPackage.ps1`

```powershell
cd Deployment/scripts
.\Build-FunctionAppPackage.ps1 -OutputPath "sentryxdr-deploy.zip"
```

**What it does:**
1. Cleans solution
2. Restores NuGet packages
3. Builds in Release mode
4. Publishes to temp folder
5. Creates ZIP at root level
6. Cleans up temp files

**Output:** `sentryxdr-deploy.zip` ready for deployment

---

### **Option 2: Manual (dotnet publish)**

```powershell
# 1. Clean
dotnet clean --configuration Release

# 2. Restore
dotnet restore

# 3. Build
dotnet build --configuration Release --no-restore

# 4. Publish
dotnet publish --configuration Release --output "./publish" --no-build

# 5. Create ZIP (PowerShell)
Compress-Archive -Path "./publish/*" -DestinationPath "sentryxdr-deploy.zip" -Force

# 6. Cleanup
Remove-Item -Path "./publish" -Recurse -Force
```

---

### **Option 3: Visual Studio**

1. Right-click project ? **Publish**
2. Target: **Folder**
3. Configuration: **Release**
4. Click **Publish**
5. ZIP the output folder contents (not the folder itself!)

---

## ?? **DEPLOYMENT OPTIONS**

### **Option A: GitHub Release (Recommended)**

**Steps:**
1. Build package: `.\Build-FunctionAppPackage.ps1`
2. Go to: https://github.com/akefallonitis/sentryxdr/releases
3. Click "Create new release"
4. Tag: `v1.0.0`
5. Upload: `sentryxdr-deploy.zip`
6. Publish

**Result:** Package available at:
```
https://github.com/akefallonitis/sentryxdr/releases/download/v1.0.0/sentryxdr-deploy.zip
```

**ARM template automatically uses this URL!**

---

### **Option B: Azure Storage Account**

**Steps:**
1. Build package
2. Upload to Azure Storage container
3. Generate SAS token (read-only, 1 year expiry)
4. Use SAS URL in ARM template parameter

**Example:**
```bash
az storage blob upload \
  --account-name mystorageaccount \
  --container-name packages \
  --name sentryxdr-deploy.zip \
  --file sentryxdr-deploy.zip

az storage blob generate-sas \
  --account-name mystorageaccount \
  --container-name packages \
  --name sentryxdr-deploy.zip \
  --permissions r \
  --expiry 2026-01-01
```

**Use in deployment:**
```powershell
.\Deploy-SentryXDR.ps1 `
  -PackageUrl "https://mystorageaccount.blob.core.windows.net/packages/sentryxdr-deploy.zip?sp=r&st=..." `
  -AppId "<your-app-id>" `
  -AppSecret "<your-secret>"
```

---

### **Option C: Direct Upload (Testing)**

For testing only:

```bash
# Using Azure Functions Core Tools
func azure functionapp publish <function-app-name> --zip sentryxdr-deploy.zip

# Using Azure CLI
az functionapp deployment source config-zip \
  --resource-group sentryxdr-rg \
  --name sentryxdr-func-xxx \
  --src sentryxdr-deploy.zip
```

---

## ?? **ARM TEMPLATE PARAMETERS**

### **Using Custom Package URL**

When deploying, override the `packageUrl` parameter:

**PowerShell:**
```powershell
New-AzResourceGroupDeployment `
  -ResourceGroupName "sentryxdr-rg" `
  -TemplateFile "Deployment/azuredeploy.json" `
  -multiTenantAppId "<your-app-id>" `
  -multiTenantAppSecret "<your-secret>" `
  -packageUrl "https://your-custom-url.com/sentryxdr-deploy.zip"
```

**Azure CLI:**
```bash
az deployment group create \
  --resource-group sentryxdr-rg \
  --template-file Deployment/azuredeploy.json \
  --parameters \
    multiTenantAppId="<your-app-id>" \
    multiTenantAppSecret="<your-secret>" \
    packageUrl="https://your-custom-url.com/sentryxdr-deploy.zip"
```

**Azure Portal:**
- Click "Deploy to Azure" button
- Fill in required parameters
- Change `packageUrl` if needed
- Deploy

---

## ?? **VERIFICATION**

### **Check if Function App is using package:**

```powershell
# Get Function App settings
$settings = az functionapp config appsettings list \
  --name sentryxdr-func-xxx \
  --resource-group sentryxdr-rg \
  --output json | ConvertFrom-Json

# Find WEBSITE_RUN_FROM_PACKAGE
$settings | Where-Object { $_.name -eq "WEBSITE_RUN_FROM_PACKAGE" }
```

**Expected output:**
```
name: WEBSITE_RUN_FROM_PACKAGE
value: https://github.com/akefallonitis/sentryxdr/releases/download/v1.0.0/sentryxdr-deploy.zip
```

### **Test health endpoint:**

```powershell
$healthUrl = "https://sentryxdr-func-xxx.azurewebsites.net/api/xdr/health"
Invoke-RestMethod -Uri $healthUrl -Method Get
```

**Expected response:**
```json
{
  "status": "Healthy",
  "version": "1.0.0",
  "timestamp": "2025-01-XX..."
}
```

---

## ?? **TROUBLESHOOTING**

### **Issue: "Could not load file or assembly"**
**Cause:** Missing dependencies in package  
**Fix:** Ensure `dotnet publish` includes all dependencies

```powershell
dotnet publish --configuration Release --output "./publish" --self-contained false
```

### **Issue: "Function runtime is unable to start"**
**Cause:** Incorrect ZIP structure (files in subfolder)  
**Fix:** Ensure files are at ZIP root

```powershell
# Wrong - includes parent folder
Compress-Archive -Path "./publish" -DestinationPath "package.zip"

# Correct - files at root
Compress-Archive -Path "./publish/*" -DestinationPath "package.zip"
```

### **Issue: "Could not download package"**
**Cause:** URL inaccessible or requires authentication  
**Fix:** 
- Ensure URL is publicly accessible
- For Azure Storage, use SAS token with read permissions
- For GitHub, ensure release is published (not draft)

---

## ?? **PACKAGE SIZE EXPECTATIONS**

| Component | Size | Notes |
|-----------|------|-------|
| **Compiled binaries** | ~5 MB | SentryXDR + dependencies |
| **NuGet packages** | ~15 MB | Azure SDK, Graph SDK, etc. |
| **Runtime files** | ~2 MB | host.json, configs |
| **Total** | ~22 MB | Typical size |

**Azure Functions limits:**
- Max package size: 1 GB
- Recommended: < 100 MB for fast startup

---

## ? **CURRENT STATUS**

| Item | Status | Notes |
|------|--------|-------|
| **ARM Template** | ? Complete | packageUrl parameter ready |
| **Build Script** | ? Complete | Build-FunctionAppPackage.ps1 |
| **Package Structure** | ? Documented | Correct format defined |
| **Deployment Options** | ? Documented | 3 options available |
| **Actual Package** | ? Pending | Needs: Build ? Upload ? GitHub Release |

---

## ?? **NEXT STEPS TO COMPLETE**

### **For Full One-Click Deployment:**

1. **Fix Compilation Errors** (Priority 1)
   - Fix `Services/Workers/EntraIDConditionalAccessService.cs` (DONE ?)
   - Fix other compilation errors
   - Verify build succeeds

2. **Build Package** (5 minutes)
```powershell
cd Deployment/scripts
.\Build-FunctionAppPackage.ps1
```

3. **Create GitHub Release** (5 minutes)
   - Go to: https://github.com/akefallonitis/sentryxdr/releases
   - Create release v1.0.0
   - Upload `sentryxdr-deploy.zip`
   - Publish

4. **Test Deployment** (15 minutes)
   - Click "Deploy to Azure" button
   - Verify package downloads
   - Check health endpoint
   - Test Swagger UI

---

## ?? **REFERENCES**

- **Azure Functions Deployment:** https://learn.microsoft.com/en-us/azure/azure-functions/deployment-zip-push
- **WEBSITE_RUN_FROM_PACKAGE:** https://learn.microsoft.com/en-us/azure/azure-functions/run-functions-from-deployment-package
- **GitHub Releases:** https://docs.github.com/en/repositories/releasing-projects-on-github

---

**?? Status:** ARM template ready, package pending compilation fix + build

**?? Time to Complete:** ~25 minutes (fix errors ? build ? upload ? test)

**?? Then:** One-click deployment will be 100% functional!
