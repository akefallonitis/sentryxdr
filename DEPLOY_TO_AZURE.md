# ?? DEPLOY SENTRYXDR TO AZURE

[![Deploy to Azure](https://aka.ms/deploytoazurebutton)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fakefallonitis%2Fsentryxdr%2Fmain%2FDeployment%2Fazuredeploy.json)

## One-Click Deployment

Click the button above to deploy SentryXDR to your Azure subscription.

### Prerequisites
- Azure subscription with Contributor access
- App registration already created (run `Setup-SentryXDR-Permissions.ps1` first)
- Admin consent granted for required permissions

### What Gets Deployed
- ? Azure Function App (Premium EP1)
- ? Storage Accounts (2x - primary + forensics)
- ? Key Vault (with app secret)
- ? Application Insights + Log Analytics
- ? Automation Account (optional)
- ? All environment variables auto-configured
- ? RBAC permissions assigned

### Deployment Time
~15 minutes

### Required Parameters
- `multiTenantAppId` - Your app registration client ID
- `multiTenantAppSecret` - Your app registration client secret
- `projectName` - Unique name (3-11 characters)
- `location` - Azure region (default: eastus)

---

## Manual Deployment

If you prefer manual deployment:

```powershell
cd Deployment/scripts
.\Setup-SentryXDR-Permissions.ps1 -ExistingAppId "<your-app-id>"
.\Deploy-SentryXDR.ps1 -ResourceGroupName "sentryxdr-rg"
```

See [DEPLOYMENT_GUIDE.md](DEPLOYMENT_GUIDE.md) for detailed instructions.
