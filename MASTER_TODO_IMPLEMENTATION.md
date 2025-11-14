# ?? MASTER TODO - COMPLETE IMPLEMENTATION

## ?? CRITICAL CORRECTIONS (Do First)

### ? Priority 0.1: Add Incident Management Worker (NEW - CRITICAL)
**Status**: ? Not Started  
**Effort**: 3 hours  
**Impact**: CRITICAL - Core XDR functionality missing

**Files to Create**:
- [ ] `Services/Workers/IncidentManagementService.cs` (500+ lines)
- [ ] `Models/IncidentModels.cs` (200+ lines)
- [ ] Update `Functions/Workers/PlatformWorkers.cs`
- [ ] Update `Program.cs` service registration

**Actions to Implement** (15):
```csharp
1.  UpdateIncidentStatus (InProgress, Resolved, Closed)
2.  UpdateIncidentSeverity (Low, Medium, High, Critical)
3.  AssignIncidentToUser
4.  AddIncidentComment
5.  AddIncidentTag
6.  LinkIncidentsToCase
7.  ResolveIncident
8.  ReopenIncident
9.  EscalateIncident
10. CreateCustomDetectionFromIncident
11. TriggerAutomatedPlaybook
12. UpdateIncidentClassification (TruePositive, FalsePositive, BenignPositive)
13. UpdateIncidentDetermination (Malware, Phishing, Suspicious, Clean, Unknown)
14. MergeIncidents
15. ExportIncidentForReporting
```

**API Reference**:
- `PATCH /security/incidents/{id}`
- `POST /security/incidents/{id}/comments`
- https://learn.microsoft.com/en-us/graph/api/security-incident-update

---

### ? Priority 0.2: Fix File Detonation Location
**Status**: ? Not Fixed  
**Effort**: 30 minutes  
**Impact**: HIGH - Currently in wrong worker

**Changes Required**:
- [ ] Remove detonation from MDE (8 actions)
- [ ] Add detonation to MDO (8 actions)
- [ ] Update action counts: MDE 72?64, MDO 35?43
- [ ] Update `Models/XDRModels.cs` enum comments
- [ ] Move implementation to MDOApiService

**Correct Classification**:
```
? Current: MDE has File Detonation
? Correct: MDO should have File Detonation
API: POST /security/threatSubmission/fileContentThreats
```

---

### ? Priority 0.3: Update ARM Template - Role Assignments
**Status**: ?? Partial (MI enabled but no roles)  
**Effort**: 1 hour  
**Impact**: CRITICAL - Azure Worker cannot function

**Add to `Deployment/azuredeploy.json`**:

```json
// After Function App resource, add 3 role assignments

// 1. Contributor Role
{
  "type": "Microsoft.Authorization/roleAssignments",
  "apiVersion": "2022-04-01",
  "name": "[guid(concat(resourceGroup().id, 'contributor'))]",
  "dependsOn": [
    "[resourceId('Microsoft.Web/sites', variables('functionAppName'))]"
  ],
  "properties": {
    "roleDefinitionId": "[concat('/subscriptions/', subscription().subscriptionId, '/providers/Microsoft.Authorization/roleDefinitions/', 'b24988ac-6180-42a0-ab88-20f7382dd24c')]",
    "principalId": "[reference(resourceId('Microsoft.Web/sites', variables('functionAppName')), '2022-03-01', 'Full').identity.principalId]",
    "principalType": "ServicePrincipal"
  }
},

// 2. Security Admin Role
{
  "type": "Microsoft.Authorization/roleAssignments",
  "apiVersion": "2022-04-01",
  "name": "[guid(concat(resourceGroup().id, 'securityadmin'))]",
  "dependsOn": [
    "[resourceId('Microsoft.Web/sites', variables('functionAppName'))]"
  ],
  "properties": {
    "roleDefinitionId": "[concat('/subscriptions/', subscription().subscriptionId, '/providers/Microsoft.Authorization/roleDefinitions/', 'fb1c8493-542b-48eb-b624-b4c8fea62acd')]",
    "principalId": "[reference(resourceId('Microsoft.Web/sites', variables('functionAppName')), '2022-03-01', 'Full').identity.principalId]",
    "principalType": "ServicePrincipal"
  }
},

// 3. Network Contributor Role
{
  "type": "Microsoft.Authorization/roleAssignments",
  "apiVersion": "2022-04-01",
  "name": "[guid(concat(resourceGroup().id, 'networkcontributor'))]",
  "dependsOn": [
    "[resourceId('Microsoft.Web/sites', variables('functionAppName'))]"
  ],
  "properties": {
    "roleDefinitionId": "[concat('/subscriptions/', subscription().subscriptionId, '/providers/Microsoft.Authorization/roleDefinitions/', '4d97b98b-1d4f-4787-a291-c67834d212e7')]",
    "principalId": "[reference(resourceId('Microsoft.Web/sites', variables('functionAppName')), '2022-03-01', 'Full').identity.principalId]",
    "principalType": "ServicePrincipal"
  }
}
```

**Tasks**:
- [ ] Add 3 role assignment resources
- [ ] Test one-click deployment
- [ ] Verify roles are assigned automatically

---

### ? Priority 0.4: Add Missing Storage Containers to ARM
**Status**: ? Not Added  
**Effort**: 30 minutes  
**Impact**: HIGH - New features need storage

**Add 7 Container Resources**:

```json
// After existing containers, add these 7:

{
  "type": "Microsoft.Storage/storageAccounts/blobServices/containers",
  "apiVersion": "2022-09-01",
  "name": "[concat(variables('storageAccountName'), '/default/live-response-library')]",
  "dependsOn": [
    "[resourceId('Microsoft.Storage/storageAccounts/blobServices', variables('storageAccountName'), 'default')]"
  ],
  "properties": { "publicAccess": "None" }
},
{
  "type": "Microsoft.Storage/storageAccounts/blobServices/containers",
  "apiVersion": "2022-09-01",
  "name": "[concat(variables('storageAccountName'), '/default/live-response-sessions')]",
  "dependsOn": [
    "[resourceId('Microsoft.Storage/storageAccounts/blobServices', variables('storageAccountName'), 'default')]"
  ],
  "properties": { "publicAccess": "None" }
},
{
  "type": "Microsoft.Storage/storageAccounts/blobServices/containers",
  "apiVersion": "2022-09-01",
  "name": "[concat(variables('storageAccountName'), '/default/hunting-queries')]",
  "dependsOn": [
    "[resourceId('Microsoft.Storage/storageAccounts/blobServices', variables('storageAccountName'), 'default')]"
  ],
  "properties": { "publicAccess": "None" }
},
{
  "type": "Microsoft.Storage/storageAccounts/blobServices/containers",
  "apiVersion": "2022-09-01",
  "name": "[concat(variables('storageAccountName'), '/default/hunting-results')]",
  "dependsOn": [
    "[resourceId('Microsoft.Storage/storageAccounts/blobServices', variables('storageAccountName'), 'default')]"
  ],
  "properties": { "publicAccess": "None" }
},
{
  "type": "Microsoft.Storage/storageAccounts/blobServices/containers",
  "apiVersion": "2022-09-01",
  "name": "[concat(variables('storageAccountName'), '/default/detonation-submissions')]",
  "dependsOn": [
    "[resourceId('Microsoft.Storage/storageAccounts/blobServices', variables('storageAccountName'), 'default')]"
  ],
  "properties": { "publicAccess": "None" }
},
{
  "type": "Microsoft.Storage/storageAccounts/blobServices/containers",
  "apiVersion": "2022-09-01",
  "name": "[concat(variables('storageAccountName'), '/default/detonation-reports')]",
  "dependsOn": [
    "[resourceId('Microsoft.Storage/storageAccounts/blobServices', variables('storageAccountName'), 'default')]"
  ],
  "properties": { "publicAccess": "None" }
},
{
  "type": "Microsoft.Storage/storageAccounts/blobServices/containers",
  "apiVersion": "2022-09-01",
  "name": "[concat(variables('storageAccountName'), '/default/threat-intelligence')]",
  "dependsOn": [
    "[resourceId('Microsoft.Storage/storageAccounts/blobServices', variables('storageAccountName'), 'default')]"
  ],
  "properties": { "publicAccess": "None" }
}
```

**Tasks**:
- [ ] Add all 7 container definitions
- [ ] Update app settings with container names
- [ ] Test deployment

---

### ? Priority 0.5: Add Missing XDRAction Enums
**Status**: ? Not Added  
**Effort**: 45 minutes  
**Impact**: CRITICAL - Build will fail without these

**Update `Models/XDRModels.cs`** - Add to XDRAction enum:

```csharp
// ==================== Incident Management (15 actions) ====================
UpdateIncidentStatus,
UpdateIncidentSeverity,
AssignIncidentToUser,
AddIncidentComment,
AddIncidentTag,
LinkIncidentsToCase,
ResolveIncident,
ReopenIncident,
EscalateIncident,
CreateCustomDetectionFromIncident,
TriggerAutomatedPlaybook,
UpdateIncidentClassification,
UpdateIncidentDetermination,
MergeIncidents,
ExportIncidentForReporting,

// ==================== Azure Infrastructure (15 actions) ====================
IsolateVMNetwork,
StopVM,
RestartVM,
DeleteVM,
SnapshotVM,
DetachDisk,
RevokeVMAccess,
UpdateNSGRules,
DisablePublicIP,
BlockStorageAccount,
DisableServicePrincipal,
RotateStorageKeys,
DeleteMaliciousResource,
EnableDiagnosticLogs,
TagResourceAsCompromised,

// ==================== Live Response (10 actions) ====================
RunLiveResponseScript,
UploadScriptToLibrary,
DeleteScriptFromLibrary,
InitiateLiveResponseSession,
GetLiveResponseResults,
RunLiveResponseCommand,
PutFileToDevice,
GetFileFromDevice,
CancelLiveResponseSession,
ExecuteNativeIRCommand,

// ==================== Advanced Hunting (5 actions) ====================
RunAdvancedHuntingQuery,
ScheduleHuntingQuery,
GetHuntingQueryResults,
ExportHuntingResults,
CreateCustomDetection,

// ==================== Threat Intelligence (12 actions) ====================
SubmitIOC,
UpdateIOC,
DeleteIOC,
BlockFileHash,
BlockIP,
BlockURL,
BlockCertificate,
AllowFileHash,
AllowIP,
AllowURL,
GetIOCList,
BulkSubmitIOCs,

// ==================== File Detonation - MDO (8 actions) ====================
SubmitFileForDetonation,
SubmitURLForDetonation,
GetDetonationReport,
DetonateFileFromURL,
GetSandboxScreenshots,
GetNetworkTraffic,
GetProcessTree,
GetBehaviorAnalysis
```

**Tasks**:
- [ ] Add all 65 new enum values
- [ ] Add XML comments for each
- [ ] Organize by worker category
- [ ] Compile and verify

---

## ?? PHASE 1: CORE IMPLEMENTATIONS (12 hours)

### Task 1.1: Complete Azure Worker (2 hours)
**Current**: 5/15 actions (33%)  
**Target**: 15/15 actions (100%)

**File**: `Services/Workers/AzureApiService.cs`

**Implement Missing Actions** (10):
- [ ] `DetachDiskAsync()` - Detach VM disk for isolation
- [ ] `RevokeVMAccessAsync()` - Remove all identities from VM
- [ ] `UpdateNSGRulesAsync()` - Modify NSG rules dynamically
- [ ] `DisablePublicIPAsync()` - Remove public IP from VM
- [ ] `BlockStorageAccountAsync()` - Enable firewall, deny public access
- [ ] `DisableServicePrincipalAsync()` - Disable app identity
- [ ] `RotateStorageKeysAsync()` - Rotate storage account keys
- [ ] `DeleteMaliciousResourceAsync()` - Delete compromised resource
- [ ] `EnableDiagnosticLogsAsync()` - Enable logging for forensics
- [ ] `TagResourceAsCompromisedAsync()` - Tag with metadata

**Testing**:
- [ ] Test with Managed Identity
- [ ] Test RBAC permissions
- [ ] Test error handling

---

### Task 1.2: Implement Incident Management Worker (3 hours) ??
**Priority**: CRITICAL  
**Status**: Not Started

**Files to Create**:
1. `Services/Workers/IncidentManagementService.cs`
2. `Models/IncidentModels.cs`
3. Update worker routing

**Implementation Details**:

```csharp
// Models/IncidentModels.cs
public class IncidentUpdateRequest
{
    public string IncidentId { get; set; }
    public IncidentStatus? Status { get; set; }
    public IncidentSeverity? Severity { get; set; }
    public string? AssignedTo { get; set; }
    public string? Comment { get; set; }
    public List<string>? Tags { get; set; }
    public IncidentClassification? Classification { get; set; }
    public IncidentDetermination? Determination { get; set; }
}

public enum IncidentStatus
{
    New,
    InProgress,
    Resolved,
    Closed,
    Reopened
}

public enum IncidentSeverity
{
    Informational,
    Low,
    Medium,
    High,
    Critical
}

public enum IncidentClassification
{
    Unknown,
    TruePositive,
    FalsePositive,
    BenignPositive
}

public enum IncidentDetermination
{
    Unknown,
    Malware,
    Phishing,
    Suspicious,
    Clean,
    CompromisedAccount,
    Ransomware,
    Other
}
```

**API Endpoints**:
- `PATCH /security/incidents/{id}`
- `POST /security/incidents/{id}/comments`
- `POST /security/incidents/{id}/link`

**All 15 Actions**:
- [ ] UpdateIncidentStatus
- [ ] UpdateIncidentSeverity
- [ ] AssignIncidentToUser
- [ ] AddIncidentComment
- [ ] AddIncidentTag
- [ ] LinkIncidentsToCase
- [ ] ResolveIncident
- [ ] ReopenIncident
- [ ] EscalateIncident
- [ ] CreateCustomDetectionFromIncident
- [ ] TriggerAutomatedPlaybook
- [ ] UpdateIncidentClassification
- [ ] UpdateIncidentDetermination
- [ ] MergeIncidents
- [ ] ExportIncidentForReporting

---

### Task 1.3: Implement Live Response with Native IR Commands (3 hours)
**Priority**: CRITICAL  
**Status**: Not Started

**Files to Create**:
1. `Services/Workers/LiveResponseService.cs`
2. `Models/LiveResponseModels.cs`
3. Native IR command scripts

**Native IR Commands Library**:

Create in blob storage: `live-response-library/native-commands/`

```powershell
# 1. collect-process-memory.ps1
# Collects memory dump of suspicious process
param([int]$ProcessId)
$dumpPath = "C:\Temp\process_$ProcessId.dmp"
procdump -ma $ProcessId $dumpPath
Write-Output "Memory dump saved to: $dumpPath"

# 2. collect-network-connections.ps1
# Captures active network connections
netstat -ano > C:\Temp\network-connections.txt
Get-NetTCPConnection | Export-Csv C:\Temp\tcp-connections.csv
Write-Output "Network data collected"

# 3. collect-file-artifacts.ps1
# Collects suspicious file and metadata
param([string]$FilePath)
$hash = Get-FileHash $FilePath -Algorithm SHA256
$metadata = Get-Item $FilePath | Select-Object *
@{Hash=$hash; Metadata=$metadata} | Export-Clixml C:\Temp\file-artifacts.xml

# 4. kill-malicious-process.ps1
# Terminates malicious process
param([int]$ProcessId)
Stop-Process -Id $ProcessId -Force
Write-Output "Process $ProcessId terminated"

# 5. isolate-suspicious-file.ps1
# Quarantines file to safe location
param([string]$FilePath)
$quarantine = "C:\Quarantine"
New-Item -ItemType Directory -Force -Path $quarantine
Move-Item $FilePath $quarantine -Force
Write-Output "File quarantined to: $quarantine"

# 6. extract-registry-keys.ps1
# Exports registry keys for analysis
param([string]$KeyPath)
reg export $KeyPath C:\Temp\registry-export.reg /y
Write-Output "Registry exported"

# 7. capture-event-logs.ps1
# Collects security event logs
param([int]$Hours = 24)
$start = (Get-Date).AddHours(-$Hours)
Get-WinEvent -FilterHashtable @{LogName='Security';StartTime=$start} | Export-Csv C:\Temp\security-events.csv
Get-WinEvent -FilterHashtable @{LogName='System';StartTime=$start} | Export-Csv C:\Temp\system-events.csv
Write-Output "Event logs captured"

# 8. dump-lsass-memory.ps1
# FORENSICS ONLY - Dumps LSASS for credential analysis
# REQUIRES ADMIN RIGHTS
$lsassPid = (Get-Process lsass).Id
procdump -ma $lsassPid C:\Temp\lsass.dmp
Write-Output "LSASS dump created (HANDLE WITH EXTREME CARE)"

# 9. collect-persistence-mechanisms.ps1
# Identifies persistence mechanisms
$results = @()
$results += Get-ItemProperty HKLM:\Software\Microsoft\Windows\CurrentVersion\Run
$results += Get-ItemProperty HKCU:\Software\Microsoft\Windows\CurrentVersion\Run
$results += Get-ScheduledTask | Where-Object {$_.State -ne 'Disabled'}
$results += Get-Service | Where-Object {$_.StartType -eq 'Automatic'}
$results | Export-Clixml C:\Temp\persistence-check.xml
Write-Output "Persistence mechanisms collected"

# 10. enumerate-suspicious-drivers.ps1
# Lists all loaded drivers
Get-WindowsDriver -Online | Export-Csv C:\Temp\drivers.csv
driverquery /v > C:\Temp\drivers-detailed.txt
Write-Output "Driver enumeration complete"
```

**Live Response Actions** (10):
- [ ] RunLiveResponseScript - Execute script from library
- [ ] UploadScriptToLibrary - Add new script to blob
- [ ] DeleteScriptFromLibrary - Remove script
- [ ] InitiateLiveResponseSession - Start session
- [ ] GetLiveResponseResults - Retrieve command output
- [ ] RunLiveResponseCommand - Execute ad-hoc command
- [ ] PutFileToDevice - Upload file to device
- [ ] GetFileFromDevice - Download file from device
- [ ] CancelLiveResponseSession - Terminate session
- [ ] ExecuteNativeIRCommand - Run native IR scripts

**Storage Integration**:
- Scripts: `live-response-library/` container
- Session logs: `live-response-sessions/{tenantId}/{sessionId}/`
- Output files: `live-response-sessions/{tenantId}/{sessionId}/output/`

---

### Task 1.4: Implement Threat Intelligence Service (2 hours)
**Priority**: CRITICAL  
**Status**: Not Started

**File**: `Services/Workers/ThreatIntelligenceService.cs`

**All 12 Actions**:
- [ ] SubmitIOC - Submit single indicator
- [ ] UpdateIOC - Modify existing indicator
- [ ] DeleteIOC - Remove indicator
- [ ] BlockFileHash - Block file by SHA256
- [ ] BlockIP - Block IP address
- [ ] BlockURL - Block URL/domain
- [ ] BlockCertificate - Block certificate
- [ ] AllowFileHash - Whitelist file
- [ ] AllowIP - Whitelist IP
- [ ] AllowURL - Whitelist URL
- [ ] GetIOCList - List all indicators
- [ ] BulkSubmitIOCs - Batch import

**Graph API**: `POST /security/tiIndicators`

**Features**:
- IOC expiration management
- Confidence scoring
- Threat feed integration
- Automatic enrichment

---

### Task 1.5: Implement Advanced Hunting Service (2 hours)
**Priority**: HIGH  
**Status**: Not Started

**File**: `Services/Workers/AdvancedHuntingService.cs`

**All 5 Actions**:
- [ ] RunAdvancedHuntingQuery - Execute KQL
- [ ] ScheduleHuntingQuery - Automated runs
- [ ] GetHuntingQueryResults - Retrieve results
- [ ] ExportHuntingResults - Save to blob
- [ ] CreateCustomDetection - Auto-response rule

**MDE API**: `POST /api/advancedhunting/run`

**KQL Query Library** (store in `hunting-queries/` container):
```kql
// 1. suspicious-process-execution.kql
DeviceProcessEvents
| where Timestamp > ago(24h)
| where ProcessCommandLine has_any ("powershell -enc", "cmd /c", "certutil", "bitsadmin")
| project Timestamp, DeviceName, ProcessCommandLine, AccountName

// 2. lateral-movement-detection.kql
DeviceNetworkEvents
| where Timestamp > ago(24h)
| where RemotePort in (445, 139, 3389, 5985)
| where InitiatingProcessFileName in ("powershell.exe", "cmd.exe", "wmic.exe")
| project Timestamp, DeviceName, RemoteIP, RemotePort, InitiatingProcessFileName

// 3. credential-dumping.kql
DeviceProcessEvents
| where Timestamp > ago(24h)
| where ProcessCommandLine has_any ("mimikatz", "procdump", "lsass")
| project Timestamp, DeviceName, ProcessCommandLine, AccountName

// 4. ransomware-behavior.kql
DeviceFileEvents
| where Timestamp > ago(1h)
| where FileName endswith_any (".encrypted", ".locked", ".crypto")
| summarize FileCount=count() by DeviceName, bin(Timestamp, 5m)
| where FileCount > 100

// 5. suspicious-registry-changes.kql
DeviceRegistryEvents
| where Timestamp > ago(24h)
| where RegistryKey has_any ("Run", "RunOnce", "Startup")
| project Timestamp, DeviceName, RegistryKey, RegistryValueData
```

---

## ?? PHASE 2: MDO ENHANCEMENTS (3 hours)

### Task 2.1: Move File Detonation to MDO (2 hours)
**Priority**: HIGH  
**Status**: Not Done

**Update**: `Services/Workers/MDOApiService.cs`

**Add All 8 Detonation Actions**:
```csharp
public async Task<XDRRemediationResponse> SubmitFileForDetonationAsync(XDRRemediationRequest request)
{
    // POST /security/threatSubmission/fileContentThreats
    var fileContent = request.Parameters["fileContent"]?.ToString();
    var fileName = request.Parameters["fileName"]?.ToString();
    
    var submission = new
    {
        category = "malware",
        contentType = "file",
        fileName = fileName,
        fileContent = fileContent
    };
    
    // Submit to Microsoft Defender
    // Return submission ID
}

public async Task<XDRRemediationResponse> SubmitURLForDetonationAsync(XDRRemediationRequest request)
{
    // POST /security/threatSubmission/urlThreats
}

public async Task<XDRRemediationResponse> GetDetonationReportAsync(XDRRemediationRequest request)
{
    // GET /security/threatSubmission/{id}
    // Retrieve full analysis report
}

// Implement remaining 5 actions...
```

**All 8 Actions**:
- [ ] SubmitFileForDetonation
- [ ] SubmitURLForDetonation
- [ ] GetDetonationReport
- [ ] DetonateFileFromURL
- [ ] GetSandboxScreenshots
- [ ] GetNetworkTraffic
- [ ] GetProcessTree
- [ ] GetBehaviorAnalysis

---

## ?? PHASE 3: CLEANUP (3 hours)

### Task 3.1: Remove Non-Remediation Actions
**Priority**: MEDIUM  
**Effort**: 1 hour

**Remove from XDRAction enum and workers**:

**MDE (15 read-only)**:
- GetSoftwareInventory
- GetVulnerabilityAssessment
- GetSecurityRecommendations
- GetMissingKBs
- GetEntityTimeline
- GetLateralMovementPaths
- GetSuspiciousActivities
- ExportSecurityData
- GetAlertDetails
- ListAlerts
- GetMachineDetails
- GetFileDetails
- GetUserDetails
- SearchEvents
- TriggerInvestigation

**EntraID (8 management)**:
- UpdateUserProfile
- AssignManager
- UpdateDepartment
- UpdateJobTitle
- GetUserProfile
- ListUsers
- GetGroupMembers
- GetDirectoryAuditLogs

**Intune (5 informational)**:
- GetDeviceCompliance
- GetDeviceConfiguration
- ListManagedDevices
- GetDeviceInventory
- GetAppInventory

---

### Task 3.2: Update Documentation (2 hours)

**Files to Update**:
- [ ] README.md - Add MI setup, incident management, workbook
- [ ] DEPLOYMENT.md - One-click with auto roles
- [ ] ACTION_INVENTORY.md - Update counts (234 total)
- [ ] Create INCIDENT_MANAGEMENT_GUIDE.md
- [ ] Create LIVE_RESPONSE_GUIDE.md
- [ ] Create WORKBOOK_DEPLOYMENT_GUIDE.md

---

## ?? PHASE 4: WORKBOOK INTEGRATION (4 hours)

### Task 4.1: Create Azure Workbook (3 hours)
**Priority**: HIGH  
**Status**: Not Started

**File**: `Deployment/SentryXDR-Workbook.json`

**Workbook Tabs**:
1. **Overview Dashboard**
   - Total actions executed (today, week, month)
   - Success/failure rate charts
   - Actions by tenant (pie chart)
   - Actions by platform (bar chart)
   - Top 10 most used actions

2. **Real-Time Operations**
   - In-progress actions (live table)
   - Queue depth gauge
   - Average execution time (line chart)
   - Current failures (alert tiles)
   - Actions per minute (spark line)

3. **Incident Management** ??
   - Open incidents by severity
   - Incident resolution time
   - Incidents by classification
   - Related remediation actions
   - Incident timeline

4. **Threat Intelligence** ??
   - Active IOCs count
   - Blocked threats (24h)
   - IOC effectiveness metrics
   - Threat trends (time series)
   - Top threat families

5. **Cost Analysis**
   - Cost per action
   - Cost by tenant
   - Cost trends (daily)
   - Budget alerts
   - Cost optimization recommendations

6. **Audit & Compliance**
   - Action history (searchable table)
   - User activity log
   - Policy compliance score
   - Export audit logs button
   - Anomaly detection

**KQL Queries** (store in workbook):
```kql
// Total actions by status
XDRActions_CL
| where TimeGenerated > ago(24h)
| summarize Count=count() by Status
| render piechart

// Actions per hour
XDRActions_CL
| where TimeGenerated > ago(24h)
| summarize Count=count() by bin(TimeGenerated, 1h)
| render timechart

// Top failing actions
XDRActions_CL
| where Status == "Failed"
| summarize FailureCount=count() by Action
| top 10 by FailureCount
| render barchart

// Incident resolution time
Incidents_CL
| where Status == "Resolved"
| extend ResolutionTime = datetime_diff('minute', ResolvedTime, CreatedTime)
| summarize AvgResolutionTime=avg(ResolutionTime) by Severity
```

---

### Task 4.2: Log Analytics Integration (1 hour)
- [ ] Create Log Analytics workspace resource in ARM
- [ ] Configure data ingestion from Function App
- [ ] Set up custom logs table
- [ ] Deploy workbook with ARM template
- [ ] Test all queries

---

## ?? FINAL METRICS

### Revised Action Counts
```
MDE: 64 actions (not 72)
MDO: 43 actions (not 35) - with detonation
Incident Management: 15 actions (NEW)
EntraID: 26 actions (after cleanup)
Intune: 28 actions (after cleanup)
MCAS: 23 actions (needs implementation)
Azure: 15 actions (needs completion)
MDI: 20 actions (needs implementation)

Total: 234 actions (219 + 15 incident management)
Pure Remediation: 234 (100%)
```

### Completion Checklist
- [ ] All XDRAction enums added (65 new)
- [ ] ARM template updated (roles + storage)
- [ ] Incident Management implemented (15 actions)
- [ ] Azure Worker completed (15 actions)
- [ ] Live Response implemented (10 actions + IR library)
- [ ] Threat Intelligence implemented (12 actions)
- [ ] Advanced Hunting implemented (5 actions)
- [ ] File Detonation moved to MDO (8 actions)
- [ ] Non-remediation actions removed (28 actions)
- [ ] Workbook created and deployed
- [ ] All documentation updated
- [ ] Build successful
- [ ] Tests passing

---

**Total Estimated Effort**: ~29 hours
**Priority**: Start with Priority 0 items (4 hours)
**Status**: Ready for systematic implementation ??
