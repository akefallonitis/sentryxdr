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
    /// XDR Actions mapped to Microsoft Graph API and product-specific APIs
    /// Reference: https://learn.microsoft.com/en-us/graph/api/resources/security-api-overview
    /// </summary>
    public enum XDRAction
    {
        // ==================== MDE Actions (Microsoft Defender for Endpoint) ====================
        // API: https://api.securitycenter.microsoft.com/api
        // Graph API: https://graph.microsoft.com/v1.0/security/
        
        // Machine Actions - https://learn.microsoft.com/en-us/defender-endpoint/api/machineaction
        IsolateDevice,                          // POST /machines/{id}/isolate
        ReleaseDeviceFromIsolation,             // POST /machines/{id}/unisolate
        RestrictAppExecution,                   // POST /machines/{id}/restrictCodeExecution
        RemoveAppRestriction,                   // POST /machines/{id}/unrestrictCodeExecution
        RunAntivirusScan,                       // POST /machines/{id}/runAntiVirusScan
        RunQuickScan,                           // POST /machines/{id}/runAntiVirusScan (Quick)
        RunFullScan,                            // POST /machines/{id}/runAntiVirusScan (Full)
        OffboardMachine,                        // POST /machines/{id}/offboard
        StopAndQuarantineFile,                  // POST /machines/{id}/StopAndQuarantineFile
        CollectInvestigationPackage,            // POST /machines/{id}/collectInvestigationPackage
        GetInvestigationPackageSasUri,          // GET /machineactions/{id}/GetPackageUri
        
        // Alert Actions
        UpdateAlert,                            // PATCH /alerts/{id}
        CreateAlertComment,                     // POST /alerts/{id}/comments
        GetAlertRelatedMachines,                // GET /alerts/{id}/machines
        GetAlertRelatedFiles,                   // GET /alerts/{id}/files
        
        // Investigation Actions
        StartAutomatedInvestigation,            // POST /alerts/{id}/startInvestigation
        GetInvestigationById,                   // GET /investigations/{id}
        GetInvestigationActions,                // GET /investigations/{id}/actions
        
        // Indicator Actions
        SubmitIndicator,                        // POST /indicators
        DeleteIndicator,                        // DELETE /indicators/{id}
        UpdateIndicator,                        // PATCH /indicators/{id}
        
        // Live Response
        InitiateLiveResponseSession,            // POST /machines/{id}/LiveResponse
        RunLiveResponseCommand,                 // POST /machines/{id}/runliveresponsecommand
        GetLiveResponseResult,                  // GET /machineactions/{id}/GetLiveResponseResultDownloadLink
        
        // Software & Vulnerability
        GetMachineSoftware,                     // GET /machines/{id}/software
        GetMachineVulnerabilities,              // GET /machines/{id}/vulnerabilities
        GetMachineRecommendations,              // GET /machines/{id}/recommendations
        
        // ==================== MDO Actions (Microsoft Defender for Office 365) ====================
        // Graph API: https://graph.microsoft.com/v1.0/security/
        
        // Email Actions - Graph API
        SoftDeleteEmail,                        // POST /security/emailThreats/{id}/delete
        HardDeleteEmail,                        // DELETE /users/{id}/messages/{messageId}
        MoveEmailToJunk,                        // POST /users/{id}/messages/{messageId}/move
        MoveEmailToInbox,                       // POST /users/{id}/messages/{messageId}/move
        RestoreEmailFromDeletedItems,           // POST /users/{id}/messages/{messageId}/move
        
        // Threat Investigation
        SubmitEmailForAnalysis,                 // POST /security/threatSubmission/emailThreats
        GetEmailThreatSubmission,               // GET /security/threatSubmission/emailThreats/{id}
        
        // Tenant Allow/Block Lists
        AddSenderToBlockList,                   // POST /security/threatSubmission/emailThreatSubmissionPolicies
        RemoveSenderFromBlockList,              // DELETE /security/threatSubmission/emailThreatSubmissionPolicies/{id}
        AddUrlToBlockList,                      // POST /security/threatSubmission/urlThreatSubmissionPolicies
        AddFileToBlockList,                     // POST /security/threatSubmission/fileThreatSubmissionPolicies
        
        // Anti-phishing & Anti-spam
        UpdateAntiPhishingPolicy,               // PATCH /security/antiPhishingPolicies/{id}
        UpdateAntiSpamPolicy,                   // PATCH /security/antiSpamPolicies/{id}
        
        // Quarantine Management
        ReleaseQuarantinedEmail,                // POST /security/quarantine/messages/{id}/release
        DeleteQuarantinedEmail,                 // DELETE /security/quarantine/messages/{id}
        PreviewQuarantinedEmail,                // GET /security/quarantine/messages/{id}
        
        // ==================== MCAS/Defender for Cloud Apps Actions ====================
        // Graph API Beta: https://graph.microsoft.com/beta/security/
        
        // User Actions
        SuspendUser,                            // POST /security/cloudAppSecurityProfiles/{id}/suspend
        RequireUserToSignInAgain,               // POST /users/{id}/revokeSignInSessions
        ConfirmUserCompromised,                 // POST /identityProtection/riskyUsers/confirmCompromised
        
        // App Governance
        RevokeAppPermission,                    // DELETE /servicePrincipals/{id}/appRoleAssignments/{id}
        DisableApp,                             // PATCH /servicePrincipals/{id}
        
        // Session Control
        RevokeUserSessions,                     // POST /users/{id}/revokeSignInSessions
        RequireStepUpAuth,                      // Conditional Access Policy
        
        // File Actions
        QuarantineFile,                         // POST /security/cloudAppSecurity/files/{id}/quarantine
        ApplySensitivityLabel,                  // POST /security/informationProtection/sensitivityLabels/{id}/apply
        RemoveCollaboration,                    // DELETE /drives/{id}/items/{item-id}/permissions/{perm-id}
        
        // Activity Governance
        BlockUserActivity,                      // POST /security/cloudAppSecurity/activities/{id}/block
        NotifyUser,                             // POST /security/cloudAppSecurity/activities/{id}/notify
        
        // ==================== MDI Actions (Microsoft Defender for Identity) ====================
        
        // Account Actions
        DisableADAccount,                       // Graph API: PATCH /users/{id}
        RequirePasswordReset,                   // POST /users/{id}/authentication/passwordMethods/{id}/resetPassword
        CloseSecurityAlert,                     // PATCH /security/alerts_v2/{id}
        
        // ==================== Entra ID Actions ====================
        // Graph API: https://graph.microsoft.com/v1.0/
        
        // User Management
        DisableUserAccount,                     // PATCH /users/{id} {"accountEnabled": false}
        EnableUserAccount,                      // PATCH /users/{id} {"accountEnabled": true}
        DeleteUser,                             // DELETE /users/{id}
        RestoreDeletedUser,                     // POST /directory/deletedItems/{id}/restore
        
        // Authentication Management
        RevokeUserSignInSessions,               // POST /users/{id}/revokeSignInSessions
        RevokeUserRefreshTokens,                // POST /users/{id}/invalidateAllRefreshTokens
        ResetUserPassword,                      // POST /users/{id}/authentication/passwordMethods/{id}/resetPassword
        ForcePasswordChange,                    // PATCH /users/{id}/authentication/passwordProfile
        
        // MFA Management
        ResetUserMFA,                           // DELETE /users/{id}/authentication/methods/{id}
        DisableUserMFA,                         // DELETE /users/{id}/authentication/phoneMethods/{id}
        RequireMFARegistration,                 // POST /identity/conditionalAccess/policies
        
        // Risk Management
        ConfirmUserCompromised_EntraID,         // POST /identityProtection/riskyUsers/confirmCompromised
        DismissRiskyUser,                       // POST /identityProtection/riskyUsers/dismiss
        
        // ==================== Intune Actions ====================
        // Graph API: https://graph.microsoft.com/v1.0/deviceManagement
        
        // Device Wipe
        WipeDevice,                             // POST /deviceManagement/managedDevices/{id}/wipe
        RetireDevice,                           // POST /deviceManagement/managedDevices/{id}/retire
        DeleteDevice,                           // DELETE /deviceManagement/managedDevices/{id}
        
        // Remote Actions
        RemoteLockDevice,                       // POST /deviceManagement/managedDevices/{id}/remoteLock
        ResetPasscode,                          // POST /deviceManagement/managedDevices/{id}/resetPasscode
        RebootDevice,                           // POST /deviceManagement/managedDevices/{id}/rebootNow
        ShutDownDevice,                         // POST /deviceManagement/managedDevices/{id}/shutDown
        
        // Security Actions
        RotateBitLockerKeys,                    // POST /deviceManagement/managedDevices/{id}/rotateBitLockerKeys
        RotateFileVaultKey,                     // POST /deviceManagement/managedDevices/{id}/rotateFileVaultKey
        InitiateDeviceAttestation,              // POST /deviceManagement/managedDevices/{id}/initiateDeviceAttestation
        
        // Sync and Update
        SyncDevice,                             // POST /deviceManagement/managedDevices/{id}/syncDevice
        UpdateWindowsDeviceAccount,             // POST /deviceManagement/managedDevices/{id}/updateWindowsDeviceAccount
        
        // Device Management
        LocateDevice,                           // POST /deviceManagement/managedDevices/{id}/locateDevice
        DisableLostMode,                        // POST /deviceManagement/managedDevices/{id}/disableLostMode
        EnableLostMode,                         // POST /deviceManagement/managedDevices/{id}/enableLostMode
        
        // ==================== Azure Security Actions ====================
        // Azure Management API: https://management.azure.com/
        
        // Virtual Machine Actions
        StopVM,                                 // POST /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Compute/virtualMachines/{vm}/powerOff
        StartVM,                                // POST /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Compute/virtualMachines/{vm}/start
        RestartVM,                              // POST /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Compute/virtualMachines/{vm}/restart
        DeallocateVM,                           // POST /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Compute/virtualMachines/{vm}/deallocate
        DeleteVM,                               // DELETE /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Compute/virtualMachines/{vm}
        
        // Network Security Group
        CreateNSGRule,                          // PUT /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Network/networkSecurityGroups/{nsg}/securityRules/{rule}
        DeleteNSGRule,                          // DELETE /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Network/networkSecurityGroups/{nsg}/securityRules/{rule}
        UpdateNSGRule,                          // PATCH /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Network/networkSecurityGroups/{nsg}/securityRules/{rule}
        
        // Network Isolation
        IsolateVMNetwork,                       // PUT /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Network/networkInterfaces/{nic}
        BlockIPAddress,                         // PUT /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Network/azureFirewalls/{firewall}
        
        // Just-in-Time Access
        EnableJITAccess,                        // PUT /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Security/locations/{location}/jitNetworkAccessPolicies/{policy}
        DisableJITAccess,                       // DELETE /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Security/locations/{location}/jitNetworkAccessPolicies/{policy}
        
        // Security Recommendations
        ApplySecurityRecommendation,            // POST /subscriptions/{sub}/providers/Microsoft.Security/tasks/{taskId}/activate
        DismissSecurityAlert,                   // POST /subscriptions/{sub}/providers/Microsoft.Security/alerts/{alertId}/dismiss
        
        // Resource Management
        ApplyResourceLock,                      // PUT /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Authorization/locks/{lock}
        RemoveResourceLock,                     // DELETE /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Authorization/locks/{lock}
        
        // RBAC
        RevokeRoleAssignment,                   // DELETE /subscriptions/{sub}/providers/Microsoft.Authorization/roleAssignments/{assignment}
        CreateRoleAssignment,                   // PUT /subscriptions/{sub}/providers/Microsoft.Authorization/roleAssignments/{assignment}
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
