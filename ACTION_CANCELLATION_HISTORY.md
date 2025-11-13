# ACTION CANCELLATION & HISTORY TRACKING

## Overview
Complete implementation of action cancellation and comprehensive history tracking based on Microsoft APIs.

---

## ? NEW FEATURES ADDED

### 1. Action Cancellation
Cancel in-progress remediation actions with full audit trail.

### 2. History Tracking
Complete history of all remediation actions stored in Azure Table Storage and Blob Storage.

### 3. Statistics Dashboard
Real-time statistics and analytics on remediation actions.

### 4. Query & Search
Advanced filtering and search capabilities across history.

---

## ?? API ENDPOINTS

### Cancel Remediation Action
```http
POST /api/xdr/cancel
Content-Type: application/json
x-functions-key: YOUR_KEY

{
  "orchestrationId": "abc123-orchestration-id",
  "requestId": "req-123",
  "cancelledBy": "admin@company.com",
  "reason": "False positive - device not compromised",
  "forceTerminate": false
}
```

**Response**:
```json
{
  "orchestrationId": "abc123-orchestration-id",
  "requestId": "req-123",
  "success": true,
  "status": "Cancelled",
  "message": "Remediation action cancelled successfully",
  "cancelledAt": "2024-01-15T10:30:00Z",
  "cancelledBy": "admin@company.com"
}
```

---

### Get History Entry
```http
GET /api/xdr/history/{requestId}?tenantId={tenantId}
x-functions-key: YOUR_KEY
```

**Response**:
```json
{
  "historyId": "hist-123",
  "requestId": "req-123",
  "orchestrationId": "orch-123",
  "tenantId": "tenant-guid",
  "incidentId": "INC-2024-001",
  "platform": "MDE",
  "action": "IsolateDevice",
  "status": "Completed",
  "parameters": {
    "deviceId": "device-guid"
  },
  "initiatedBy": "soc@company.com",
  "priority": "Critical",
  "justification": "Ransomware detected",
  "initiatedAt": "2024-01-15T10:00:00Z",
  "completedAt": "2024-01-15T10:01:30Z",
  "duration": "00:01:30",
  "success": true,
  "details": {...}
}
```

---

### Query History
```http
POST /api/xdr/history/query
Content-Type: application/json
x-functions-key: YOUR_KEY

{
  "tenantId": "tenant-guid",
  "platform": "MDE",
  "action": "IsolateDevice",
  "status": "Completed",
  "fromDate": "2024-01-01T00:00:00Z",
  "toDate": "2024-01-31T23:59:59Z",
  "pageSize": 50,
  "pageNumber": 1,
  "sortBy": "InitiatedAt",
  "sortOrder": "desc"
}
```

**Response**:
```json
{
  "totalCount": 150,
  "pageSize": 50,
  "pageNumber": 1,
  "totalPages": 3,
  "items": [
    {
      "requestId": "req-123",
      "platform": "MDE",
      "action": "IsolateDevice",
      "status": "Completed",
      "initiatedAt": "2024-01-15T10:00:00Z",
      "success": true
    }
    // ... more items
  ]
}
```

---

### Get Statistics
```http
GET /api/xdr/history/statistics?tenantId={tenantId}&fromDate={date}&toDate={date}
x-functions-key: YOUR_KEY
```

**Response**:
```json
{
  "totalActions": 1250,
  "successfulActions": 1100,
  "failedActions": 100,
  "cancelledActions": 50,
  "inProgressActions": 5,
  "actionsByPlatform": {
    "MDE": 500,
    "MDO": 300,
    "EntraID": 250,
    "Intune": 200
  },
  "actionsByType": {
    "IsolateDevice": 200,
    "SoftDeleteEmail": 150,
    "DisableUserAccount": 100
  },
  "actionsByTenant": {
    "tenant-1": 800,
    "tenant-2": 450
  },
  "averageCompletionTime": 45.5,
  "successRate": 88.0
}
```

---

### Purge Old History
```http
DELETE /api/xdr/history/purge?tenantId={tenantId}&beforeDate=2023-12-31
x-functions-key: YOUR_KEY
```

**Response**:
```json
{
  "success": true,
  "purgedCount": 543,
  "message": "Purged 543 orchestration instances older than 2023-12-31"
}
```

---

## ?? STORAGE ARCHITECTURE

### Table Storage (Fast Queries)
**Table**: `XDRRemediationHistory`

**Partition Key**: `TenantId`  
**Row Key**: `RequestId`

**Columns**:
- OrchestrationId
- IncidentId
- Platform
- Action
- Status
- InitiatedBy
- Priority
- InitiatedAt
- CompletedAt
- CancelledAt
- CancelledBy
- Success
- ErrorMessage

### Blob Storage (Detailed History)
**Container**: `xdr-history`

**Structure**: `{tenantId}/{yyyy}/{MM}/{dd}/{requestId}.json`

**Contains**:
- Complete request parameters
- Detailed response
- Error stack traces
- Full audit trail

---

## ?? AUTHENTICATION & AUTHORIZATION

### Graph API Support
? **Graph v1.0** - `https://graph.microsoft.com/v1.0`
- MDO actions
- Entra ID actions
- Intune actions

? **Graph Beta** - `https://graph.microsoft.com/beta`
- Advanced features
- MCAS actions
- Preview features

### Required Permissions

#### Graph API (v1.0 & Beta)
```
SecurityEvents.ReadWrite.All
SecurityActions.ReadWrite.All
User.ReadWrite.All
Directory.ReadWrite.All
DeviceManagementManagedDevices.ReadWrite.All
Mail.ReadWrite
ThreatSubmission.ReadWrite.All
IdentityRiskyUser.ReadWrite.All
RoleManagement.ReadWrite.Directory
```

#### MDE API
```
Machine.ReadWrite.All
Alert.ReadWrite.All
File.ReadWrite.All
```

#### Azure Management API
```
Contributor or Owner role
```

---

## ?? USE CASES

### 1. Cancel False Positive
```bash
# Device isolated by mistake
curl -X POST https://your-app.azurewebsites.net/api/xdr/cancel \
  -H "x-functions-key: YOUR_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "orchestrationId": "orch-123",
    "cancelledBy": "admin@company.com",
    "reason": "False positive - device is clean"
  }'
```

### 2. Query All Failed Actions
```bash
curl -X POST https://your-app.azurewebsites.net/api/xdr/history/query \
  -H "x-functions-key: YOUR_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "status": "Failed",
    "fromDate": "2024-01-01T00:00:00Z",
    "pageSize": 100
  }'
```

### 3. Get Tenant Statistics
```bash
curl "https://your-app.azurewebsites.net/api/xdr/history/statistics?tenantId=tenant-guid" \
  -H "x-functions-key: YOUR_KEY"
```

### 4. Audit Compliance Report
```bash
# Get all actions for specific incident
curl -X POST https://your-app.azurewebsites.net/api/xdr/history/query \
  -H "x-functions-key: YOUR_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "incidentId": "INC-2024-001",
    "pageSize": 1000
  }'
```

---

## ?? AUTOMATIC HISTORY TRACKING

History is automatically tracked for all actions:

```csharp
// Orchestrator automatically records:
// 1. Action initiated
// 2. Validation result
// 3. Worker execution
// 4. Final result
// 5. Any errors or cancellations

var historyEntry = new RemediationHistoryEntry
{
    RequestId = request.RequestId,
    OrchestrationId = context.InstanceId,
    TenantId = request.TenantId,
    Status = "InProgress",
    InitiatedAt = DateTime.UtcNow
};

await context.CallActivityAsync("RecordHistoryActivity", historyEntry);
```

---

## ?? STATISTICS & ANALYTICS

### Real-Time Metrics
- Total actions (all time)
- Success rate (percentage)
- Failed actions count
- Cancelled actions count
- In-progress actions count
- Average completion time (seconds)

### Breakdown by Dimension
- **Platform**: MDE, MDO, EntraID, Intune, MCAS, Azure, MDI
- **Action Type**: All 219 actions
- **Tenant**: Per-tenant statistics
- **Time Period**: Custom date ranges

---

## ?? TESTING

### Test Cancellation
```powershell
# Start a remediation
$response = Invoke-RestMethod -Method POST `
  -Uri "https://your-app.azurewebsites.net/api/xdr/remediate" `
  -Headers @{"x-functions-key"="YOUR_KEY"} `
  -Body (@{
    tenantId = "tenant-guid"
    platform = "MDE"
    action = "IsolateDevice"
    parameters = @{ deviceId = "device-guid" }
  } | ConvertTo-Json)

$orchestrationId = $response.orchestrationId

# Cancel it
Invoke-RestMethod -Method POST `
  -Uri "https://your-app.azurewebsites.net/api/xdr/cancel" `
  -Headers @{"x-functions-key"="YOUR_KEY"} `
  -Body (@{
    orchestrationId = $orchestrationId
    cancelledBy = "test@example.com"
    reason = "Testing cancellation"
  } | ConvertTo-Json)
```

### Test History Query
```powershell
$stats = Invoke-RestMethod -Method GET `
  -Uri "https://your-app.azurewebsites.net/api/xdr/history/statistics" `
  -Headers @{"x-functions-key"="YOUR_KEY"}

Write-Host "Total Actions: $($stats.totalActions)"
Write-Host "Success Rate: $($stats.successRate)%"
```

---

## ?? SECURITY CONSIDERATIONS

### Authorization
- Function-level keys required
- Azure AD authentication supported
- Role-based access control (RBAC) recommended

### Data Protection
- History data encrypted at rest (Azure Storage)
- PII redaction support
- Retention policies configurable

### Audit Trail
- All cancellations logged
- Cancellation reason required
- Who cancelled and when tracked

---

## ?? BETA API ACTIONS SUPPORTED

### Graph Beta Endpoints
All actions that require Beta endpoints are fully supported:

1. **Advanced Conditional Access**
   - Complex CA policy management
   - Authentication contexts
   - Named locations

2. **Identity Protection (Beta)**
   - Risk event investigations
   - User risk state management
   - Sign-in risk policies

3. **MCAS Operations**
   - Cloud app discovery
   - OAuth app governance
   - Activity policies

4. **Advanced PIM**
   - Just-in-time role activation
   - PIM alerts management
   - Role eligibility schedules

---

## ?? QUERY EXAMPLES

### Get All Actions for User
```json
{
  "initiatedBy": "soc.analyst@company.com",
  "pageSize": 100
}
```

### Get All Cancelled Actions
```json
{
  "status": "Cancelled",
  "fromDate": "2024-01-01T00:00:00Z"
}
```

### Get Platform-Specific Actions
```json
{
  "platform": "MDE",
  "action": "IsolateDevice",
  "success": true
}
```

### Get Actions by Date Range
```json
{
  "fromDate": "2024-01-01T00:00:00Z",
  "toDate": "2024-01-31T23:59:59Z",
  "sortBy": "CompletedAt",
  "sortOrder": "desc"
}
```

---

## ? VERIFICATION CHECKLIST

- [x] Action cancellation implemented
- [x] History tracking in Table Storage
- [x] Detailed history in Blob Storage
- [x] Statistics and analytics
- [x] Query with filters
- [x] Pagination support
- [x] Graph v1.0 authentication
- [x] Graph Beta authentication
- [x] MDE API authentication
- [x] Azure Management authentication
- [x] Automatic history recording
- [x] HTTP endpoints for all operations
- [x] Comprehensive error handling
- [x] Logging and telemetry

---

## ?? DEPLOYMENT

All features are automatically deployed with the ARM template:

```json
{
  "type": "Microsoft.Storage/storageAccounts",
  "resources": [
    {
      "type": "tableServices",
      "name": "default",
      "properties": {
        "tables": [
          { "name": "XDRRemediationHistory" }
        ]
      }
    },
    {
      "type": "blobServices",
      "properties": {
        "containers": [
          { "name": "xdr-history" }
        ]
      }
    }
  ]
}
```

**Status**: ? **PRODUCTION READY**  
**Features**: ? **ALL IMPLEMENTED**  
**APIs**: ? **v1.0 + BETA SUPPORTED**
