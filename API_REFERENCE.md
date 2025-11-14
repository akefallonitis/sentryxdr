# SentryXDR API Reference

## Overview

SentryXDR is a production-ready, multi-tenant XDR orchestration platform for Microsoft Security products.

**Version**: 1.0.0  
**Base URL**: `https://<your-function-app>.azurewebsites.net`  
**Authentication**: Azure Managed Identity + Multi-tenant OAuth2

---

## OpenAPI/Swagger Specification

### Access Swagger UI
```
https://<your-function-app>.azurewebsites.net/api/swagger/ui
```

### Download OpenAPI JSON
```
https://<your-function-app>.azurewebsites.net/api/swagger.json
```

---

## API Endpoints

### 1. Submit Remediation Action

**Endpoint**: `POST /api/v1/remediation/submit`

**Description**: Submit a single remediation action

**Headers**:
```json
{
  "Content-Type": "application/json",
  "x-tenant-id": "<tenant-id>"
}
```

**Request Body**:
```json
{
  "requestId": "guid",
  "tenantId": "tenant-guid",
  "incidentId": "incident-guid",
  "platform": "MDE|MDO|EntraID|Intune|Azure",
  "action": "IsolateDevice|BlockUser|...",
  "parameters": {
    "machineId": "device-id",
    "comment": "Remediation comment"
  },
  "initiatedBy": "user@domain.com",
  "priority": "Critical|High|Medium|Low",
  "justification": "Security incident response"
}
```

**Response** (200 OK):
```json
{
  "requestId": "guid",
  "tenantId": "tenant-guid",
  "incidentId": "incident-guid",
  "success": true,
  "status": "Completed|InProgress|Failed",
  "message": "Device isolated successfully",
  "details": {
    "machineId": "device-id",
    "actionId": "action-guid"
  },
  "actionId": "action-guid",
  "completedAt": "2024-01-01T00:00:00Z",
  "duration": "00:00:05"
}
```

**Error Response** (400/500):
```json
{
  "requestId": "guid",
  "success": false,
  "status": "Failed",
  "message": "Error message",
  "errors": ["Detailed error 1", "Detailed error 2"],
  "completedAt": "2024-01-01T00:00:00Z"
}
```

---

### 2. Submit Batch Actions

**Endpoint**: `POST /api/v1/remediation/batch`

**Description**: Submit multiple remediation actions

**Request Body**:
```json
{
  "batchId": "guid",
  "tenantId": "tenant-guid",
  "incidentId": "incident-guid",
  "actions": [
    {
      "platform": "MDE",
      "action": "IsolateDevice",
      "parameters": { "machineId": "device-1" }
    },
    {
      "platform": "EntraID",
      "action": "DisableUser",
      "parameters": { "userId": "user-1" }
    }
  ],
  "initiatedBy": "user@domain.com"
}
```

**Response**:
```json
{
  "batchId": "guid",
  "totalActions": 2,
  "successful": 2,
  "failed": 0,
  "results": [
    {
      "requestId": "guid-1",
      "success": true,
      "message": "Device isolated"
    },
    {
      "requestId": "guid-2",
      "success": true,
      "message": "User disabled"
    }
  ]
}
```

---

### 3. Get Action Status

**Endpoint**: `GET /api/v1/remediation/{actionId}/status`

**Description**: Get status of a remediation action

**Response**:
```json
{
  "actionId": "guid",
  "status": "Completed|InProgress|Failed|Cancelled",
  "platform": "MDE",
  "action": "IsolateDevice",
  "startedAt": "2024-01-01T00:00:00Z",
  "completedAt": "2024-01-01T00:00:05Z",
  "duration": "00:00:05"
}
```

---

### 4. Cancel Action

**Endpoint**: `POST /api/v1/remediation/{actionId}/cancel`

**Description**: Cancel a pending/in-progress action

**Request Body**:
```json
{
  "comment": "Cancellation reason"
}
```

**Response**:
```json
{
  "actionId": "guid",
  "success": true,
  "message": "Action cancelled successfully",
  "cancelledAt": "2024-01-01T00:00:00Z"
}
```

---

### 5. Get Action History

**Endpoint**: `GET /api/v1/remediation/history`

**Description**: Get action history (uses native Microsoft APIs)

**Query Parameters**:
- `tenantId`: Filter by tenant (required)
- `incidentId`: Filter by incident (optional)
- `platform`: Filter by platform (optional)
- `startDate`: Start date (optional)
- `endDate`: End date (optional)
- `pageSize`: Results per page (default: 50)
- `pageToken`: Pagination token (optional)

**Response**:
```json
{
  "totalCount": 100,
  "pageSize": 50,
  "nextPageToken": "token",
  "actions": [
    {
      "actionId": "guid",
      "platform": "MDE",
      "action": "IsolateDevice",
      "status": "Completed",
      "timestamp": "2024-01-01T00:00:00Z"
    }
  ]
}
```

---

## Supported Actions (237 Total)

### MDE (Microsoft Defender for Endpoint)
- `IsolateDevice`
- `ReleaseDeviceFromIsolation`
- `RestrictAppExecution`
- `RemoveAppRestriction`
- `RunAntivirusScan`
- `StopAndQuarantineFile`
- `CollectInvestigationPackage`
- `InitiateAutomatedInvestigation`
- `CancelMachineAction`
- ... (37 total MDE actions)

### MDO (Microsoft Defender for Office 365)
- `SoftDeleteEmail`
- `HardDeleteEmail`
- `MoveEmailToJunkFolder`
- `QuarantineEmail`
- `SubmitFileForDetonation`
- `SubmitURLForDetonation`
- `RemoveEmailFromQuarantine`
- ... (35 total MDO actions)

### EntraID (Microsoft Entra ID)
- `DisableUser`
- `EnableUser`
- `ForcePasswordReset`
- `RevokeUserSessions`
- `BlockUserSignIn`
- ... (26 total EntraID actions)

### Intune (Microsoft Intune)
- `WipeDevice`
- `RemoteLockDevice`
- `ResetPasscode`
- `RetireDevice`
- ... (28 total Intune actions)

### Azure Security
- `StopVM`
- `IsolateNetworkInterface`
- `BlockIPAddress`
- `RegenerateStorageKeys`
- ... (15 total Azure actions)

### Threat Intelligence
- `SubmitIOC`
- `UpdateIOC`
- `DeleteIOC`
- `BatchSubmitIOCs`
- ... (8 total TI actions)

### Advanced Hunting
- `RunAdvancedHuntingQuery`
- `ScheduleHuntingQuery`
- ... (2 total hunting actions)

### Live Response
- `InitiateLiveResponseSession`
- `RunLiveResponseCommand`
- `RunLiveResponseScript`
- `GetFileFromDevice`
- `PutFileToDevice`
- ... (7 total live response actions)

---

## Authentication

### Managed Identity (Recommended)
```csharp
// Automatically handled by Azure Function
// No credentials needed in code
```

### Multi-Tenant OAuth2
```
POST https://login.microsoftonline.com/{tenant-id}/oauth2/v2.0/token
Content-Type: application/x-www-form-urlencoded

grant_type=client_credentials
&client_id={client-id}
&client_secret={client-secret}
&scope=https://graph.microsoft.com/.default
```

---

## Required Permissions

### Microsoft Graph API
- `SecurityIncident.ReadWrite.All`
- `SecurityAlert.ReadWrite.All`
- `User.ReadWrite.All`
- `Directory.ReadWrite.All`
- `DeviceManagementManagedDevices.ReadWrite.All`
- `ThreatSubmission.ReadWrite.All`

### MDE API
- `Machine.ReadWrite.All`
- `Machine.LiveResponse`
- `Machine.CollectForensics`
- `Ti.ReadWrite.All`
- `AdvancedQuery.Read.All`
- `Alert.ReadWrite.All`

### Azure RBAC
- Custom Role: `SentryXDR Remediation Operator`

---

## Error Codes

| Code | Description |
|------|-------------|
| 200 | Success |
| 400 | Bad Request - Invalid parameters |
| 401 | Unauthorized - Invalid/missing auth |
| 403 | Forbidden - Insufficient permissions |
| 404 | Not Found - Resource doesn't exist |
| 429 | Too Many Requests - Rate limited |
| 500 | Internal Server Error |
| 503 | Service Unavailable - Temporary issue |

---

## Rate Limiting

- **Per-tenant**: 100 requests/minute
- **Per-action**: Follows Microsoft API limits
- **Batch operations**: Max 50 actions per batch

---

## Examples

### PowerShell
```powershell
$body = @{
    tenantId = "tenant-guid"
    platform = "MDE"
    action = "IsolateDevice"
    parameters = @{
        machineId = "device-guid"
        comment = "Security incident response"
    }
} | ConvertTo-Json

Invoke-RestMethod -Uri "https://<app>.azurewebsites.net/api/v1/remediation/submit" `
    -Method POST `
    -Body $body `
    -ContentType "application/json" `
    -Headers @{"x-tenant-id" = "tenant-guid"}
```

### Python
```python
import requests

url = "https://<app>.azurewebsites.net/api/v1/remediation/submit"
headers = {
    "Content-Type": "application/json",
    "x-tenant-id": "tenant-guid"
}
body = {
    "tenantId": "tenant-guid",
    "platform": "MDE",
    "action": "IsolateDevice",
    "parameters": {
        "machineId": "device-guid",
        "comment": "Security incident response"
    }
}

response = requests.post(url, json=body, headers=headers)
print(response.json())
```

### cURL
```bash
curl -X POST https://<app>.azurewebsites.net/api/v1/remediation/submit \
  -H "Content-Type: application/json" \
  -H "x-tenant-id: tenant-guid" \
  -d '{
    "tenantId": "tenant-guid",
    "platform": "MDE",
    "action": "IsolateDevice",
    "parameters": {
      "machineId": "device-guid",
      "comment": "Security incident response"
    }
  }'
```

---

## Storage Usage

### Native Microsoft APIs (Default)
- **Action History**: `GET /machineactions` (MDE native API)
- **Tracking**: Native polling with `GET /machineactions/{id}`
- **Cancellation**: Native `POST /machineactions/{id}/cancel`
- **Audit Logs**: Application Insights

### Azure Storage (Only When Required)
- **Live Response**: Command outputs, collected files
- **Investigation Packages**: Forensics packages
- **IR Scripts**: PowerShell script library

**Storage Connection**: Retrieved from Function App environment variables:
```
AzureWebJobsStorage
```

---

## Support

- **Issues**: https://github.com/akefallonitis/sentryxdr/issues
- **Documentation**: https://github.com/akefallonitis/sentryxdr/docs
- **License**: MIT
