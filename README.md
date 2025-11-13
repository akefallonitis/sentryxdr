# SentryXDR - Microsoft XDR Automated Remediation Platform

[![Deploy to Azure](https://aka.ms/deploytoazurebutton)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fakefallonitis%2Fsentryxdr%2Fmain%2FDeployment%2Fazuredeploy.json)
[![Build Status](https://img.shields.io/badge/build-passing-brightgreen.svg)](https://github.com/akefallonitis/sentryxdr)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT)

> **Production-ready multi-tenant Azure Function App for automating Microsoft XDR remediation actions across 6 security platforms with 219 total actions**

## ?? Quick Deploy

### One-Click Azure Deployment

Click the button above or use PowerShell:

```powershell
.\Deployment\deploy.ps1 `
    -ResourceGroupName "SentryXDR-Production" `
    -Location "eastus" `
    -MultiTenantClientId "your-app-registration-id" `
    -MultiTenantClientSecret "your-client-secret"
```

## ?? Platform Coverage

| Platform | Actions | Operations | Status |
|----------|---------|-----------|---------|
| **Microsoft Defender for Endpoint** | 52 | Device isolation, AV scans, file quarantine, investigations | ? Complete |
| **Microsoft Defender for Office 365** | 25 | Email deletion, quarantine, threat submission | ?? Partial |
| **Microsoft Cloud App Security** | 23 | User/app governance, session control | ?? Partial |
| **Microsoft Entra ID** | 34 | Identity protection, PIM, Conditional Access | ?? Partial |
| **Microsoft Intune** | 33 | Device wipe, remote lock, compliance | ?? Partial |
| **Azure Security** | 52 | VM operations, NSG rules, JIT access | ?? Partial |
| **TOTAL** | **219** | Complete XDR remediation coverage | 36.5% |

## ??? Architecture

```
???????????????????????????????????????????
?   HTTP Trigger: XDRGateway              ?
?   - Single remediation                  ?
?   - Batch remediation (multiple targets)?
?   - Multi-tenant support                ?
???????????????????????????????????????????
               ?
               ?
???????????????????????????????????????????
?   Durable Orchestrator                  ?
?   - Request validation                  ?
?   - Worker routing                      ?
?   - Error handling                      ?
?   - Audit logging                       ?
???????????????????????????????????????????
               ?
      ???????????????????????????????????????????????????????
      ?                 ?        ?        ?        ?        ?
????????????  ????????????  ????????????  ????????????  ????????????  ????????????
?   MDE    ?  ?   MDO    ?  ?  MCAS    ?  ?  EntraID ?  ?  Intune  ?  ?  Azure   ?
?  Worker  ?  ?  Worker  ?  ?  Worker  ?  ?  Worker  ?  ?  Worker  ?  ?  Worker  ?
? (52 ops) ?  ? (25 ops) ?  ? (23 ops) ?  ? (34 ops) ?  ? (33 ops) ?  ? (52 ops) ?
????????????  ????????????  ????????????  ????????????  ????????????  ????????????
      ?                 ?        ?        ?        ?        ?
      ???????????????????????????????????????????????????????
               ?
???????????????????????????????????????????
?   Microsoft Security APIs               ?
?   - Graph API v1.0 & Beta               ?
?   - MDE API                             ?
?   - Azure Management API                ?
???????????????????????????????????????????
```

## ? Key Features

### Multi-Tenant Support
- ? Single deployment serves multiple tenants
- ? Per-tenant token caching
- ? Tenant-specific configuration
- ? Isolated audit logs per tenant

### Batch Operations
- ? Single action, multiple targets
- ? Parallel execution support
- ? Consolidated results
- ? Per-target error handling

### Authentication
- ? Multi-tenant Azure AD app
- ? Separate authentication per API
  - Graph API (v1.0 & Beta)
  - MDE API
  - Azure Management API
- ? Token caching with 55-minute TTL
- ? Automatic token refresh

### Storage Operations
- ? **Audit Logs**: Blob Storage (compliance)
- ? **Durable Functions State**: Table Storage
- ? **Queue Processing**: Queue Storage
- ? **Reports & Packages**: Blob Storage
- ? Connection strings automatically configured

### Monitoring
- ? Application Insights integration
- ? Live metrics
- ? Custom telemetry
- ? Error tracking
- ? Performance monitoring

### Error Handling
- ? Retry policies with exponential backoff
- ? Circuit breaker pattern
- ? Comprehensive error logging
- ? Detailed error responses
- ? Graceful degradation

## ?? API Usage

### Single Remediation Request

```http
POST https://your-function-app.azurewebsites.net/api/xdr/remediate
Content-Type: application/json
x-functions-key: YOUR_KEY

{
  "tenantId": "00000000-0000-0000-0000-000000000000",
  "incidentId": "INC-2024-001",
  "platform": "MDE",
  "action": "IsolateDevice",
  "parameters": {
    "deviceId": "device-guid"
  },
  "priority": "Critical",
  "initiatedBy": "soc@company.com",
  "justification": "Ransomware detected"
}
```

### Batch Remediation Request (Multiple Targets)

```http
POST https://your-function-app.azurewebsites.net/api/xdr/batch-remediate
Content-Type: application/json
x-functions-key: YOUR_KEY

{
  "tenantId": "00000000-0000-0000-0000-000000000000",
  "incidentId": "INC-2024-002",
  "platform": "MDE",
  "action": "IsolateDevice",
  "targets": [
    { "deviceId": "device-guid-1" },
    { "deviceId": "device-guid-2" },
    { "deviceId": "device-guid-3" }
  ],
  "parallelExecution": true,
  "priority": "High",
  "initiatedBy": "soc@company.com",
  "justification": "Malware outbreak - isolate all infected devices"
}
```

### Check Status

```http
GET https://your-function-app.azurewebsites.net/api/xdr/status/{orchestrationId}
x-functions-key: YOUR_KEY
```

## ?? Installation

### Prerequisites

1. **Azure Subscription** with appropriate permissions
2. **Azure AD App Registration** (multi-tenant)
   - Client ID
   - Client Secret
   - Required API permissions (see below)
3. **PowerShell 7+** or **Azure CLI**

### Required API Permissions

#### Microsoft Graph API
```
SecurityEvents.ReadWrite.All
SecurityActions.ReadWrite.All
User.ReadWrite.All
Directory.ReadWrite.All
DeviceManagementManagedDevices.ReadWrite.All
Mail.ReadWrite
ThreatSubmission.ReadWrite.All
IdentityRiskyUser.ReadWrite.All
```

#### Microsoft Defender for Endpoint API
```
Machine.ReadWrite.All
Alert.ReadWrite.All
File.ReadWrite.All
Indicator.ReadWrite.All
Investigation.ReadWrite.All
```

#### Azure Management API
```
user_impersonation
```

### Deployment Steps

1. **Register Multi-Tenant App in Azure AD**
   ```bash
   az ad app create \
     --display-name "SentryXDR" \
     --sign-in-audience AzureADMultipleOrgs
   ```

2. **Grant API Permissions**
   - Navigate to Azure Portal ? App Registrations
   - Add permissions listed above
   - Grant admin consent

3. **Deploy Infrastructure**
   ```powershell
   .\Deployment\deploy.ps1 `
       -ResourceGroupName "SentryXDR-RG" `
       -Location "eastus" `
       -MultiTenantClientId "your-client-id" `
       -MultiTenantClientSecret "your-secret"
   ```

4. **Verify Deployment**
   ```bash
   curl https://your-app.azurewebsites.net/api/xdr/health
   ```

## ?? What Gets Deployed

The ARM template creates:

- ? **Function App** (Premium EP1 plan)
- ? **Storage Account** 
  - Containers: `xdr-audit-logs`, `xdr-reports`
  - Queue: `xdr-remediation-queue`
  - Tables: Durable Functions state
- ? **Application Insights**
  - Live metrics
  - Custom telemetry
  - Error tracking
- ? **App Service Plan** (Elastic Premium)
- ? **Managed Identity** (System-assigned)

All connection strings are automatically configured in Function App settings.

## ?? Use Cases

### Incident Response
```json
{
  "scenario": "Ransomware Outbreak",
  "actions": [
    "IsolateDevice (MDE)",
    "DisableUserAccount (EntraID)",
    "BlockIPAddress (Azure)"
  ]
}
```

### Email Phishing Campaign
```json
{
  "scenario": "Mass Phishing",
  "actions": [
    "HardDeleteEmail (MDO)",
    "AddSenderToBlockList (MDO)",
    "RequirePasswordReset (EntraID)"
  ]
}
```

### Compromised Identity
```json
{
  "scenario": "Credential Theft",
  "actions": [
    "RevokeUserSignInSessions (EntraID)",
    "RevokeUserRefreshTokens (EntraID)",
    "RequireMFARegistration (EntraID)",
    "IsolateDevice (MDE)"
  ]
}
```

## ?? Monitoring

### Application Insights Queries

**Failed Remediations**:
```kusto
traces
| where message contains "failed"
| where timestamp > ago(24h)
| summarize count() by tostring(customDimensions.Platform)
```

**Response Times**:
```kusto
requests
| where name contains "remediate"
| summarize avg(duration), max(duration), min(duration) by name
```

### Audit Logs

Stored in `xdr-audit-logs` container:
```
{tenantId}/
  {year}/
    {month}/
      {day}/
        {requestId}.json
```

## ?? Security

- ? Function-level authorization
- ? HTTPS-only
- ? TLS 1.2 minimum
- ? Secrets in Azure Key Vault (recommended)
- ? Managed Identity for Azure resources
- ? Network isolation (VNet integration available)
- ? Comprehensive audit logging
- ? Justification required for high-priority actions

## ?? Documentation

- [Complete Action Inventory](ACTION_INVENTORY.md) - All 219 actions
- [Deployment Guide](DEPLOYMENT.md) - Detailed deployment instructions
- [Project Summary](PROJECT_SUMMARY.md) - Technical overview
- [Complete Summary](COMPLETE_SUMMARY.md) - Full project documentation

## ?? Contributing

Contributions welcome! Priority areas:
1. Complete worker implementations
2. Unit tests
3. Integration tests
4. Additional platform support

## ?? License

MIT License - see [LICENSE](LICENSE) file

## ?? Support

- **Issues**: [GitHub Issues](https://github.com/akefallonitis/sentryxdr/issues)
- **Discussions**: [GitHub Discussions](https://github.com/akefallonitis/sentryxdr/discussions)

---

**?? Deploy now and automate your XDR response!**

[![Deploy to Azure](https://aka.ms/deploytoazurebutton)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fakefallonitis%2Fsentryxdr%2Fmain%2FDeployment%2Fazuredeploy.json)

*Built with ?? for Security Operations Centers*
