# SentryXDR - Complete Action Inventory

## Total Actions: 219

### Platform Breakdown

#### 1. Microsoft Defender for Endpoint (MDE) - 80 Actions

**Machine Actions - Device Control (20)**
- IsolateDevice, ReleaseDeviceFromIsolation
- Restrict/Remove App Execution
- AV Scans (Quick, Full, Offline)
- Investigation Package Collection
- Machine Tagging & Management
- Logon User Analysis

**File Actions (15)**
- Stop & Quarantine File
- File Collection & Download
- Block/Unblock File
- File Statistics & Prevalence
- File Activities & Reputation

**Alert Actions (12)**
- Update, Comment, Resolve
- Classification & Assignment
- Related Entity Analysis (Machines, Files, IPs, Domains, Users)

**Investigation Actions (10)**
- Start/Cancel/Restart Automated Investigations
- Investigation Evidence & Timeline
- Related Alerts, Machines, Users

**Indicator Actions (10)**
- Submit/Delete/Update Indicators
- File, IP, Domain, URL Indicators
- Batch Submissions

**Live Response Actions (8)**
- Initiate Live Response Sessions
- Run Commands & Scripts
- Library File Management
- Result Downloads

**Software & Vulnerability Management (5)**
- Software Inventory
- Vulnerability Assessment
- Security Recommendations
- Missing KB Analysis

---

#### 2. Microsoft Defender for Office 365 (MDO) - 35 Actions

**Email Message Actions (15)**
- Soft/Hard Delete
- Move (Junk, Inbox, Custom Folder)
- Restore, Purge
- Remove from All Mailboxes
- Mark Read/Unread, Flag/Unflag
- Email Headers & Metadata

**Threat Submission & Investigation (10)**
- Submit Email, Attachments, Files, URLs for Analysis
- Threat Submission Status & Review
- Threat Intelligence
- Email Trace & Tracking

**Tenant Allow/Block Lists (5)**
- Sender, URL, File Block Lists
- Block List Entry Management

**Policy Management (3)**
- Anti-Phishing Policy
- Anti-Spam Policy
- Anti-Malware Policy

**Quarantine Management (2)**
- Release/Delete Quarantined Emails

---

#### 3. Microsoft Defender for Cloud Apps (MCAS) - 40 Actions

**User Governance (12)**
- Suspend/Unsuspend User
- Require Sign-In Again
- Confirm Compromised
- Password Reset
- Notifications (User, Manager, Team)
- Account Enable/Disable
- Group & Role Management
- Access Limitation

**App Governance (8)**
- Revoke App Permissions & Consent
- Block/Unblock Apps
- App Tagging
- Compliance Marking

**Session Control (5)**
- Revoke Sessions & Tokens
- Step-Up Authentication
- End Active Sessions
- Block Downloads

**File Actions (10)**
- Quarantine/Unquarantine
- Sensitivity Labels
- Collaboration & Sharing Control
- Trash & Restore
- Legal Hold Management

**Activity Governance (5)**
- Block Activities
- Alerts & Reports
- Security Team Notifications
- Incident Creation

---

#### 4. Microsoft Defender for Identity (MDI) - 20 Actions

**Account Management (10)**
- Enable/Disable AD Accounts
- Password Operations (Reset, Expire)
- Force Logoff
- Privileged Group Management
- Account Lockout Reset
- Delegation & SmartCard Controls
- Admin Rights Removal

**Alert Management (5)**
- Close, Update, Assign Alerts
- Add Comments
- Escalate Alerts

**Investigation (5)**
- Entity Timeline
- Lateral Movement Paths
- Suspicious Activities
- Security Data Export
- Trigger Investigations

---

#### 5. Microsoft Entra ID - 25 Actions

**User Management (8)**
- Enable/Disable/Delete/Restore Users
- Update Profiles
- License Management
- Manager Assignment

**Authentication Management (8)**
- Revoke Sessions & Tokens
- Password Reset & Force Change
- Block/Unblock Sign-In
- Reset Failed Sign-In Count
- Account Unlock

**MFA Management (4)**
- Reset/Disable User MFA
- Require MFA Registration
- Temporary Access Pass

**Risk Management (5)**
- Confirm Compromised
- Dismiss Risky Users
- Risk Detections & History
- Risk Remediation

---

#### 6. Microsoft Intune - 19 Actions

**Device Wipe & Retirement (4)**
- Full Wipe
- Corporate Data Wipe
- Retire Device
- Delete Device

**Remote Device Actions (5)**
- Remote Lock
- Passcode Reset
- Reboot/Shutdown
- Bypass Activation Lock

**Security Actions (5)**
- Rotate BitLocker Keys
- Rotate FileVault Keys
- Rotate Local Admin Password
- Device Attestation
- Proactive Remediation

**Device Management (3)**
- Sync Device
- Locate Device
- Lost Mode Sound

**Compliance (2)**
- Enable/Disable Lost Mode

---

## API Coverage

### Primary APIs Used

1. **Microsoft Defender for Endpoint API**
   - Base: `https://api.securitycenter.microsoft.com/api`
   - Authentication: Azure AD App Token
   - Scope: `WindowsDefenderATP/.default`

2. **Microsoft Graph API v1.0**
   - Base: `https://graph.microsoft.com/v1.0`
   - Resources: Users, Security, DeviceManagement
   - Scope: `https://graph.microsoft.com/.default`

3. **Microsoft Graph API Beta**
   - Base: `https://graph.microsoft.com/beta`
   - Resources: CloudAppSecurity, IdentityProtection
   - Scope: `https://graph.microsoft.com/.default`

4. **Azure Management API**
   - Base: `https://management.azure.com`
   - Resources: Compute, Network, Security
   - Scope: `https://management.azure.com/.default`

---

## Worker Architecture

```
DefenderXDRGateway
    ?
DefenderXDROrchestrator
    ?
????????????????????????????????????????????????????
?  MDEWorker      (80 actions)                     ?
?  MDOWorker      (35 actions)                     ?
?  MCASWorker     (40 actions)                     ?
?  MDIWorker      (20 actions)                     ?
?  EntraIDWorker  (25 actions)                     ?
?  IntuneWorker   (19 actions)                     ?
????????????????????????????????????????????????????
                   ?
          Microsoft Security APIs
```

---

## Implementation Status

? **Architecture**: Complete  
? **Models**: 219 actions defined  
? **MDE Worker**: Full implementation (80 actions)  
?? **MDO Worker**: Stub (needs 35 implementations)  
?? **MCAS Worker**: Stub (needs 40 implementations)  
?? **MDI Worker**: Stub (needs 20 implementations)  
?? **Entra ID Worker**: Stub (needs 25 implementations)  
?? **Intune Worker**: Stub (needs 19 implementations)  

**Total Implementation Progress**: 80/219 (36.5%)

---

## Next Implementation Priority

1. **Entra ID Worker** (25 actions) - Most critical for identity response
2. **MDO Worker** (35 actions) - Email threats are common
3. **Intune Worker** (19 actions) - Device management
4. **MCAS Worker** (40 actions) - Cloud app governance
5. **MDI Worker** (20 actions) - Advanced identity protection

---

*Last Updated: Current Build*  
*Reference: https://github.com/akefallonitis/defenderc2xsoar*
