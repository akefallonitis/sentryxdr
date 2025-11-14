# ?? SENTRYXDR v2.0 - API VERIFICATION & ENHANCEMENT ROADMAP

**Current Version:** 1.0.0 (Production)  
**Target Version:** 2.0.0  
**Focus:** API completeness, Workbook integration, Advanced features  

---

## ?? **PHASE 1: API VERIFICATION (All Microsoft Security APIs)**

### **1. Microsoft Defender for Endpoint (MDE)**
**API Reference:** https://learn.microsoft.com/en-us/defender-endpoint/api/apis-intro

#### **Current Implementation (24 actions)** ?
- Device isolation/unisolation
- IOC management (indicators)
- Live response (file operations)
- AIR (Automated Investigation & Response)
- Antivirus scans
- App restrictions

#### **Missing Critical Actions (v2.0)**
Priority actions to add:

| Action | Endpoint | Priority | Estimated Time |
|--------|----------|----------|----------------|
| **Advanced Hunting (Enhanced)** | `/api/advancedqueries/run` | HIGH | 2h |
| **Device Tags Management** | `/api/machines/{id}/tags` | MEDIUM | 1h |
| **Software Inventory** | `/api/machines/{id}/software` | MEDIUM | 2h |
| **Vulnerability Management** | `/api/machines/{id}/vulnerabilities` | HIGH | 3h |
| **Security Recommendations** | `/api/machines/{id}/recommendations` | MEDIUM | 2h |
| **Attack Surface Reduction** | `/api/asr/rules` | HIGH | 2h |
| **Device Health** | `/api/machines/{id}/healthstatus` | LOW | 1h |
| **Network Containment** | `/api/machines/{id}/NetworkContainment` | HIGH | 1h |

**Total New Actions:** 8 (Priority: 5 HIGH, 2 MEDIUM, 1 LOW)  
**Total Time:** ~14 hours

---

### **2. Microsoft Graph API (v1.0 & Beta)**
**API Reference:** https://learn.microsoft.com/en-us/graph/api/overview

#### **Current Implementation** ?
- Entra ID: 18 actions
- Intune: 15 actions
- MCAS: 12 actions
- MDO: 15 actions
- Mail Forwarding: 3 actions

#### **Missing Critical Actions (v2.0)**

**Entra ID Enhancements:**
| Action | Endpoint | Priority | Time |
|--------|----------|----------|------|
| **Risk Detection** | `/beta/riskDetections` | HIGH | 2h |
| **Risky Users** | `/beta/riskyUsers/{id}/history` | HIGH | 2h |
| **Sign-in Logs (Advanced)** | `/beta/auditLogs/signIns` | MEDIUM | 3h |
| **Identity Protection Policies** | `/beta/identityProtection/policies` | HIGH | 3h |
| **Access Reviews** | `/beta/accessReviews` | MEDIUM | 4h |
| **Entitlement Management** | `/beta/identityGovernance/entitlementManagement` | LOW | 4h |

**MDO Enhancements:**
| Action | Endpoint | Priority | Time |
|--------|----------|----------|------|
| **Safe Links** | `/beta/security/threatIntelligence/urlIndicators` | HIGH | 2h |
| **Safe Attachments** | `/beta/security/threatIntelligence/fileIndicators` | HIGH | 2h |
| **Anti-Phishing Policies** | `/beta/security/antiPhishPolicies` | MEDIUM | 3h |
| **Mail Flow Rules (Transport Rules)** | Exchange Online PowerShell via Automation | HIGH | 4h |

**Intune Enhancements:**
| Action | Endpoint | Priority | Time |
|--------|----------|----------|------|
| **App Protection Policies** | `/beta/deviceAppManagement/managedAppPolicies` | MEDIUM | 3h |
| **Configuration Profiles (Advanced)** | `/beta/deviceManagement/deviceConfigurations` | MEDIUM | 3h |
| **Compliance Scripts** | `/beta/deviceManagement/deviceComplianceScripts` | LOW | 2h |

**Total New Graph Actions:** 13  
**Total Time:** ~33 hours

---

### **3. Azure Management API**
**API Reference:** https://learn.microsoft.com/en-us/rest/api/azure/

#### **Current Implementation (25 actions)** ?
- VM isolation (NSG)
- Storage security
- Key Vault operations
- Firewall rules

#### **Missing Critical Actions (v2.0)**

| Action | API | Priority | Time |
|--------|-----|----------|------|
| **Sentinel Integration** | `/workspaces/{id}/incidents` | HIGH | 4h |
| **Defender for Cloud** | `/securityStatuses` | HIGH | 3h |
| **Azure Firewall Threat Intel** | `/azureFirewalls/{id}/threatIntelligence` | HIGH | 2h |
| **Application Gateway WAF** | `/applicationGateways/{id}/webApplicationFirewall` | MEDIUM | 3h |
| **Azure DDoS Protection** | `/ddosProtectionPlans` | MEDIUM | 2h |
| **Private Endpoint Management** | `/privateEndpoints` | MEDIUM | 2h |
| **Azure Policy Compliance** | `/policyStates` | LOW | 2h |
| **Cost Management Alerts** | `/costManagement/alerts` | LOW | 2h |

**Total New Azure Actions:** 8  
**Total Time:** ~20 hours

---

### **4. Microsoft Defender for Cloud Apps (MCAS)**

#### **Missing Actions (v2.0)**

| Action | Endpoint | Priority | Time |
|--------|----------|----------|------|
| **Cloud Discovery** | `/api/v1/discovery` | MEDIUM | 3h |
| **Activity Logs** | `/api/v1/activities` | MEDIUM | 2h |
| **Sanctioned/Unsanctioned Apps** | `/api/v1/apps` | HIGH | 2h |
| **IP Address Ranges** | `/api/v1/subnet` | LOW | 1h |

**Total New MCAS Actions:** 4  
**Total Time:** ~8 hours

---

## ?? **SUMMARY: v2.0 API ENHANCEMENTS**

| Platform | Current | New Actions | Total v2.0 | Time |
|----------|---------|-------------|------------|------|
| **MDE** | 24 | +8 | 32 | 14h |
| **Entra ID** | 18 | +6 | 24 | 14h |
| **MDO** | 15 | +4 | 19 | 11h |
| **Intune** | 15 | +3 | 18 | 8h |
| **Azure** | 25 | +8 | 33 | 20h |
| **MCAS** | 12 | +4 | 16 | 8h |
| **DLP** | 5 | +0 | 5 | - |
| **On-Prem AD** | 5 | +0 | 5 | - |
| **Incident Mgmt** | 18 | +0 | 18 | - |
| **Mail Forwarding** | 3 | +0 | 3 | - |
| **TOTAL** | **150** | **+33** | **183** | **75h** |

**v2.0 Target:** 183 actions (22% increase)  
**Estimated Development Time:** ~2 weeks (75 hours)

---

## ?? **PHASE 2: AZURE WORKBOOK INTEGRATION**

### **Vision: Single Pane of Glass**

**Goal:** Unified console for all XDR operations with:
- ? Multi-tenant support (Lighthouse)
- ? Incident/Alert/Entity-driven actions
- ? Auto-populated dropdowns
- ? Conditional visibility
- ? Live response console
- ? Advanced hunting console
- ? File upload/download
- ? Auto-refresh listings

### **Architecture**

```
??????????????????????????????????????????????????????????????
?         Azure Workbook (Single Pane of Glass)              ?
?                                                             ?
?  ???????????????????????????????????????????????????????? ?
?  ?  Top Menu (Auto-Populated Global Parameters)         ? ?
?  ?  - Tenant Selector (Lighthouse)                      ? ?
?  ?  - Incident Selector (Auto-refresh)                  ? ?
?  ?  - Entity Selector (Devices, Users, IPs, Files)      ? ?
?  ???????????????????????????????????????????????????????? ?
?                                                             ?
?  ???????????????????????????????????????????????????????? ?
?  ?  Main Tabs                                           ? ?
?  ?  ?????????????????????????????????????????????????? ? ?
?  ?  ? Inc. ? MDE  ? MDO  ?EntraID?Azure? Live ?Hunt  ? ? ?
?  ?  ?      ?      ?      ?      ?     ?Resp. ?      ? ? ?
?  ?  ?????????????????????????????????????????????????? ? ?
?  ?                                                       ? ?
?  ?  Each Tab Contains:                                  ? ?
?  ?  - Listing Operations (Custom Endpoints)             ? ?
?  ?  - Manual Actions (ARM Actions)                      ? ?
?  ?  - Conditional Visibility (per context)              ? ?
?  ?  - Auto-populated dropdowns                          ? ?
?  ???????????????????????????????????????????????????????? ?
?                                                             ?
?  ???????????????????????????????????????????????????????? ?
?  ?  Special Consoles                                    ? ?
?  ?  - Live Response Shell (Text input + ARM actions)    ? ?
?  ?  - Advanced Hunting KQL (Query editor + Execute)     ? ?
?  ?  - File Library (Upload from storage, Download)      ? ?
?  ???????????????????????????????????????????????????????? ?
??????????????????????????????????????????????????????????????
                            ?
                            ?
??????????????????????????????????????????????????????????????
?              SentryXDR Function App                         ?
?                                                             ?
?  Custom Endpoints (GET):                                   ?
?  - /api/v1/workbook/tenants (Lighthouse list)             ?
?  - /api/v1/workbook/incidents                             ?
?  - /api/v1/workbook/devices                               ?
?  - /api/v1/workbook/users                                 ?
?  - /api/v1/workbook/alerts                                ?
?                                                             ?
?  ARM Actions (POST):                                       ?
?  - All existing remediation actions                        ?
?  - Live response commands                                  ?
?  - Advanced hunting queries                                ?
??????????????????????????????????????????????????????????????
```

### **Workbook Features**

#### **1. Global Parameters (Top Menu)**
```json
{
  "parameters": [
    {
      "name": "TenantSelector",
      "type": "dropdown",
      "query": "customEndpoint: /api/v1/workbook/tenants",
      "multiSelect": true,
      "defaultValue": "all"
    },
    {
      "name": "IncidentSelector",
      "type": "dropdown",
      "query": "customEndpoint: /api/v1/workbook/incidents?tenantId={TenantSelector}",
      "dependsOn": ["TenantSelector"],
      "autoRefresh": "5m"
    },
    {
      "name": "EntityType",
      "type": "dropdown",
      "options": ["Device", "User", "IP", "File", "Email"],
      "defaultValue": "Device"
    },
    {
      "name": "EntitySelector",
      "type": "dropdown",
      "query": "customEndpoint: /api/v1/workbook/{EntityType}?tenantId={TenantSelector}",
      "dependsOn": ["TenantSelector", "EntityType"],
      "conditionalVisibility": "EntityType != ''"
    }
  ]
}
```

#### **2. Main Tabs with Conditional Visibility**

**Tab 1: Incident Management**
- Listing: All incidents (auto-refresh)
- Actions:
  - Update status (ARM action)
  - Change severity (ARM action)
  - Assign to user (ARM action)
  - Merge incidents (ARM action)
- Conditional: Show only when incident selected

**Tab 2: MDE Operations**
- Listing: Devices, Alerts, Investigations
- Actions:
  - Isolate device (ARM)
  - Run antivirus scan (ARM)
  - Collect investigation package (ARM)
  - Manage IOCs (ARM)
- Conditional: Show only when device selected

**Tab 3: Live Response Console**
```
????????????????????????????????????????????????????
? Live Response Shell                              ?
?                                                  ?
? Device: [Auto-populated from selection]         ?
?                                                  ?
? Command: [Text Input]                           ?
? [Execute] [Get File] [Put File]                 ?
?                                                  ?
? Output:                                          ?
? ?????????????????????????????????????????????????
? ? > dir C:\                                    ??
? ? Directory listing...                         ??
? ?                                              ??
? ? > reg query HKLM\Software                   ??
? ? Registry output...                           ??
? ?????????????????????????????????????????????????
????????????????????????????????????????????????????
```

**Tab 4: Advanced Hunting Console**
```
????????????????????????????????????????????????????
? Advanced Hunting (KQL Query Editor)             ?
?                                                  ?
? Tenant: [Auto-populated]                        ?
? Timespan: [Dropdown: 1h, 24h, 7d, 30d, Custom] ?
?                                                  ?
? Query:                                          ?
? ?????????????????????????????????????????????????
? ? DeviceProcessEvents                          ??
? ? | where Timestamp > ago(1h)                  ??
? ? | where ProcessCommandLine contains "powershell"??
? ? | summarize count() by DeviceName            ??
? ?????????????????????????????????????????????????
?                                                  ?
? [Execute Query] [Export to CSV]                 ?
?                                                  ?
? Results: (Auto-refresh grid)                    ?
????????????????????????????????????????????????????
```

---

## ?? **PHASE 3: IMPLEMENTATION PLAN**

### **Week 1: API Enhancements (High Priority)**
- Day 1-2: MDE enhancements (8 actions)
- Day 3-4: Entra ID enhancements (6 actions)
- Day 5: Azure enhancements (critical 4 actions)

### **Week 2: API Completion & Workbook Foundation**
- Day 1-2: Complete remaining API actions
- Day 3-5: Workbook foundation (parameters, tabs, basic actions)

### **Week 3: Advanced Workbook Features**
- Day 1-2: Live Response Console
- Day 3-4: Advanced Hunting Console
- Day 5: File upload/download workarounds

### **Week 4: Integration & Testing**
- Day 1-2: Multi-tenant/Lighthouse integration
- Day 3-4: Auto-refresh, conditional visibility
- Day 5: End-to-end testing, documentation

---

## ?? **DELIVERABLES**

### **v2.0 Package**
1. ? Enhanced Function App (183 actions)
2. ? Azure Workbook (single pane of glass)
3. ? Workbook endpoints (listing operations)
4. ? Updated ARM templates
5. ? Deployment guide (workbook + function app)
6. ? Demo video
7. ? Updated README

---

## ?? **IMMEDIATE NEXT STEPS**

**Would you like me to:**

**Option A:** Start with high-priority API enhancements (Week 1)?
**Option B:** Create the workbook foundation first (rapid prototype)?
**Option C:** Do both in parallel (split work)?

**My Recommendation:** Option C - I'll create:
1. Workbook foundation (tonight) - visual mockup with basic connectivity
2. High-priority API actions (tomorrow) - critical missing features

**Which would you prefer?**
