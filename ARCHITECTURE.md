# ??? SentryXDR - System Architecture

**Version:** 1.0.0  
**Platform:** Azure Functions (Isolated Worker)  
**Language:** C# 11 / .NET 8  
**Architecture:** Multi-Tenant, Event-Driven, Microservices

---

## ?? **High-Level Architecture**

```
???????????????????????????????????????????????????????????????????
?                         CLIENT LAYER                             ?
?  (SOAR, Microsoft Sentinel, Custom Apps, Direct API Calls)      ?
???????????????????????????????????????????????????????????????????
                            ?
                            ? HTTPS/REST API
                            ?
???????????????????????????????????????????????????????????????????
?                    AZURE FUNCTION APP                            ?
?  ????????????????????????????????????????????????????????????   ?
?  ?         HTTP Trigger Functions (25+)                     ?   ?
?  ?  SubmitRemediation ? GetStatus ? Health ? Swagger       ?   ?
?  ????????????????????????????????????????????????????????????   ?
?                         ?                                        ?
?  ????????????????????????????????????????????????????????????   ?
?  ?         Orchestration & Validation Layer                 ?   ?
?  ?  RemediationOrchestrator ? RemediationValidator          ?   ?
?  ????????????????????????????????????????????????????????????   ?
?                         ?                                        ?
?  ????????????????????????????????????????????????????????????   ?
?  ?         Worker Services Layer (10 Platforms)             ?   ?
?  ?  MDE ? MDO ? Entra ID ? Intune ? Azure ? MCAS ? etc    ?   ?
?  ????????????????????????????????????????????????????????????   ?
?                         ?                                        ?
?  ????????????????????????????????????????????????????????????   ?
?  ?         Multi-Tenant Authentication Service              ?   ?
?  ?  Per-Tenant Token Management ? Token Caching            ?   ?
?  ????????????????????????????????????????????????????????????   ?
??????????????????????????????????????????????????????????????????
                         ?
        ???????????????????????????????????????????????
        ?                ?                            ?
        ?                ?                            ?
????????????????? ???????????????? ??????????????????????????
?  Microsoft    ? ?  Microsoft   ? ?  Azure Management      ?
?  Graph API    ? ?  Defender    ? ?  API                   ?
?  (50+ perms)  ? ?  APIs        ? ?  (Resource Management) ?
????????????????? ???????????????? ??????????????????????????
```

---

## ?? **Core Components**

### **1. Function App (Entry Point)**
- **Runtime:** Azure Functions Isolated Worker (.NET 8)
- **Trigger:** HTTP (RESTful API)
- **Endpoints:**
  - `POST /api/xdr/remediate` - Submit remediation request
  - `GET /api/xdr/status/{requestId}` - Get request status
  - `GET /api/xdr/health` - Health check
  - `GET /api/swagger/ui` - Swagger documentation

### **2. Orchestration Layer**
- **RemediationOrchestrator**
  - Routes requests to appropriate worker service
  - Handles retries and error recovery
  - Manages async operations
  
- **RemediationValidator**
  - Validates request parameters
  - Checks permissions
  - Validates tenant access

### **3. Worker Services (Platform-Specific)**
10 platform implementations:

| Service | Platform | Actions |
|---------|----------|---------|
| `MDEWorkerService` | Microsoft Defender for Endpoint | 24 |
| `MDOApiService` | Microsoft Defender for Office 365 | 18 |
| `EntraIDApiService` | Microsoft Entra ID | 18 |
| `IntuneApiService` | Microsoft Intune | 15 |
| `AzureWorkerService` | Azure Infrastructure | 25 |
| `MCASApiService` | Microsoft Defender for Cloud Apps | 12 |
| `DLPRemediationService` | Data Loss Prevention | 5 |
| `OnPremADService` | On-Premises Active Directory | 5 |
| `IncidentService` | XDR Incident Management | 18 |
| `AdvancedHuntingService` | Advanced Hunting (KQL) | 1 |

**Total:** 150+ Actions

### **4. Authentication Service**
- **IMultiTenantAuthService**
  - Per-tenant token acquisition
  - Token caching (in-memory)
  - Multiple resource support (Graph, MDE, Azure, MCAS)
  - Automatic token refresh

### **5. Storage Services**
- **Forensics Storage**
  - Investigation packages
  - Evidence collection
  - Geo-redundant (GRS)
  
- **Audit Logging**
  - Application Insights
  - Log Analytics Workspace
  - 90-day retention

---

## ?? **Platform Details & Actions**

### **1. Microsoft Defender for Endpoint (MDE)** - 24 Actions

**API:** `https://api.securitycenter.microsoft.com/api/`

| Action Category | Actions | Methods |
|-----------------|---------|---------|
| **Device Isolation** (3) | Isolate, Unisolate, Collect Forensics | `POST /machines/{id}/isolate` |
| **IOC Management** (6) | Submit IP/URL/File Hash/Domain/Certificate/Network | `POST /indicators` |
| **Live Response** (5) | Run Script, Get File, Put File, Delete File, List Files | `POST /machines/{id}/runliveresponse` |
| **Antivirus** (3) | Quick Scan, Full Scan, Update Signatures | `POST /machines/{id}/runAntiVirusScan` |
| **Restrictions** (2) | Restrict App Execution, Unrestrict | `POST /machines/{id}/restrictCodeExecution` |
| **Investigation** (2) | Package Collection, Get Investigation | `POST /machines/{id}/collectInvestigationPackage` |
| **AIR** (3) | Trigger Investigation, Get Status, Get Action Status | `POST /investigations` |

**Permissions Required:**
- `Machine.Read.All`
- `Machine.ReadWrite.All`
- `Machine.Isolate`
- `Machine.RestrictExecution`
- `Machine.Scan`
- `Machine.LiveResponse`
- `Ti.ReadWrite`

---

### **2. Microsoft Defender for Office 365 (MDO)** - 18 Actions

**API:** `https://graph.microsoft.com/v1.0/` + `https://graph.microsoft.com/beta/`

| Action Category | Actions | Methods |
|-----------------|---------|---------|
| **Email Remediation** (4) | Soft Delete, Hard Delete, Move to Junk, Move to Inbox | `POST /users/{id}/messages/{id}/move` |
| **Quarantine** (3) | Quarantine, Release, Delete from Quarantine | `POST /security/threatSubmission/emailThreats` |
| **Mail Forwarding** (3) | Disable, Enable, Get Status | `PATCH /users/{id}/mailboxSettings` (Beta API) |
| **Threat Submission** (4) | Submit Email/URL/File/Attachment | `POST /security/threatSubmission/*Threats` |
| **Analysis** (2) | Analyze Email, Detonate URL | `POST /security/collaboration/analyzedEmails` |
| **Sandbox** (2) | Detonate URL, Detonate File | `POST /security/threatSubmission/urlThreats` |

**Permissions Required:**
- `Mail.Read`
- `Mail.ReadWrite`
- `Mail.Send`
- `MailboxSettings.ReadWrite`
- `ThreatSubmission.ReadWrite.All`
- `SecurityEvents.ReadWrite.All`

---

### **3. Microsoft Entra ID (Azure AD)** - 18 Actions

**API:** `https://graph.microsoft.com/v1.0/`

| Action Category | Actions | Methods |
|-----------------|---------|---------|
| **User Management** (4) | Disable, Enable, Reset Password, Force Sign-Out | `PATCH /users/{id}` |
| **MFA** (2) | Reset MFA, Require Re-register | `DELETE /users/{id}/authentication/methods/{id}` |
| **Session Management** (3) | Revoke Sessions, Revoke Tokens, Force Logout | `POST /users/{id}/revokeSignInSessions` |
| **Conditional Access** (3) | Create Policy, Block Geo Location, Emergency Block | `POST /identity/conditionalAccess/policies` |
| **Risk Management** (3) | Confirm User Compromised, Dismiss Risk, Remediate | `POST /identityProtection/riskyUsers/dismiss` |
| **Role Management** (2) | Remove Admin Role, Revoke OAuth Grants | `DELETE /roleManagement/directory/roleAssignments/{id}` |
| **Device Compliance** (1) | Enforce Device Compliance | `POST /deviceManagement/managedDevices/{id}/retire` |

**Permissions Required:**
- `User.ReadWrite.All`
- `Directory.ReadWrite.All`
- `Policy.ReadWrite.ConditionalAccess`
- `UserAuthenticationMethod.ReadWrite.All`
- `RoleManagement.ReadWrite.Directory`
- `IdentityRiskyUser.ReadWrite.All`

---

### **4. Microsoft Intune** - 15 Actions

**API:** `https://graph.microsoft.com/v1.0/deviceManagement/`

| Action Category | Actions | Methods |
|-----------------|---------|---------|
| **Device Wipe** (3) | Wipe, Retire, Fresh Start | `POST /managedDevices/{id}/wipe` |
| **Lost Mode** (2) | Enable, Disable | `POST /managedDevices/{id}/enableLostMode` |
| **Device Lock** (2) | Remote Lock, Unlock | `POST /managedDevices/{id}/remoteLock` |
| **App Management** (3) | Uninstall App, Install App, Update App | `POST /mobileApps/{id}/assignments` |
| **Compliance** (3) | Force Compliance Check, Update Compliance, Sync Device | `POST /managedDevices/{id}/syncDevice` |
| **Configuration** (2) | Apply Configuration, Reboot Device | `POST /managedDevices/{id}/rebootNow` |

**Permissions Required:**
- `DeviceManagementManagedDevices.ReadWrite.All`
- `DeviceManagementManagedDevices.PrivilegedOperations.All`
- `DeviceManagementApps.ReadWrite.All`
- `DeviceManagementConfiguration.ReadWrite.All`

---

### **5. Azure Infrastructure** - 25 Actions

**API:** `https://management.azure.com/`

| Action Category | Actions | Methods |
|-----------------|---------|---------|
| **VM Isolation** (5) | Isolate (NSG), Unisolate, Shutdown, Restart, Deallocate | `PUT /subscriptions/{id}/resourceGroups/{rg}/providers/Microsoft.Network/networkSecurityGroups/{nsg}/securityRules/{rule}` |
| **Storage Security** (5) | Disable Public Access, Enable Firewall, Rotate Keys, Block Anonymous, Enable Logging | `PATCH /subscriptions/{id}/resourceGroups/{rg}/providers/Microsoft.Storage/storageAccounts/{account}` |
| **Key Vault** (4) | Disable Secret, Rotate Secret, Purge Secret, Enable Soft Delete | `PATCH /subscriptions/{id}/resourceGroups/{rg}/providers/Microsoft.KeyVault/vaults/{vault}/secrets/{secret}` |
| **Network** (5) | Block IP (NSG), Allow IP, Create NSG Rule, Delete NSG Rule, Block Service Tag | `PUT /subscriptions/{id}/resourceGroups/{rg}/providers/Microsoft.Network/networkSecurityGroups/{nsg}/securityRules/{rule}` |
| **Firewall** (3) | Add Deny Rule, Remove Rule, Update Priority | `PUT /subscriptions/{id}/resourceGroups/{rg}/providers/Microsoft.Network/azureFirewalls/{firewall}/networkRuleCollections/{collection}` |
| **RBAC** (3) | Revoke Access, Remove Role Assignment, Deny Assignment | `DELETE /subscriptions/{id}/providers/Microsoft.Authorization/roleAssignments/{id}` |

**Permissions Required:**
- `Contributor` (Azure RBAC)
- `Network Contributor`
- `Storage Account Contributor`
- `Key Vault Administrator`

---

### **6. Microsoft Defender for Cloud Apps (MCAS)** - 12 Actions

**API:** `https://portal.cloudappsecurity.com/api/v1`  
**Authentication:** Custom API Token (not Graph)

| Action Category | Actions | Methods |
|-----------------|---------|---------|
| **OAuth Governance** (4) | Revoke, Disable, Ban, Mark Unusual | `POST /oauth_apps/{id}/revoke` |
| **Session Control** (3) | Block Session, Force Logout, Revoke Tokens | `POST /activities/governance` |
| **File Governance** (4) | Quarantine, Remove Sharing, Trash, Apply Label | `POST /files/{id}/quarantine` |
| **Malware** (1) | Block Malware File | `POST /files/{id}/governance` |

**Permissions Required:**
- MCAS Admin role
- Custom API token with full permissions

---

### **7. Data Loss Prevention (DLP)** - 5 Actions

**API:** `https://graph.microsoft.com/v1.0/`

| Action Category | Actions | Methods |
|-----------------|---------|---------|
| **File Protection** (3) | Remove External Sharing, Break Inheritance, Apply Label | `DELETE /drives/{id}/items/{id}/permissions/{id}` |
| **Sensitivity** (2) | Apply Sensitivity Label, Remove Label | `PATCH /drives/{id}/items/{id}` |

**Permissions Required:**
- `Files.ReadWrite.All`
- `Sites.ReadWrite.All`
- `InformationProtectionPolicy.Read`

---

### **8. On-Premises Active Directory** - 5 Actions

**Method:** Azure Automation Hybrid Worker  
**Protocol:** PowerShell Remoting (WinRM)

| Action Category | Actions | PowerShell Cmdlets |
|-----------------|---------|---------------------|
| **User Management** (2) | Disable User, Force Password Reset | `Disable-ADAccount`, `Set-ADAccountPassword` |
| **Computer Management** (2) | Remove Computer, Disable Computer | `Remove-ADComputer`, `Disable-ADComputer` |
| **Group Management** (1) | Remove from Group | `Remove-ADGroupMember` |

**Requirements:**
- Azure Automation Account
- Hybrid Worker deployed on-premises
- Domain Admin credentials (stored in Key Vault)

---

### **9. XDR Incident Management** - 18 Actions

**API:** `https://graph.microsoft.com/v1.0/security/incidents/`

| Action Category | Actions | Methods |
|-----------------|---------|---------|
| **Lifecycle** (6) | Create, Update, Resolve, Reopen, Close, Archive | `PATCH /security/incidents/{id}` |
| **Assignment** (3) | Assign to User, Assign to Team, Unassign | `PATCH /security/incidents/{id}` |
| **Classification** (4) | Set Severity, Set Status, Add Tags, Set Classification | `PATCH /security/incidents/{id}` |
| **Comments** (3) | Add Comment, Update Comment, Delete Comment | `POST /security/incidents/{id}/comments` |
| **Alerts** (2) | Link Alert, Unlink Alert | `POST /security/incidents/{id}/alerts` |

**Permissions Required:**
- `SecurityIncident.ReadWrite.All`
- `SecurityAlert.ReadWrite.All`

---

### **10. Advanced Hunting** - 1 Action

**API:** `https://api.securitycenter.microsoft.com/api/advancedqueries/run`

| Action | Method | Description |
|--------|--------|-------------|
| **Run KQL Query** | `POST /advancedqueries/run` | Execute custom KQL query across MDE data |

**Permissions Required:**
- `AdvancedQuery.Read.All`

---

## ?? **Authentication & Authorization**

### **Multi-Tenant Token Acquisition**

```csharp
public interface IMultiTenantAuthService
{
    Task<string> GetGraphTokenAsync(string tenantId);
    Task<string> GetMDETokenAsync(string tenantId);
    Task<string> GetAzureManagementTokenAsync(string tenantId);
    Task<string> GetMCASTokenAsync(string tenantId);
}
```

**Token Flow:**
1. Request arrives with `tenantId`
2. Service checks token cache
3. If expired, acquire new token using:
   - Client ID (from App Registration)
   - Client Secret (from Key Vault)
   - Tenant-specific endpoint
4. Cache token (60-minute TTL)
5. Return token to worker service

### **Required App Registration Permissions**

**Total:** 106 permissions across 3 APIs

- **Microsoft Graph API:** 50+ permissions
- **Windows Defender ATP:** 15+ permissions
- **Azure Management:** 3+ RBAC roles

**Setup:**
```powershell
.\Deployment\scripts\Setup-SentryXDR-Permissions-COMPLETE.ps1
```

---

## ?? **Data Flow**

```
1. Client submits remediation request
   ?
2. HTTP Trigger receives request
   ?
3. RemediationValidator validates parameters
   ?
4. RemediationOrchestrator routes to worker service
   ?
5. Worker service acquires tenant-specific token
   ?
6. Worker calls platform API (Graph/MDE/Azure/MCAS)
   ?
7. Platform executes action
   ?
8. Worker logs result to Application Insights
   ?
9. Worker stores forensics to Storage Account
   ?
10. Response returned to client
```

---

## ?? **Retry & Error Handling**

### **Retry Strategy**
- **Transient Errors:** 3 retries with exponential backoff
- **Initial Delay:** 1 second
- **Max Delay:** 30 seconds
- **Retryable:** 429 (Too Many Requests), 503 (Service Unavailable), network errors

### **Error Handling**
```csharp
try
{
    // Execute action
}
catch (HttpRequestException ex) when (IsTransient(ex))
{
    // Retry with backoff
}
catch (Exception ex)
{
    // Log and return error response
    return CreateExceptionResponse(request, ex, startTime);
}
```

---

## ?? **Observability**

### **Logging**
- **Application Insights:** Real-time telemetry
- **Log Analytics:** 90-day retention
- **Structured Logging:** JSON format with correlation IDs

### **Metrics**
- Request rate (per platform)
- Success rate (per action)
- Response time (percentiles)
- Error rate (by type)

### **Tracing**
- Request correlation ID
- Tenant ID
- Incident ID
- Action type
- Duration

---

## ??? **Infrastructure**

### **Azure Resources**

| Resource | SKU | Purpose |
|----------|-----|---------|
| **Function App** | Premium EP1 | API hosting, elastic scaling |
| **Storage (Primary)** | Standard_LRS | Function App storage |
| **Storage (Forensics)** | Standard_GRS | Investigation packages, geo-redundant |
| **Application Insights** | Standard | Telemetry & monitoring |
| **Log Analytics** | PerGB2018 | Centralized logging (90-day) |
| **Automation Account** | Free | Hybrid worker (optional) |

**Estimated Cost:** ~$100-150/month

---

## ?? **Security**

### **Network Security**
- HTTPS only (TLS 1.2+)
- CORS configured
- Private Endpoints supported (optional)
- VNet integration supported (optional)

### **Secrets Management**
- Client Secrets in Azure Key Vault
- Managed Identity for Azure resources
- No secrets in source code

### **Audit & Compliance**
- All actions logged to Application Insights
- 90-day retention
- Azure Policy compliance tags
- RBAC permissions (least privilege)

---

## ?? **API Reference**

### **Base URL**
```
https://<function-app>.azurewebsites.net/api
```

### **Endpoints**

#### **Submit Remediation**
```http
POST /xdr/remediate
Content-Type: application/json

{
  "tenantId": "tenant-guid",
  "incidentId": "incident-123",
  "platform": "MDE",
  "action": "IsolateDevice",
  "parameters": {
    "deviceId": "device-guid",
    "isolationType": "Full"
  },
  "initiatedBy": "analyst@contoso.com",
  "justification": "Suspicious activity detected"
}
```

#### **Get Status**
```http
GET /xdr/status/{requestId}
```

#### **Health Check**
```http
GET /xdr/health
```

#### **Swagger UI**
```http
GET /swagger/ui
```

---

## ?? **Performance**

### **Scalability**
- **Concurrent Requests:** 100+ (auto-scaling)
- **Cold Start:** <3 seconds
- **Warm Request:** <100ms
- **Throughput:** 100+ req/sec

### **Optimization**
- Token caching (reduces auth overhead)
- Connection pooling (HTTP reuse)
- Async/await throughout
- Efficient JSON serialization

---

## ?? **Related Documentation**

- [Deployment Guide](DEPLOYMENT_GUIDE.md)
- [API Reference](https://your-app.azurewebsites.net/api/swagger/ui)
- [Permissions Setup](Deployment/scripts/Setup-SentryXDR-Permissions-COMPLETE.ps1)
- [Production Checklist](PRODUCTION_READINESS_CHECKLIST.md)
- [Future Roadmap](V2_ROADMAP_API_WORKBOOK.md)

---

**Version:** 1.0.0  
**Last Updated:** 2025-01-15  
**Status:** ? Production Ready
