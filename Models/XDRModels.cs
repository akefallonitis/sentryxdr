namespace SentryXDR.Models
{
    /// <summary>
    /// Multi-tenant XDR remediation request
    /// Based on Microsoft Defender XDR APIs
    /// </summary>
    public class XDRRemediationRequest
    {
        public string RequestId { get; set; } = Guid.NewGuid().ToString();
        public string TenantId { get; set; } = string.Empty;
        public string IncidentId { get; set; } = string.Empty;
        public XDRPlatform Platform { get; set; }
        public XDRAction Action { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
        public string InitiatedBy { get; set; } = string.Empty;
        public RemediationPriority Priority { get; set; } = RemediationPriority.Medium;
        public string Justification { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public Dictionary<string, string> Metadata { get; set; } = new();
    }

    /// <summary>
    /// XDR remediation response
    /// </summary>
    public class XDRRemediationResponse
    {
        public string RequestId { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;
        public string IncidentId { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public Dictionary<string, object> Details { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
        public TimeSpan Duration { get; set; }
        public string? ActionId { get; set; }
    }

    /// <summary>
    /// Microsoft XDR Platforms
    /// </summary>
    public enum XDRPlatform
    {
        MDE,        // Microsoft Defender for Endpoint
        MDO,        // Microsoft Defender for Office 365
        MCAS,       // Microsoft Defender for Cloud Apps (formerly MCAS)
        MDI,        // Microsoft Defender for Identity
        EntraID,    // Microsoft Entra ID (formerly Azure AD)
        Intune,     // Microsoft Intune
        Azure       // Azure Security
    }

    /// <summary>
    /// XDR remediation actions across all platforms (219 total actions)
    /// Reference: https://learn.microsoft.com/en-us/graph/api/resources/security-api-overview
    /// Based on: https://github.com/akefallonitis/defenderc2xsoar
    /// </summary>
    public enum XDRAction
    {
        // ==================== MDE Actions (Microsoft Defender for Endpoint) - 80 Actions ====================
        // API: https://api.securitycenter.microsoft.com/api
        // Graph API: https://graph.microsoft.com/v1.0/security/
        
        // Machine Actions - Device Control (20)
        IsolateDevice,                          // POST /machines/{id}/isolate
        ReleaseDeviceFromIsolation,             // POST /machines/{id}/unisolate
        RestrictAppExecution,                   // POST /machines/{id}/restrictCodeExecution
        RemoveAppRestriction,                   // POST /machines/{id}/unrestrictCodeExecution
        RunAntivirusScan,                       // POST /machines/{id}/runAntiVirusScan
        RunQuickScan,                           // POST /machines/{id}/runAntiVirusScan (Quick)
        RunFullScan,                            // POST /machines/{id}/runAntiVirusScan (Full)
        RunOfflineScan,                         // POST /machines/{id}/runAntiVirusScan (Offline)
        UpdateAntivirusSignatures,              // POST /machines/{id}/updateAVSignatures
        OffboardMachine,                        // POST /machines/{id}/offboard
        CollectInvestigationPackage,            // POST /machines/{id}/collectInvestigationPackage
        GetInvestigationPackageSasUri,          // GET /machineactions/{id}/GetPackageUri
        CancelMachineAction,                    // POST /machineactions/{id}/cancel
        GetMachineAction,                       // GET /machineactions/{id}
        ListMachineActions,                     // GET /machines/{id}/machineactions
        TagMachine,                             // POST /machines/{id}/tag
        UntagMachine,                           // DELETE /machines/{id}/tag
        GetMachineById,                         // GET /machines/{id}
        GetMachineByIp,                         // GET /machines/findbyip(ip='{ip}')
        GetMachineLogonUsers,                   // GET /machines/{id}/logonusers
        
        // File Actions (15)
        StopAndQuarantineFile,                  // POST /machines/{id}/StopAndQuarantineFile
        RemoveFileFromQuarantine,               // POST /machines/{id}/unquarantinefile
        GetFileInfo,                            // GET /files/{id}
        GetFileStatistics,                      // GET /files/{id}/stats
        GetFileMachines,                        // GET /files/{id}/machines
        GetFileAlerts,                          // GET /files/{id}/alerts
        CollectFile,                            // POST /machines/{id}/collectFile
        DownloadFile,                           // GET /machineactions/{id}/GetFileDownloadLink
        BlockFile,                              // POST /files/{sha1}/block
        UnblockFile,                            // DELETE /files/{sha1}/block
        GetFileInstances,                       // GET /files/{id}/instances
        GetGlobalFilePrevalence,                // GET /files/{id}/globalprevalence
        GetOrgFilePrevalence,                   // GET /files/{id}/orgprevalence
        GetFileActivities,                      // GET /files/{id}/activities
        SetFileReputation,                      // POST /files/{id}/setreputation
        
        // Alert Actions (12)
        UpdateAlert,                            // PATCH /alerts/{id}
        CreateAlertComment,                     // POST /alerts/{id}/comments
        GetAlert,                               // GET /alerts/{id}
        ListAlerts,                             // GET /alerts
        GetAlertRelatedMachines,                // GET /alerts/{id}/machines
        GetAlertRelatedFiles,                   // GET /alerts/{id}/files
        GetAlertRelatedIPs,                     // GET /alerts/{id}/ips
        GetAlertRelatedDomains,                 // GET /alerts/{id}/domains
        GetAlertRelatedUsers,                   // GET /alerts/{id}/users
        ResolveAlert,                           // PATCH /alerts/{id} (status=Resolved)
        ClassifyAlert,                          // PATCH /alerts/{id} (classification)
        AssignAlert,                            // PATCH /alerts/{id} (assignedTo)
        
        // Investigation Actions (10)
        StartAutomatedInvestigation,            // POST /alerts/{id}/startInvestigation
        GetInvestigationById,                   // GET /investigations/{id}
        GetInvestigationActions,                // GET /investigations/{id}/actions
        ListInvestigations,                     // GET /investigations
        GetInvestigationAlerts,                 // GET /investigations/{id}/alerts
        GetInvestigationEvidence,               // GET /investigations/{id}/evidence
        GetInvestigationMachines,               // GET /investigations/{id}/machines
        GetInvestigationUsers,                  // GET /investigations/{id}/users
        CancelInvestigation,                    // POST /investigations/{id}/cancel
        RestartInvestigation,                   // POST /investigations/{id}/restart
        
        // Indicator Actions (10)
        SubmitIndicator,                        // POST /indicators
        DeleteIndicator,                        // DELETE /indicators/{id}
        UpdateIndicator,                        // PATCH /indicators/{id}
        GetIndicator,                           // GET /indicators/{id}
        ListIndicators,                         // GET /indicators
        SubmitFileIndicator,                    // POST /indicators (file)
        SubmitIPIndicator,                      // POST /indicators (ip)
        SubmitDomainIndicator,                  // POST /indicators (domain)
        SubmitURLIndicator,                     // POST /indicators (url)
        BatchSubmitIndicators,                  // POST /indicators/batch
        
        // Live Response Actions (8)
        InitiateLiveResponseSession,            // POST /machines/{id}/LiveResponse
        RunLiveResponseCommand,                 // POST /machines/{id}/runliveresponsecommand
        GetLiveResponseResult,                  // GET /machineactions/{id}/GetLiveResponseResultDownloadLink
        UploadFileToLibrary,                    // POST /libraryfiles
        DeleteFileFromLibrary,                  // DELETE /libraryfiles/{id}
        GetLibraryFile,                         // GET /libraryfiles/{id}
        ListLibraryFiles,                       // GET /libraryfiles
        RunScript,                              // POST /machines/{id}/runscript
        
        // Software & Vulnerability Management (5)
        GetMachineSoftware,                     // GET /machines/{id}/software
        GetMachineVulnerabilities,              // GET /machines/{id}/vulnerabilities
        GetMachineRecommendations,              // GET /machines/{id}/recommendations
        GetMissingKBs,                          // GET /machines/{id}/missingkbs
        ExportSoftwareInventory,                // GET /machines/SoftwareInventoryExport
        
        // ==================== MDO Actions (Microsoft Defender for Office 365) - 35 Actions ====================
        
        // Email Message Actions (15)
        SoftDeleteEmail,                        // POST /users/{id}/messages/{messageId}/delete
        HardDeleteEmail,                        // DELETE /users/{id}/messages/{messageId}
        MoveEmailToJunk,                        // POST /users/{id}/messages/{messageId}/move
        MoveEmailToInbox,                       // POST /users/{id}/messages/{messageId}/move
        MoveEmailToFolder,                      // POST /users/{id}/messages/{messageId}/move
        RestoreEmailFromDeletedItems,           // POST /users/{id}/messages/{messageId}/restore
        PurgeEmailFromMailbox,                  // POST /users/{id}/messages/{messageId}/purge
        RemoveEmailFromAllMailboxes,            // POST /security/emailRemoval
        GetEmailMessage,                        // GET /users/{id}/messages/{messageId}
        ListEmailMessages,                      // GET /users/{id}/messages
        GetEmailHeaders,                        // GET /users/{id}/messages/{messageId}/$value
        MarkEmailAsRead,                        // PATCH /users/{id}/messages/{messageId}
        MarkEmailAsUnread,                      // PATCH /users/{id}/messages/{messageId}
        FlagEmail,                              // PATCH /users/{id}/messages/{messageId}
        UnflagEmail,                            // PATCH /users/{id}/messages/{messageId}
        
        // Threat Submission & Investigation (10)
        SubmitEmailForAnalysis,                 // POST /security/threatSubmission/emailThreats
        SubmitEmailAttachmentForAnalysis,       // POST /security/threatSubmission/emailContentThreats
        SubmitFileForAnalysis,                  // POST /security/threatSubmission/fileContentThreats
        SubmitURLForAnalysis,                   // POST /security/threatSubmission/urlThreats
        GetEmailThreatSubmission,               // GET /security/threatSubmission/emailThreats/{id}
        GetThreatSubmissionStatus,              // GET /security/threatSubmission/{id}
        ReviewThreatSubmission,                 // POST /security/threatSubmission/{id}/review
        GetThreatIntelligence,                  // GET /security/threatIntelligence
        TrackEmailDelivery,                     // POST /security/messageTrace
        GetEmailTrace,                          // GET /security/messageTrace/{id}
        
        // Tenant Allow/Block Lists (5)
        AddSenderToBlockList,                   // POST /security/threatSubmission/emailThreatSubmissionPolicies
        RemoveSenderFromBlockList,              // DELETE /security/threatSubmission/emailThreatSubmissionPolicies/{id}
        AddUrlToBlockList,                      // POST /security/threatSubmission/urlThreatSubmissionPolicies
        AddFileToBlockList,                     // POST /security/threatSubmission/fileThreatSubmissionPolicies
        GetBlockListEntries,                    // GET /security/threatSubmission/policies
        
        // Policy Management (3)
        UpdateAntiPhishingPolicy,               // PATCH /security/antiPhishingPolicies/{id}
        UpdateAntiSpamPolicy,                   // PATCH /security/antiSpamPolicies/{id}
        UpdateAntiMalwarePolicy,                // PATCH /security/antiMalwarePolicies/{id}
        
        // Quarantine Management (2)
        ReleaseQuarantinedEmail,                // POST /security/quarantine/messages/{id}/release
        DeleteQuarantinedEmail,                 // DELETE /security/quarantine/messages/{id}
        
        // ==================== MCAS/Defender for Cloud Apps Actions - 40 Actions ====================
        
        // User Governance (12)
        SuspendUser,                            // POST /security/cloudAppSecurityProfiles/{id}/suspend
        UnsuspendUser,                          // POST /security/cloudAppSecurityProfiles/{id}/unsuspend
        RequireUserToSignInAgain,               // POST /users/{id}/revokeSignInSessions
        ConfirmUserCompromised,                 // POST /identityProtection/riskyUsers/confirmCompromised
        RequireUserPasswordReset,               // POST /users/{id}/authentication/passwordMethods/resetPassword
        NotifyUser,                             // POST /security/cloudAppSecurity/activities/{id}/notify
        NotifyManager,                          // POST /security/cloudAppSecurity/activities/{id}/notifyManager
        DisableUserAccountMCAS,                 // PATCH /users/{id}
        EnableUserAccountMCAS,                  // PATCH /users/{id}
        RemoveUserFromGroup,                    // DELETE /groups/{id}/members/{userId}
        RevokeAdminPrivileges,                  // DELETE /directoryRoles/{id}/members/{userId}
        LimitUserAccess,                        // POST /security/conditionalAccess/policies
        
        // App Governance (8)
        RevokeAppPermission,                    // DELETE /servicePrincipals/{id}/appRoleAssignments/{id}
        DisableApp,                             // PATCH /servicePrincipals/{id}
        RevokeAppConsent,                       // DELETE /oauth2PermissionGrants/{id}
        BlockApp,                               // POST /security/cloudAppSecurity/apps/{id}/block
        UnblockApp,                             // DELETE /security/cloudAppSecurity/apps/{id}/block
        TagApp,                                 // POST /security/cloudAppSecurity/apps/{id}/tag
        UntagApp,                               // DELETE /security/cloudAppSecurity/apps/{id}/tag
        MarkAppAsCompliant,                     // PATCH /security/cloudAppSecurity/apps/{id}
        
        // Session Control (5)
        RevokeUserSessions,                     // POST /users/{id}/revokeSignInSessions
        RevokeRefreshTokens,                    // POST /users/{id}/invalidateAllRefreshTokens
        RequireStepUpAuth,                      // Conditional Access Policy
        EndActiveSession,                       // DELETE /users/{id}/onlineMeetings/{meetingId}
        BlockDownload,                          // POST /security/cloudAppSecurity/sessions/{id}/block
        
        // File Actions (10)
        QuarantineFile,                         // POST /security/cloudAppSecurity/files/{id}/quarantine
        UnquarantineFile,                       // DELETE /security/cloudAppSecurity/files/{id}/quarantine
        ApplySensitivityLabel,                  // POST /security/informationProtection/sensitivityLabels/{id}/apply
        RemoveSensitivityLabel,                 // DELETE /drives/{id}/items/{item-id}/label
        RemoveCollaboration,                    // DELETE /drives/{id}/items/{item-id}/permissions/{perm-id}
        RemoveExternalSharing,                  // DELETE /drives/{id}/items/{item-id}/permissions
        TrashFile,                              // DELETE /drives/{id}/items/{item-id}
        RestoreFile,                            // POST /drives/{id}/items/{item-id}/restore
        PutFileInLegalHold,                     // POST /security/cases/ediscoveryCases/{id}/legalHolds
        RemoveFileFromLegalHold,                // DELETE /security/cases/ediscoveryCases/{id}/legalHolds/{holdId}
        
        // Activity Governance (5)
        BlockUserActivity,                      // POST /security/cloudAppSecurity/activities/{id}/block
        AlertOnActivity,                        // POST /security/cloudAppSecurity/activities/{id}/alert
        GenerateActivityReport,                 // POST /security/cloudAppSecurity/activities/export
        NotifySecurityTeam,                     // POST /security/cloudAppSecurity/activities/{id}/notifyTeam
        CreateIncident,                         // POST /security/incidents
        
        // ==================== MDI Actions (Microsoft Defender for Identity) - 20 Actions ====================
        
        // Account Management (10)
        DisableADAccount,                       // PATCH /users/{id}
        EnableADAccount,                        // PATCH /users/{id}
        RequirePasswordReset,                   // POST /users/{id}/authentication/passwordMethods/{id}/resetPassword
        ExpirePassword,                         // PATCH /users/{id}/passwordProfile
        ForceLogoff,                            // POST /users/{id}/revokeSignInSessions
        RemoveFromPrivilegedGroup,              // DELETE /groups/{id}/members/{userId}
        ResetAccountLockout,                    // POST /users/{id}/resetLockout
        DisableAccountDelegation,               // PATCH /users/{id}/onPremisesExtensionAttributes
        RequireSmartCardLogon,                  // PATCH /users/{id}/onPremisesExtensionAttributes
        RemoveAdminRights,                      // DELETE /directoryRoles/{id}/members/{userId}
        
        // Alert Management (5)
        CloseSecurityAlert,                     // PATCH /security/alerts_v2/{id}
        UpdateAlertStatus,                      // PATCH /security/alerts_v2/{id}
        AssignAlertToAnalyst,                   // PATCH /security/alerts_v2/{id}
        AddAlertComment,                        // POST /security/alerts_v2/{id}/comments
        EscalateAlert,                          // PATCH /security/alerts_v2/{id}
        
        // Investigation (5)
        GetEntityTimeline,                      // GET /security/alerts_v2/{id}/timeline
        GetLateralMovementPaths,                // GET /security/alerts_v2/{id}/lateralMovementPaths
        GetSuspiciousActivities,                // GET /security/alerts_v2/{id}/activities
        ExportSecurityData,                     // POST /security/alerts_v2/export
        TriggerInvestigation,                   // POST /security/investigations
        
        // ==================== Entra ID Actions - 25 Actions ====================
        
        // User Management (8)
        DisableUserAccount,                     // PATCH /users/{id} {"accountEnabled": false}
        EnableUserAccount,                      // PATCH /users/{id} {"accountEnabled": true}
        DeleteUser,                             // DELETE /users/{id}
        RestoreDeletedUser,                     // POST /directory/deletedItems/{id}/restore
        UpdateUserProfile,                      // PATCH /users/{id}
        AssignLicense,                          // POST /users/{id}/assignLicense
        RevokeLicense,                          // POST /users/{id}/assignLicense (remove)
        SetUserManager,                         // PUT /users/{id}/manager/$ref
        
        // Authentication Management (8)
        RevokeUserSignInSessions,               // POST /users/{id}/revokeSignInSessions
        RevokeUserRefreshTokens,                // POST /users/{id}/invalidateAllRefreshTokens
        ResetUserPassword,                      // POST /users/{id}/authentication/passwordMethods/{id}/resetPassword
        ForcePasswordChange,                    // PATCH /users/{id}/authentication/passwordProfile
        BlockSignIn,                            // PATCH /users/{id}/authentication/signInPreferences
        UnblockSignIn,                          // PATCH /users/{id}/authentication/signInPreferences
        ResetFailedSignInCount,                 // POST /users/{id}/authentication/resetFailedSignInCount
        UnlockAccount,                          // POST /users/{id}/authentication/unlock
        
        // MFA Management (4)
        ResetUserMFA,                           // DELETE /users/{id}/authentication/methods/{id}
        DisableUserMFA,                         // DELETE /users/{id}/authentication/phoneMethods/{id}
        RequireMFARegistration,                 // POST /identity/conditionalAccess/policies
        BypassMFAOnce,                          // POST /users/{id}/authentication/temporaryAccessPass
        
        // Risk Management (5)
        ConfirmUserCompromised_EntraID,         // POST /identityProtection/riskyUsers/confirmCompromised
        DismissRiskyUser,                       // POST /identityProtection/riskyUsers/dismiss
        GetUserRiskDetections,                  // GET /identityProtection/riskDetections
        GetUserRiskHistory,                     // GET /identityProtection/riskyUsers/{id}/history
        RemediateUserRisk,                      // POST /identityProtection/riskyUsers/{id}/remediate
        
        // ==================== Intune Actions - 19 Actions ====================
        
        // Device Wipe & Retirement (4)
        WipeDevice,                             // POST /deviceManagement/managedDevices/{id}/wipe
        WipeCorporateData,                      // POST /deviceManagement/managedDevices/{id}/wipeCorporateData
        RetireDevice,                           // POST /deviceManagement/managedDevices/{id}/retire
        DeleteDevice,                           // DELETE /deviceManagement/managedDevices/{id}
        
        // Remote Device Actions (5)
        RemoteLockDevice,                       // POST /deviceManagement/managedDevices/{id}/remoteLock
        ResetPasscode,                          // POST /deviceManagement/managedDevices/{id}/resetPasscode
        RebootDevice,                           // POST /deviceManagement/managedDevices/{id}/rebootNow
        ShutDownDevice,                         // POST /deviceManagement/managedDevices/{id}/shutDown
        BypassActivationLock,                   // POST /deviceManagement/managedDevices/{id}/bypassActivationLock
        
        // Security Actions (5)
        RotateBitLockerKeys,                    // POST /deviceManagement/managedDevices/{id}/rotateBitLockerKeys
        RotateFileVaultKey,                     // POST /deviceManagement/managedDevices/{id}/rotateFileVaultKey
        RotateLocalAdminPassword,               // POST /deviceManagement/managedDevices/{id}/rotateLocalAdminPassword
        InitiateDeviceAttestation,              // POST /deviceManagement/managedDevices/{id}/initiateDeviceAttestation
        InitiateOnDemandProactiveRemediation,   // POST /deviceManagement/managedDevices/{id}/initiateOnDemandProactiveRemediation
        
        // Device Management (3)
        SyncDevice,                             // POST /deviceManagement/managedDevices/{id}/syncDevice
        LocateDevice,                           // POST /deviceManagement/managedDevices/{id}/locateDevice
        PlayLostModeSound,                      // POST /deviceManagement/managedDevices/{id}/playLostModeSound
        
        // Compliance (2)
        EnableLostMode,                         // POST /deviceManagement/managedDevices/{id}/enableLostMode
        DisableLostMode,                        // POST /deviceManagement/managedDevices/{id}/disableLostMode
        
        // ==================== Azure Security Actions - 20 Actions ====================
        // Azure Management API: https://management.azure.com/
        
        // Virtual Machine Actions (5)
        StopVM,                                 // POST /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Compute/virtualMachines/{vm}/powerOff
        StartVM,                                // POST /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Compute/virtualMachines/{vm}/start
        RestartVM,                              // POST /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Compute/virtualMachines/{vm}/restart
        DeallocateVM,                           // POST /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Compute/virtualMachines/{vm}/deallocate
        DeleteVM,                               // DELETE /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Compute/virtualMachines/{vm}
        
        // Network Security Group (3)
        CreateNSGRule,                          // PUT /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Network/networkSecurityGroups/{nsg}/securityRules/{rule}
        DeleteNSGRule,                          // DELETE /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Network/networkSecurityGroups/{nsg}/securityRules/{rule}
        UpdateNSGRule,                          // PATCH /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Network/networkSecurityGroups/{nsg}/securityRules/{rule}
        
        // Network Isolation (2)
        IsolateVMNetwork,                       // PUT /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Network/networkInterfaces/{nic}
        BlockIPAddress,                         // PUT /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Network/azureFirewalls/{firewall}
        
        // Just-in-Time Access (2)
        EnableJITAccess,                        // PUT /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Security/locations/{location}/jitNetworkAccessPolicies/{policy}
        DisableJITAccess,                       // DELETE /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Security/locations/{location}/jitNetworkAccessPolicies/{policy}
        
        // Security Recommendations (2)
        ApplySecurityRecommendation,            // POST /subscriptions/{sub}/providers/Microsoft.Security/tasks/{taskId}/activate
        DismissSecurityAlert,                   // POST /subscriptions/{sub}/providers/Microsoft.Security/alerts/{alertId}/dismiss
        
        // Resource Management (2)
        ApplyResourceLock,                      // PUT /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Authorization/locks/{lock}
        RemoveResourceLock,                     // DELETE /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Authorization/locks/{lock}
        
        // RBAC (2)
        RevokeRoleAssignment,                   // DELETE /subscriptions/{sub}/providers/Microsoft.Authorization/roleAssignments/{assignment}
        CreateRoleAssignment,                   // PUT /subscriptions/{sub}/providers/Microsoft.Authorization/roleAssignments/{assignment}
        
        // Azure Defender (2)
        EnableAzureDefender,                    // PUT /subscriptions/{sub}/providers/Microsoft.Security/pricings/{tier}
        DisableAzureDefender                    // DELETE /subscriptions/{sub}/providers/Microsoft.Security/pricings/{tier}
    }

    /// <summary>
    /// Remediation priority levels
    /// </summary>
    public enum RemediationPriority
    {
        Low = 1,
        Medium = 2,
        High = 3,
        Critical = 4
    }

    /// <summary>
    /// Tenant configuration for multi-tenancy support
    /// </summary>
    public class TenantConfiguration
    {
        public string TenantId { get; set; } = string.Empty;
        public string TenantName { get; set; } = string.Empty;
        public Dictionary<XDRPlatform, bool> EnabledPlatforms { get; set; } = new();
        public Dictionary<string, string> CustomSettings { get; set; } = new();
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastUpdated { get; set; }
    }

    /// <summary>
    /// Audit log entry for compliance and tracking
    /// </summary>
    public class AuditLogEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string TenantId { get; set; } = string.Empty;
        public string RequestId { get; set; } = string.Empty;
        public string IncidentId { get; set; } = string.Empty;
        public XDRPlatform Platform { get; set; }
        public XDRAction Action { get; set; }
        public string InitiatedBy { get; set; } = string.Empty;
        public string TargetResource { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string Result { get; set; } = string.Empty;
        public string Justification { get; set; } = string.Empty;
        public Dictionary<string, object> Details { get; set; } = new();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Machine action response from MDE API
    /// </summary>
    public class MDEMachineActionResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string MachineId { get; set; } = string.Empty;
        public DateTime CreationDateTimeUtc { get; set; }
        public DateTime LastUpdateTimeUtc { get; set; }
        public string? Error { get; set; }
    }
}
