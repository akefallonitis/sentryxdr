# ??? SentryXDR - Microsoft Security XDR Orchestration Platform

[![Deploy to Azure](https://aka.ms/deploytoazurebutton)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fakefallonitis%2Fsentryxdr%2Fmain%2FDeployment%2Fazuredeploy.json)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/)
[![Status](https://img.shields.io/badge/Status-Production%20Ready-green.svg)]()
[![Version](https://img.shields.io/badge/Version-1.0.0-blue.svg)]()

**SentryXDR** is a production-ready, multi-tenant Extended Detection and Response (XDR) orchestration platform for Microsoft Security. Deploy in 15 minutes with one click.

> **?? v2.0 Coming Soon:** Azure Workbook integration - Single pane of glass for all XDR operations! [See Roadmap](#v20-roadmap)

---

## ? Quick Start

### 1-Click Deployment
[![Deploy to Azure](https://aka.ms/deploytoazurebutton)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fakefallonitis%2Fsentryxdr%2Fmain%2FDeployment%2Fazuredeploy.json)

**Time:** ~15 minutes | **Prerequisites:** Azure subscription + App registration

[Complete Deployment Guide ?](DEPLOY_TO_AZURE.md)

---

## ? Features

### Current (v1.0)
- ? **150 Security Actions** across 10 Microsoft services
- ? **Multi-Tenant** with unified authentication
- ? **Swagger/OpenAPI** auto-generated docs at `/api/swagger/ui`
- ? **Durable Functions** orchestration (reliable workflows)
- ? **Forensics Storage** for investigation packages
- ? **One-Click Deployment** via ARM template
- ? **Hybrid Worker** for on-premise AD

### Coming Soon (v2.0)
- ?? **Azure Workbook** - Single pane of glass console
- ?? **183 Actions** (+33 new actions)
- ?? **Live Response Console** - Interactive shell
- ?? **Advanced Hunting** - KQL query editor
- ?? **Multi-Tenant Dashboard** - Lighthouse support
- ?? **Sentinel Integration** - Native incident sync

---

## ?? Supported Platforms

| Platform | Actions | Key Capabilities |
|----------|---------|------------------|
| **MDE** | 24 | Device isolation, IOC, AIR, Live Response |
| **Entra ID** | 18 | Session revocation, CA policies, Identity Protection |
| **Azure** | 25 | VM isolation, NSG, Firewall, Key Vault |
| **Intune** | 15 | Device wipe/retire, Lost mode, App protection |
| **MCAS** | 12 | OAuth governance, Session control |
| **MDO** | 15 | Email remediation, Threat submission |
| **Mail Forwarding** | 3 | External forwarding control (NEW!) |
| **DLP** | 5 | File sharing, Quarantine |
| **On-Prem AD** | 5 | User/computer management (Hybrid Worker) |
| **Incident Mgmt** | 18 | XDR incident lifecycle |

**Total: 150 actions** | **v2.0 Target: 183 actions**

---

## ??? Architecture

```
???????????????????????????????????????????????????
?   REST API Gateway (Swagger/OpenAPI)           ?
?   /api/swagger/ui - Interactive docs            ?
???????????????????????????????????????????????????
                   ?
                   ?
???????????????????????????????????????????????????
?   Durable Functions Orchestrator                ?
?   - Workflow coordination                       ?
?   - Error handling & retry                      ?
?   - Native history/audit                        ?
???????????????????????????????????????????????????
                   ?
      ???????????????????????????????????????
      ?            ?           ?            ?
 ??????????  ???????????  ??????????  ?????????
 ?  MDE   ?  ?EntraID  ?  ? Azure  ?  ?  MDO  ?
 ? Worker ?  ? Worker  ?  ? Worker ?  ?Worker ?
 ??????????  ???????????  ??????????  ?????????
      ?           ?            ?           ?
      ??????????????????????????????????????
                   ?
                   ?
???????????????????????????????????????????????????
?   Unified Multi-Tenant Authentication           ?
?   - Microsoft Graph (v1.0 & Beta)              ?
?   - MDE API | Azure Management API             ?
???????????????????????????????????????????????????
```

---

## ?? Deployment

### Option 1: One-Click (Recommended)
Click "Deploy to Azure" button above.

**Deploys:**
- Azure Function App (Premium EP1)
- Storage Accounts (2x: primary + forensics)
- Application Insights + Log Analytics
- Automation Account (optional for on-prem AD)
- All settings auto-configured

### Option 2: PowerShell
```powershell
cd Deployment/scripts
.\Setup-SentryXDR-Permissions.ps1 -ExistingAppId "<your-app-id>"
.\Deploy-SentryXDR.ps1 -ResourceGroupName "sentryxdr-rg"
```

### Option 3: Azure CLI
```bash
az deployment group create \
  --resource-group sentryxdr-rg \
  --template-file Deployment/azuredeploy.json \
  --parameters multiTenantAppId="<id>" multiTenantAppSecret="<secret>"
```

---

## ?? API Documentation

### Swagger UI (Interactive)
After deployment:
```
https://<your-function-app>.azurewebsites.net/api/swagger/ui
```

### Example: Isolate Device
```http
POST /api/v1/remediation/submit
Content-Type: application/json

{
  "tenantId": "00000000-0000-0000-0000-000000000000",
  "platform": "MDE",
  "action": "IsolateDevice",
  "parameters": {
    "deviceId": "device-guid",
    "isolationType": "Full"
  },
  "justification": "Malware detected - Incident #12345"
}
```

### Example: Disable Mail Forwarding (NEW)
```http
POST /api/v1/remediation/submit
Content-Type: application/json

{
  "tenantId": "00000000-0000-0000-0000-000000000000",
  "platform": "MDO",
  "action": "DisableExternalMailForwarding",
  "parameters": {
    "userId": "user-guid"
  }
}
```

[More Examples ?](DEPLOYMENT_GUIDE.md#api-examples)

---

## ?? Security & Permissions

### Least Privilege Model
Uses **minimal required permissions**:
- `SecurityEvents.Read.All` - Security events
- `Machine.ReadWrite.All` - MDE operations
- `Mail.ReadWrite` - Email remediation
- `MailboxSettings.ReadWrite` - Mail forwarding control
- `User.ReadWrite.All` - User management
- `SecurityIncident.ReadWrite.All` - Incident management

**Total:** 18 application permissions

### Authentication
- ? Multi-tenant app registration
- ? Environment variables (no Key Vault)
- ? Managed Identity for Azure resources
- ? Unified auth across all APIs

---

## ?? Use Cases

### 1. Incident Response Automation
```
Alert ? SentryXDR ? Isolate device + Block user + Collect forensics
```

### 2. SIEM Integration
```
Sentinel/Splunk ? Webhook ? SentryXDR ? Automated remediation
```

### 3. Logic Apps (via OpenAPI)
```
Logic App ? Custom Connector ? SentryXDR ? Multi-step workflow
```

### 4. Security Copilot (v2.0)
```
Copilot ? SentryXDR API ? Execute XDR actions
```

---

## ?? v2.0 Roadmap

### ?? Azure Workbook Integration
**Vision:** Single pane of glass for all XDR operations

**Features:**
- ?? Multi-tenant dashboard (Lighthouse support)
- ?? Incident/Alert-driven actions
- ?? Live Response console (interactive shell)
- ?? Advanced Hunting (KQL editor)
- ?? Auto-populated dropdowns
- ?? Conditional visibility per context
- ?? File upload/download
- ?? Auto-refresh listings

### ?? New API Actions (+33)
- **MDE:** +8 (Vulnerability management, ASR rules, Network containment)
- **Entra ID:** +6 (Risk detection, Identity Protection policies)
- **Azure:** +8 (Sentinel, Defender for Cloud, Azure Firewall)
- **MDO:** +4 (Safe Links/Attachments, Anti-phishing policies)
- **Intune:** +3 (App protection, Configuration profiles)
- **MCAS:** +4 (Cloud Discovery, Activity logs)

**v2.0 Total:** 183 actions (22% increase)

[Full Roadmap ?](V2_ROADMAP_API_WORKBOOK.md)

---

## ?? What's Included

### Code
- `Functions/Gateway/` - REST API with Swagger
- `Functions/XDROrchestrator.cs` - Orchestration logic
- `Services/Workers/` - 10 platform workers
- `Services/Authentication/` - Multi-tenant auth
- `Services/Storage/` - Forensics storage

### Deployment
- `Deployment/azuredeploy.json` - ARM template
- `Deployment/scripts/` - PowerShell automation
- `Deployment/azure-pipelines.yml` - CI/CD

### Documentation
- `DEPLOYMENT_GUIDE.md` - Step-by-step guide
- `FINAL_PROJECT_STATUS.md` - Implementation status
- `V2_ROADMAP_API_WORKBOOK.md` - v2.0 roadmap

---

## ?? Monitoring

### Native Capabilities
- ? **Audit Logs:** Durable Functions history
- ? **Action Tracking:** Orchestration instances
- ? **Cancellation:** Native terminate API
- ? **Health Check:** `/api/xdr/health`
- ? **Telemetry:** Application Insights

### Endpoints
```http
GET /api/v1/remediation/{id}/status     # Query status
DELETE /api/v1/remediation/{id}/cancel  # Cancel action
GET /api/v1/remediation/history         # History
GET /api/xdr/health                     # Health check
```

---

## ??? Development

### Local Development
```powershell
dotnet restore
dotnet build
func start
```

### Test Locally
```powershell
# Health check
Invoke-RestMethod -Uri "http://localhost:7071/api/xdr/health"

# Swagger UI
Start-Process "http://localhost:7071/api/swagger/ui"
```

---

## ?? Support

- **Repository:** https://github.com/akefallonitis/sentryxdr
- **Issues:** https://github.com/akefallonitis/sentryxdr/issues
- **Docs:** [DEPLOYMENT_GUIDE.md](DEPLOYMENT_GUIDE.md)
- **Roadmap:** [V2_ROADMAP_API_WORKBOOK.md](V2_ROADMAP_API_WORKBOOK.md)

---

## ?? License

MIT License - see [LICENSE](LICENSE)

---

## ?? Status

- **Version:** 1.0.0 (Production Ready)
- **Implementation:** 98% (150/152 actions)
- **Deployment:** ? One-click ready
- **Documentation:** ? Complete
- **v2.0 ETA:** Q2 2025

---

## ?? Next Steps

### After Deployment
1. Access Swagger UI
2. Test health endpoint
3. Submit test remediation
4. Export OpenAPI for Logic Apps
5. Configure Application Insights alerts

### Optional Enhancements
- Add Azure API Management
- Implement approval workflows
- Connect to Microsoft Sentinel
- **Deploy v2.0 Workbook** (Coming soon!)

---

**Built with ?? for Security Operations Teams**

**?? Deploy Now:** Click "Deploy to Azure" at the top!

---

## ?? Star History

If you find SentryXDR useful, please ? star the repository!

---

**Latest Updates:**
- **v1.0.0 (Current):** Production release with 150 actions
- **v2.0 (Planned):** Workbook integration + 33 new actions
- **Future:** Security Copilot integration, Advanced automation
