# ?? DEPLOY SENTRYXDR TO AZURE

## One-Click Deployment

[![Deploy to Azure](https://aka.ms/deploytoazurebutton)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fakefallonitis%2Fsentryxdr%2Fmain%2FDeployment%2Fazuredeploy.json)

[![Visualize](https://raw.githubusercontent.com/Azure/azure-quickstart-templates/master/1-CONTRIBUTION-GUIDE/images/visualizebutton.svg?sanitize=true)](http://armviz.io/#/?load=https%3A%2F%2Fraw.githubusercontent.com%2Fakefallonitis%2Fsentryxdr%2Fmain%2FDeployment%2Fazuredeploy.json)

## Prerequisites

Before clicking "Deploy to Azure":

1. **Azure Subscription** with Contributor access
2. **App Registration** with permissions configured:
   ```powershell
   cd Deployment/scripts
   .\Setup-SentryXDR-Permissions.ps1 -ExistingAppId "<your-app-id>"
   ```
3. **Admin Consent** granted for required permissions
4. **Client Secret** created and saved securely

## What Gets Deployed

? **Azure Function App** (Premium EP1 plan)
- .NET 8 Isolated runtime
- System-assigned managed identity
- Auto-configured environment variables

? **Storage Accounts** (2x)
- Primary: Function App storage
- Forensics: Investigation packages, live response, detonation results (GRS, Cool tier)

? **Application Insights** + Log Analytics
- Real-time monitoring
- 90-day retention

? **Azure Automation Account** (optional)
- Hybrid Worker support for on-premise AD
- PowerShell runbooks included

? **RBAC Permissions** (auto-configured)
- Contributor role for Function App managed identity
- Storage Blob Data Contributor for forensics storage

## Deployment Parameters

| Parameter | Description | Required | Default |
|-----------|-------------|----------|---------|
| `projectName` | Unique name (3-11 chars) | Yes | `sentryxdr` |
| `location` | Azure region | Yes | `eastus` |
| `environment` | Environment (dev/test/staging/prod) | Yes | `prod` |
| `multiTenantAppId` | App registration client ID | **Yes** | - |
| `multiTenantAppSecret` | App registration secret | **Yes** | - |
| `deployHybridWorker` | Deploy automation account | No | `false` |
| `functionAppSku` | Function plan (EP1/EP2/EP3) | No | `EP1` |

## Deployment Steps

### Option 1: Azure Portal (Recommended)

1. Click **"Deploy to Azure"** button above
2. Fill in required parameters:
   - Project Name: `sentryxdr` (or your choice)
   - Multi-Tenant App ID: `<your-app-id>`
   - Multi-Tenant App Secret: `<your-secret>`
3. Review and create
4. Wait ~15 minutes for deployment
5. Deploy function code:
   ```powershell
   func azure functionapp publish <function-app-name>
   ```

### Option 2: PowerShell

```powershell
cd Deployment/scripts

# Setup permissions (first time only)
.\Setup-SentryXDR-Permissions.ps1 -ExistingAppId "<your-app-id>"

# Deploy infrastructure
.\Deploy-SentryXDR.ps1 `
    -ResourceGroupName "sentryxdr-rg" `
    -Location "eastus" `
    -Environment "prod"

# Deploy function code
cd ../..
dotnet publish --configuration Release
func azure functionapp publish <function-app-name>
```

### Option 3: Azure CLI

```bash
# Create resource group
az group create --name sentryxdr-rg --location eastus

# Deploy template
az deployment group create \
  --resource-group sentryxdr-rg \
  --template-file Deployment/azuredeploy.json \
  --parameters \
    projectName=sentryxdr \
    multiTenantAppId="<your-app-id>" \
    multiTenantAppSecret="<your-secret>"
```

## Post-Deployment

### 1. Verify Deployment
```powershell
$functionUrl = "https://<function-app-name>.azurewebsites.net"
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

### 2. Configure RBAC (Auto-configured, verify only)
```powershell
# Function App should have Contributor role
$functionApp = Get-AzWebApp -Name "<function-app-name>"
Get-AzRoleAssignment -ObjectId $functionApp.Identity.PrincipalId
```

### 3. Test Remediation
```powershell
$body = @{
    tenantId = "<tenant-id>"
    platform = "MDE"
    action = "IsolateDevice"
    parameters = @{
        deviceId = "<device-id>"
    }
} | ConvertTo-Json

Invoke-RestMethod `
    -Uri "$functionUrl/api/v1/remediation/submit" `
    -Method Post `
    -Body $body `
    -ContentType "application/json" `
    -Headers @{ "x-functions-key" = "<function-key>" }
```

## Architecture

```
???????????????????????????????????????????
?      REST API Gateway (Swagger)         ?
?    https://<func>.azurewebsites.net     ?
???????????????????????????????????????????
               ?
               ?
????????????????????????????????????????????
?   Durable Functions Orchestrator         ?
?   - Workflow coordination                 ?
?   - Error handling                        ?
?   - Native history tracking               ?
????????????????????????????????????????????
               ?
        ???????????????
        ?             ?
????????????????  ????????????????
? MDE Worker   ?  ? MDO Worker   ?
? Entra Worker ?  ? Azure Worker ?
? Intune       ?  ? MCAS         ?
????????????????  ????????????????
       ?                 ?
       ???????????????????
                ?
?????????????????????????????????????????????
?     Microsoft Native APIs                  ?
?  - Graph API                               ?
?  - MDE API                                 ?
?  - Azure Management                        ?
?????????????????????????????????????????????
```

## Estimated Costs

| Resource | SKU | Monthly Cost (USD) |
|----------|-----|-------------------|
| Function App | EP1 | ~$170 |
| Storage (Primary) | Standard LRS | ~$20 |
| Storage (Forensics) | Standard GRS | ~$50 |
| Application Insights | Per GB | ~$50 |
| Automation Account | Basic | ~$10 |
| **Total** | | **~$300/month** |

*Costs may vary by region and usage*

## Troubleshooting

### Deployment Fails

**Error:** `Resource name already in use`

**Solution:**
```powershell
# Use different project name
.\Deploy-SentryXDR.ps1 -ProjectName "sentryxdr2"
```

### Function App Won't Start

**Error:** `Function app is in stopped state`

**Solution:**
```powershell
# Restart function app
Restart-AzWebApp -Name "<function-app-name>" -ResourceGroupName "sentryxdr-rg"

# Check logs
Get-AzWebAppLogStream -Name "<function-app-name>" -ResourceGroupName "sentryxdr-rg"
```

### Authentication Errors

**Error:** `AADSTS700016: Application not found`

**Solution:**
- Wait 5-10 minutes for permissions to propagate
- Verify admin consent was granted
- Check app registration exists

## Support

- **Issues:** https://github.com/akefallonitis/sentryxdr/issues
- **Documentation:** [DEPLOYMENT_GUIDE.md](DEPLOYMENT_GUIDE.md)
- **Discussions:** https://github.com/akefallonitis/sentryxdr/discussions

---

**Ready to deploy?** Click the "Deploy to Azure" button at the top!
