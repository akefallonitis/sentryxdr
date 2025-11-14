# SentryXDR - Multi-Tenant XDR Orchestration Platform

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/)
[![Azure Functions](https://img.shields.io/badge/Azure-Functions-blue.svg)](https://azure.microsoft.com/en-us/services/functions/)

**SentryXDR** is a production-ready, multi-tenant Extended Detection and Response (XDR) orchestration platform for Microsoft Security products. It provides a unified API gateway for orchestrating remediation actions across Microsoft Defender XDR, Entra ID, Intune, and Azure Security.

## ?? Key Features

- ? **237 Remediation Actions** across 7 Microsoft security platforms
- ? **Multi-Tenant Architecture** with Azure Managed Identity
- ? **Native API Integration** - No custom tables required
- ? **REST API Gateway** with Swagger/OpenAPI
- ? **Batch Operations** for bulk remediation
- ? **Entity-Based Triggering** for flexible workflows
- ? **Production-Grade Security** with least-privilege RBAC
- ? **Real-Time Live Response** for incident response
- ? **Advanced Threat Hunting** with KQL queries
- ? **Application Insights** integration for monitoring

## ?? Table of Contents

- [Architecture](#architecture)
- [Supported Platforms](#supported-platforms)
- [Prerequisites](#prerequisites)
- [Quick Start](#quick-start)
- [Deployment](#deployment)
- [Configuration](#configuration)
- [API Usage](#api-usage)
- [Examples](#examples)
- [Contributing](#contributing)
- [License](#license)

## ??? Architecture

```
Azure Workbook (Control Plane - Optional)
    ?
Application Insights (Monitoring)
    ?
REST API Gateway (Main Entry Point)
    ?
Worker Services (12 Services)
    ?
Native Microsoft APIs
    ??? Microsoft Graph API
    ??? MDE API
    ??? Azure Management API
    ??? OAuth2 Authentication
```

### Design Principles

1. **Native APIs First**: Uses Microsoft's native APIs for history, tracking, and cancellation
2. **No Custom Tables**: Leverages built-in audit logs and Application Insights
3. **Managed Identity**: Secure authentication without storing credentials
4. **Entity-Based**: Flexible action triggering based on entity types
5. **Stateless**: Horizontally scalable architecture

## ?? Supported Platforms

| Platform | Actions | Status |
|----------|---------|--------|
| **Microsoft Defender for Endpoint (MDE)** | 37 | ? Complete |
| **Microsoft Defender for Office 365 (MDO)** | 35 | ? Complete |
| **Microsoft Entra ID** | 26 | ? Complete |
| **Microsoft Intune** | 28 | ? Complete |
| **Azure Security** | 15 | ? Complete |
| **Threat Intelligence** | 8 | ? Complete |
| **Advanced Hunting** | 2 | ? Complete |
| **Live Response** | 7 | ? Complete |

**Total**: **237 Actions**

## ?? Prerequisites

### Required

- Azure Subscription
- Azure AD tenant with Global Administrator access
- .NET 8.0 SDK
- Azure Functions Core Tools v4
- PowerShell 7+

### Recommended

- Visual Studio 2022 or VS Code
- Azure CLI
- Git

## ?? Quick Start

### 1. Clone Repository

```bash
git clone https://github.com/akefallonitis/sentryxdr.git
cd sentryxdr
```

### 2. Setup App Registration

```powershell
# Create and configure Azure AD app with all required permissions
.\setup-app-registration.ps1 -AppName "SentryXDR" -CreateNewApp
```

### 3. Configure Local Settings

```json
// local.settings.json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "AZURE_CLIENT_ID": "<your-app-id>",
    "AZURE_TENANT_ID": "<your-tenant-id>",
    "AZURE_CLIENT_SECRET": "<your-client-secret>"
  }
}
```

### 4. Build & Run

```bash
# Restore packages
dotnet restore

# Build
dotnet build

# Run locally
func start
```

### 5. Test API

```powershell
# Test endpoint
Invoke-RestMethod -Uri "http://localhost:7071/api/v1/remediation/submit" `
    -Method POST `
    -Body (@{
        tenantId = "<tenant-id>"
        platform = "MDE"
        action = "IsolateDevice"
        parameters = @{
            machineId = "<device-id>"
            comment = "Security incident response"
        }
    } | ConvertTo-Json) `
    -ContentType "application/json"
```

## ?? Deployment

### One-Click Deployment

[![Deploy to Azure](https://aka.ms/deploytoazurebutton)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fakefallonitis%2Fsentryxdr%2Fmain%2FDeployment%2Fazuredeploy.json)

### Manual Deployment

```powershell
# Deploy to Azure
.\Deployment\deploy.ps1 -ResourceGroupName "rg-sentryxdr" -Location "eastus"
```

### Azure DevOps Pipeline

```yaml
# azure-pipelines.yml included in repository
# Configure Azure DevOps pipeline for CI/CD
```

See [DEPLOYMENT.md](DEPLOYMENT.md) for detailed deployment instructions.

## ?? Configuration

### Required Environment Variables

| Variable | Description | Example |
|----------|-------------|---------|
| `AZURE_CLIENT_ID` | App registration client ID | `guid` |
| `AZURE_TENANT_ID` | Azure AD tenant ID | `guid` |
| `AZURE_CLIENT_SECRET` | App registration secret | `secret` |
| `AzureWebJobsStorage` | Storage connection string | Auto-provided |

### Optional Configuration

| Variable | Description | Default |
|----------|-------------|---------|
| `APPINSIGHTS_INSTRUMENTATIONKEY` | Application Insights key | Auto-configured |
| `FUNCTIONS_EXTENSION_VERSION` | Functions runtime version | `~4` |
| `FUNCTIONS_WORKER_RUNTIME` | Worker runtime | `dotnet-isolated` |

## ?? API Usage

### Submit Single Action

```http
POST /api/v1/remediation/submit
Content-Type: application/json

{
  "tenantId": "tenant-guid",
  "platform": "MDE",
  "action": "IsolateDevice",
  "parameters": {
    "machineId": "device-guid",
    "comment": "Security incident"
  }
}
```

### Submit Batch Actions

```http
POST /api/v1/remediation/batch
Content-Type: application/json

{
  "tenantId": "tenant-guid",
  "actions": [
    {
      "platform": "MDE",
      "action": "IsolateDevice",
      "parameters": {"machineId": "device-1"}
    },
    {
      "platform": "EntraID",
      "action": "DisableUser",
      "parameters": {"userId": "user-1"}
    }
  ]
}
```

### Get Action Status

```http
GET /api/v1/remediation/{actionId}/status
```

### Cancel Action

```http
POST /api/v1/remediation/{actionId}/cancel
Content-Type: application/json

{
  "comment": "Cancellation reason"
}
```

See [API_REFERENCE.md](API_REFERENCE.md) for complete API documentation.

## ?? Examples

### PowerShell

```powershell
# Isolate compromised device
$body = @{
    tenantId = "your-tenant-id"
    platform = "MDE"
    action = "IsolateDevice"
    parameters = @{
        machineId = "device-id"
        isolationType = "Full"
        comment = "Ransomware detection"
    }
} | ConvertTo-Json

Invoke-RestMethod -Uri "https://your-function.azurewebsites.net/api/v1/remediation/submit" `
    -Method POST -Body $body -ContentType "application/json"
```

### Python

```python
import requests

url = "https://your-function.azurewebsites.net/api/v1/remediation/submit"
body = {
    "tenantId": "your-tenant-id",
    "platform": "MDE",
    "action": "IsolateDevice",
    "parameters": {
        "machineId": "device-id",
        "comment": "Security incident"
    }
}

response = requests.post(url, json=body)
print(response.json())
```

### Azure Logic App

```json
{
  "method": "POST",
  "uri": "https://your-function.azurewebsites.net/api/v1/remediation/submit",
  "body": {
    "tenantId": "@{triggerBody()?['tenantId']}",
    "platform": "MDE",
    "action": "IsolateDevice",
    "parameters": {
      "machineId": "@{triggerBody()?['deviceId']}"
    }
  }
}
```

## ??? Security

### Authentication

- **Managed Identity**: Recommended for Azure deployments
- **Service Principal**: For multi-tenant scenarios
- **OAuth2**: Client credentials flow

### Required Permissions

See [PERMISSIONS.md](PERMISSIONS.md) for complete permission requirements.

### Security Best Practices

1. ? Use Managed Identity when possible
2. ? Store secrets in Azure Key Vault
3. ? Enable Application Insights for audit logs
4. ? Implement least-privilege RBAC
5. ? Use private endpoints for storage

## ?? Monitoring

### Application Insights

- Request telemetry
- Dependency tracking
- Custom metrics
- Error tracking

### Native APIs

- Action history: `GET /machineactions`
- Action status: `GET /machineactions/{id}`
- Cancellation: `POST /machineactions/{id}/cancel`

## ?? Contributing

We welcome contributions! Please see [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

### Development Setup

```bash
# Clone repo
git clone https://github.com/akefallonitis/sentryxdr.git

# Install dependencies
dotnet restore

# Run tests
dotnet test

# Run locally
func start
```

## ?? License

This project is licensed under the MIT License - see [LICENSE](LICENSE) file for details.

## ?? Acknowledgments

- Microsoft Security APIs
- Azure Functions team
- Open-source community

## ?? Support

- **Issues**: [GitHub Issues](https://github.com/akefallonitis/sentryxdr/issues)
- **Discussions**: [GitHub Discussions](https://github.com/akefallonitis/sentryxdr/discussions)
- **Documentation**: [Wiki](https://github.com/akefallonitis/sentryxdr/wiki)

## ??? Roadmap

- [ ] Azure Workbook control plane
- [ ] Microsoft Defender for Cloud Apps (MCAS) integration
- [ ] Microsoft Defender for Identity (MDI) integration
- [ ] Custom playbook engine
- [ ] SOAR platform integrations

---

**Built with ?? for the security community**

**Star ? this repo if you find it useful!**
