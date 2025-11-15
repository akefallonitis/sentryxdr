# ?? PERMISSIONS ANALYSIS - LEAST PRIVILEGE REVIEW

**Script:** `Setup-SentryXDR-Permissions-COMPLETE.ps1`  
**Date:** 2025-01-15  
**Status:** ?? **NEEDS OPTIMIZATION**

---

## ?? **LEAST PRIVILEGE PRINCIPLE**

**Definition:** Grant only the minimum permissions required for the application to function correctly.

**Current Status:** ?? **PARTIALLY COMPLIANT** - Some permissions can be reduced

---

## ?? **CURRENT PERMISSIONS (106 TOTAL)**

### **Microsoft Graph API: 56 Permissions**
### **Windows Defender ATP: 16 Permissions**  
### **Azure RBAC: 3 Roles**

---

## ?? **EXCESSIVE PERMISSIONS IDENTIFIED**

### **1. Directory.ReadWrite.All** ? **HIGH RISK**

**Current:** Full directory write access  
**Risk:** Can modify ANY directory object (users, groups, apps, devices)  
**Usage in Code:** User management, directory operations  

**Least Privilege Alternative:**
```
? User.ReadWrite.All            (for user disable/enable)
? User.EnableDisableAccount.All (specific for user operations)
? Group.Read.All                (read-only groups - NO write needed)
? Device.Read.All               (read devices)
? Application.Read.All          (read apps for OAuth governance)
? Directory.ReadWrite.All       (REMOVE - too broad)
```

**Recommendation:** ? **REMOVE Directory.ReadWrite.All**

---

### **2. Group.ReadWrite.All** ? **NOT NEEDED**

**Current:** Can modify all security and Microsoft 365 groups  
**Code Analysis:** ? **No group modification actions found**  
**Actual Need:** Read groups only (for context)  

**Recommendation:**  
? REMOVE: `Group.ReadWrite.All`  
? KEEP: `Group.Read.All` (already present)

---

### **3. Mail.ReadWrite** vs **Mail.ReadWrite.Shared** ??

**Current:** `Mail.ReadWrite` - User's own mailbox  
**Needed:** Access to other users' mailboxes (MDO email remediation)  
**Issue:** Application permission should access multiple mailboxes  

**Correct Permission:**  
```
? Mail.ReadWrite           (user's own mail only)
? Mail.ReadWrite           (application permission - all mailboxes)
```

**Status:** ? **CORRECT** - Application permission grants access to all mailboxes

---

### **4. RoleManagement.ReadWrite.Directory** ?? **CRITICAL**

**Current:** Can assign/remove ANY directory role  
**Usage:** Remove admin roles from compromised accounts  
**Risk:** **VERY HIGH** - Can elevate own privileges to Global Admin  

**Security Concerns:**
- Can assign Global Administrator role to itself
- Can bypass other security controls
- Can grant permissions to other applications

**Mitigation:**
1. **Conditional Access:** Restrict where app can run from
2. **PIM:** Use Privileged Identity Management
3. **Alert:** Monitor role assignment changes
4. **Alternative:** Use Azure Automation with separate privileged account

**Recommendation:** ?? **KEEP BUT MONITOR CLOSELY** - Ensure:
- App runs only from trusted networks
- Role changes are logged and alerted
- Regular access reviews

---

### **5. Files.ReadWrite.All** & **Sites.ReadWrite.All** ??

**Current:** Access to ALL files and sites  
**Usage:** File quarantine (DLP actions)  
**Risk:** Can access all SharePoint/OneDrive content  

**Least Privilege Consideration:**
```
Current: Files.ReadWrite.All + Sites.ReadWrite.All
Alternative: Files.ReadWrite.Selected (requires site-specific consent)
```

**Challenge:** Site-specific consent not practical for multi-tenant MSSP scenario  

**Recommendation:** ?? **KEEP** - Necessary for DLP file quarantine across all sites  
**Mitigation:** Implement file access logging and auditing

---

### **6. v2.0 Permissions Not Yet Implemented** ?

**Remove from v1.0:**
```
? EntitlementManagement.Read.All
? EntitlementManagement.ReadWrite.All
? AccessReview.Read.All
? AccessReview.ReadWrite.All
```

**Reason:** Features not implemented yet, violates least privilege

---

### **7. Machine.CollectForensics** ? **CORRECT**

**Current:** Can collect forensics from devices  
**Usage:** Investigation package collection (MDE action)  
**Risk:** Medium - Can access device memory/files  
**Justification:** Required for incident response  

**Recommendation:** ? **KEEP** - Properly scoped

---

## ? **CORRECTLY SCOPED PERMISSIONS**

These follow least privilege:

| Permission | Justification | Status |
|------------|---------------|--------|
| `User.EnableDisableAccount.All` | Specific action, not full User.ReadWrite | ? GOOD |
| `UserAuthenticationMethod.ReadWrite.All` | MFA reset only | ? GOOD |
| `Policy.ReadWrite.ConditionalAccess` | Scoped to CA policies only | ? GOOD |
| `ThreatIndicators.ReadWrite.OwnedBy` | Only indicators created by app | ? EXCELLENT |
| `DeviceManagementManagedDevices.PrivilegedOperations.All` | Wipe/retire devices | ? JUSTIFIED |
| `SecurityIncident.ReadWrite.All` | Incident management | ? CORRECT |
| `Machine.Isolate` | Specific MDE action | ? PERFECT |

---

## ?? **RECOMMENDED OPTIMIZATIONS**

### **HIGH PRIORITY (Security Risk)**

1. ? **REMOVE** `Directory.ReadWrite.All`
   - Already have specific permissions (User.ReadWrite.All, etc.)
   
2. ? **REMOVE** `Group.ReadWrite.All`
   - No group modification actions
   - Keep Group.Read.All

3. ? **REMOVE** v2.0 permissions (not implemented):
   - EntitlementManagement.*
   - AccessReview.*

4. ?? **ADD MONITORING** for `RoleManagement.ReadWrite.Directory`
   - Alert on any role assignment changes
   - Log all role modifications
   - Implement Conditional Access restrictions

---

### **MEDIUM PRIORITY (Best Practice)**

5. ?? **REVIEW** `Mail.ReadWrite` scope
   - Verify application permission is correct
   - Document why full mailbox access is needed

6. ?? **DOCUMENT** broad permissions:
   - Files.ReadWrite.All (needed for DLP quarantine)
   - Sites.ReadWrite.All (needed for SharePoint actions)
   - User.ReadWrite.All (needed for user management)

---

### **LOW PRIORITY (Future Enhancement)**

7. ?? **REFINE** in v2.0:
   - Implement granular permissions where possible
   - Use delegated permissions for user-context actions
   - Implement consent framework for customer-specific scopes

---

## ?? **OPTIMIZED PERMISSION LIST**

### **REMOVE (Not Needed/Excessive):**
```powershell
# HIGH RISK - Too Broad
? "Directory.ReadWrite.All"          # Use specific permissions instead

# NOT USED - No group modification
? "Group.ReadWrite.All"              # Keep Group.Read.All only

# V2.0 - Not Implemented Yet
? "EntitlementManagement.Read.All"
? "EntitlementManagement.ReadWrite.All"
? "AccessReview.Read.All"
? "AccessReview.ReadWrite.All"
```

### **KEEP (Least Privilege Compliant):**
```powershell
? All MDE permissions (scoped to device actions)
? All Intune permissions (device management)
? User.ReadWrite.All (user management)
? User.EnableDisableAccount.All (specific action)
? UserAuthenticationMethod.ReadWrite.All (MFA only)
? Policy.ReadWrite.ConditionalAccess (CA only)
? SecurityIncident.ReadWrite.All (incidents)
? Mail.ReadWrite (mailbox remediation)
? Files.ReadWrite.All (DLP actions)
? Sites.ReadWrite.All (SharePoint security)
```

### **MONITOR CLOSELY (High Privilege):**
```powershell
?? "RoleManagement.ReadWrite.Directory"    # Can assign roles - CRITICAL
?? "Files.ReadWrite.All"                   # All files access
?? "Sites.ReadWrite.All"                   # All sites access
?? "User.ReadWrite.All"                    # All users write
```

---

## ??? **SECURITY RECOMMENDATIONS**

### **1. Conditional Access**
```
Require:
- Specific source IPs (Azure Function VNet)
- Compliant device (if hybrid worker)
- MFA for emergency access
```

### **2. Privileged Identity Management (PIM)**
```
- Use PIM for RoleManagement permissions
- Time-bound activation
- Approval workflow for role changes
```

### **3. Monitoring & Alerting**
```
Alert on:
- Role assignment changes
- File access to sensitive sites
- User account modifications
- OAuth app approvals
```

### **4. Audit Logging**
```
Log to:
- Application Insights (all actions)
- Log Analytics (90-day retention)
- Security Information and Event Management (SIEM)
```

---

## ?? **PERMISSION REDUCTION SUMMARY**

| Category | Before | After | Reduction |
|----------|--------|-------|-----------|
| **Microsoft Graph** | 56 | **50** | **-6 (-11%)** |
| **Windows Defender ATP** | 16 | 16 | 0 |
| **Azure RBAC** | 3 | 3 | 0 |
| **TOTAL** | **75** | **69** | **-6 (-8%)** |

**Removed Permissions:**
1. Directory.ReadWrite.All
2. Group.ReadWrite.All
3. EntitlementManagement.Read.All
4. EntitlementManagement.ReadWrite.All
5. AccessReview.Read.All
6. AccessReview.ReadWrite.All

---

## ? **CONCLUSION**

### **Current State:**
- ?? **PARTIALLY COMPLIANT** with least privilege
- 6 excessive/unnecessary permissions identified
- Some high-risk permissions require additional controls

### **Recommended State:**
- ? Remove 6 unnecessary permissions
- ?? Add monitoring for high-privilege permissions
- ? Document justification for broad-scope permissions
- ?? Implement Conditional Access restrictions

### **Action Items:**
1. ? Update `Setup-SentryXDR-Permissions-COMPLETE.ps1` to remove 6 permissions
2. ?? Add monitoring alerts for `RoleManagement.ReadWrite.Directory`
3. ?? Document remaining broad permissions in SECURITY.md
4. ?? Implement Conditional Access policies
5. ? Test thoroughly after permission reduction

---

## ?? **NEXT STEPS**

**Immediate (Pre-Deployment):**
1. Update permissions script
2. Remove unnecessary permissions
3. Test all actions still work

**Post-Deployment:**
1. Monitor permission usage
2. Implement alerting
3. Regular access reviews
4. Annual permission audit

---

**Status:** ?? **ACTION REQUIRED**  
**Risk:** Medium (excessive permissions)  
**Effort:** Low (script update)  
**Priority:** High (security best practice)

**Recommendation:** ? **FIX BEFORE PRODUCTION DEPLOYMENT**
