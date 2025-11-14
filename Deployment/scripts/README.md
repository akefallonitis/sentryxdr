# ??? SentryXDR - Microsoft Security XDR Orchestration Platform

[![Deploy to Azure](https://aka.ms/deploytoazurebutton)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fakefallonitis%2Fsentryxdr%2Fmain%2FDeployment%2Fazuredeploy.json)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/)
[![Status](https://img.shields.io/badge/Status-Production%20Ready-green.svg)]()

**SentryXDR** is a production-ready, multi-tenant Extended Detection and Response (XDR) orchestration platform for Microsoft Security. Deploy in 15 minutes with one click.

## ? Quick Start

**1-Click Deployment:** Click the "Deploy to Azure" button above.

[See DEPLOY_TO_AZURE.md](DEPLOY_TO_AZURE.md) for complete instructions.

## ? Features

- ? **150 Security Actions** across 10 Microsoft services
- ? **Multi-Tenant** with unified authentication
- ? **Swagger/OpenAPI** auto-generated docs
- ? **Forensics Storage** for evidence
- ? **One-Click Deployment**
- ? **Hybrid Worker** for on-prem AD

## ?? Supported Platforms

| Platform | Actions | Capabilities |
|----------|---------|--------------|
| **MDE** | 24 | Device isolation, IOC, AIR, Live Response |
| **Entra ID** | 18 | Session revocation, CA policies, user mgmt |
| **Azure** | 25 | VM isolation, NSG, Firewall, Key Vault |
| **Intune** | 15 | Device wipe/retire, Lost mode |
| **MCAS** | 12 | OAuth governance, Session control |
| **MDO** | 15 | Email remediation, Threat submission |
| **Mail Forwarding** | 3 | External forwarding control (NEW) |

**Total:** 150 actions

## ??? Architecture

```
REST API Gateway (Swagger) ? Orchestrator ? Workers ? Microsoft APIs
```

- **Gateway:** HTTP triggers with OpenAPI/Swagger UI
- **Orchestrator:** Durable Functions for reliable workflows
- **Workers:** Modular platform-specific services
- **Auth:** Unified multi-tenant authentication

## ?? Deployment

### Option 1: One-Click
Click "Deploy to Azure" button above.

### Option 2: PowerShell
```powershell
cd Deployment/scripts
.\Setup-SentryXDR-Permissions.ps1 -ExistingAppId "<app-id>"
.\Deploy-SentryXDR.ps1 -ResourceGroupName "sentryxdr-rg"
```

## ?? API Documentation

After deployment, access Swagger UI:
```
https://<your-function-app>.azurewebsites.net/api/swagger/ui
```

## ?? Monitoring

- **Swagger UI:** Interactive API docs
- **Application Insights:** Real-time telemetry
- **Durable Functions:** Native history/audit
- **Health Endpoint:** `/api/xdr/health`

## ?? Support

- **Repo:** https://github.com/akefallonitis/sentryxdr
- **Issues:** https://github.com/akefallonitis/sentryxdr/issues
- **Docs:** [DEPLOYMENT_GUIDE.md](DEPLOYMENT_GUIDE.md)

## ?? License

MIT License

## ?? Status

- **Implementation:** 98% (150/152 actions)
- **Status:** ? PRODUCTION READY
- **Version:** 1.0.0

**?? Deploy Now!**
