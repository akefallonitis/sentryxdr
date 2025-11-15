using Microsoft.Extensions.Logging;
using SentryXDR.Models;
using SentryXDR.Services.Authentication;
using System.Text.Json;

namespace SentryXDR.Services.Workers
{
    public interface IEntraIDAdvancedService
    {
        // Privileged Access Management
        Task<XDRRemediationResponse> RemoveFromPrivilegedRoleAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> RevokeAdminConsentAsync(XDRRemediationRequest request);
        
        // Guest Access Management
        Task<XDRRemediationResponse> RevokeGuestAccessAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> DeleteGuestUserAsync(XDRRemediationRequest request);
        
        // Additional Identity Actions
        Task<XDRRemediationResponse> ForcePasswordChangeAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> ConfirmUserCompromisedAsync(XDRRemediationRequest request);
    }

    /// <summary>
    /// Entra ID Advanced Service
    /// Handles privileged access, guest management, and identity risk
    /// API Reference: https://learn.microsoft.com/en-us/graph/api/resources/users
    /// </summary>
    public class EntraIDAdvancedService : BaseWorkerService, IEntraIDAdvancedService
    {
        private readonly IMultiTenantAuthService _authService;
        private const string GraphBaseUrl = "https://graph.microsoft.com/v1.0";
        private const string GraphBetaUrl = "https://graph.microsoft.com/beta";

        public EntraIDAdvancedService(
            ILogger<EntraIDAdvancedService> logger,
            IMultiTenantAuthService authService,
            HttpClient httpClient) : base(logger, httpClient)
        {
            _authService = authService;
        }

        private async Task SetAuthHeaderAsync(string tenantId)
        {
            var token = await _authService.GetGraphTokenAsync(tenantId);
            SetBearerToken(token);
        }

        // ==================== Privileged Access Management ====================

        /// <summary>
        /// Remove user from privileged role (e.g., Global Admin, Security Admin)
        /// DELETE /roleManagement/directory/roleAssignments/{id}
        /// Permission: RoleManagement.ReadWrite.Directory
        /// </summary>
        public async Task<XDRRemediationResponse> RemoveFromPrivilegedRoleAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "RemoveFromPrivilegedRole");
                await SetAuthHeaderAsync(request.TenantId);

                if (!ValidateRequiredParameters(request, out var failureResponse, "userId", "roleId"))
                {
                    return failureResponse!;
                }

                var userId = request.Parameters["userId"]!.ToString()!;
                var roleId = request.Parameters["roleId"]!.ToString()!;

                Logger.LogCritical("REMOVE PRIVILEGED ROLE: Removing user {UserId} from role {RoleId}", userId, roleId);

                // First, find the role assignment ID
                var assignmentsUrl = $"{GraphBaseUrl}/roleManagement/directory/roleAssignments?$filter=principalId eq '{userId}' and roleDefinitionId eq '{roleId}'";
                var assignmentsResult = await GetJsonAsync<JsonElement>(assignmentsUrl);

                if (!assignmentsResult.Success || assignmentsResult.Data.ValueKind == JsonValueKind.Undefined)
                {
                    return CreateFailureResponse(request, "Unable to find role assignment", startTime);
                }

                var assignments = assignmentsResult.Data.GetProperty("value");
                if (assignments.GetArrayLength() == 0)
                {
                    return CreateFailureResponse(request, "No role assignment found for this user", startTime);
                }

                var assignmentId = assignments[0].GetProperty("id").GetString();

                // Delete the role assignment
                var deleteResult = await DeleteAsync($"{GraphBaseUrl}/roleManagement/directory/roleAssignments/{assignmentId}");

                if (deleteResult.Success)
                {
                    LogOperationComplete(request, "RemoveFromPrivilegedRole", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"User removed from privileged role successfully", new Dictionary<string, object>
                    {
                        { "userId", userId },
                        { "roleId", roleId },
                        { "assignmentId", assignmentId! },
                        { "action", "RoleRemoved" },
                        { "effect", "User no longer has privileged access" }
                    }, startTime);
                }

                return CreateFailureResponse(request, deleteResult.Error ?? "Unknown error removing role", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        /// <summary>
        /// Revoke admin consent for application (OAuth app)
        /// DELETE /oauth2PermissionGrants/{id}
        /// Permission: Directory.ReadWrite.All
        /// </summary>
        public async Task<XDRRemediationResponse> RevokeAdminConsentAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "RevokeAdminConsent");
                await SetAuthHeaderAsync(request.TenantId);

                if (!ValidateRequiredParameters(request, out var failureResponse, "servicePrincipalId"))
                {
                    return failureResponse!;
                }

                var servicePrincipalId = request.Parameters["servicePrincipalId"]!.ToString()!;

                Logger.LogCritical("REVOKE ADMIN CONSENT: Revoking admin consent for service principal {ServicePrincipalId}", servicePrincipalId);

                // Get all OAuth2 permission grants for this service principal
                var grantsUrl = $"{GraphBaseUrl}/oauth2PermissionGrants?$filter=clientId eq '{servicePrincipalId}'";
                var grantsResult = await GetJsonAsync<JsonElement>(grantsUrl);

                if (!grantsResult.Success || grantsResult.Data.ValueKind == JsonValueKind.Undefined)
                {
                    return CreateFailureResponse(request, "Unable to find permission grants", startTime);
                }

                var grants = grantsResult.Data.GetProperty("value");
                var revokedCount = 0;

                // Delete each grant
                foreach (var grant in grants.EnumerateArray())
                {
                    var grantId = grant.GetProperty("id").GetString();
                    var deleteResult = await DeleteAsync($"{GraphBaseUrl}/oauth2PermissionGrants/{grantId}");
                    if (deleteResult.Success)
                    {
                        revokedCount++;
                    }
                }

                if (revokedCount > 0)
                {
                    LogOperationComplete(request, "RevokeAdminConsent", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"Admin consent revoked successfully", new Dictionary<string, object>
                    {
                        { "servicePrincipalId", servicePrincipalId },
                        { "revokedGrants", revokedCount },
                        { "action", "ConsentRevoked" },
                        { "effect", "Application permissions have been removed" }
                    }, startTime);
                }

                return CreateFailureResponse(request, "No permission grants found to revoke", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        // ==================== Guest Access Management ====================

        /// <summary>
        /// Revoke guest user access (disable + revoke tokens)
        /// PATCH /users/{id}
        /// Permission: User.ReadWrite.All
        /// </summary>
        public async Task<XDRRemediationResponse> RevokeGuestAccessAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "RevokeGuestAccess");
                await SetAuthHeaderAsync(request.TenantId);

                var userId = GetRequiredParameter(request, "userId", out var failureResponse);
                if (userId == null) return failureResponse!;

                Logger.LogCritical("REVOKE GUEST ACCESS: Revoking guest user {UserId} access", userId);

                // Step 1: Disable the user account
                var disableBody = new
                {
                    accountEnabled = false
                };

                var disableResult = await PatchJsonAsync<JsonElement>(
                    $"{GraphBaseUrl}/users/{userId}",
                    disableBody);

                if (!disableResult.Success)
                {
                    return CreateFailureResponse(request, disableResult.Error ?? "Unable to disable guest user", startTime);
                }

                // Step 2: Revoke all refresh tokens
                var revokeResult = await PostJsonAsync<JsonElement>(
                    $"{GraphBaseUrl}/users/{userId}/revokeSignInSessions",
                    new { });

                if (revokeResult.Success)
                {
                    LogOperationComplete(request, "RevokeGuestAccess", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"Guest access revoked successfully", new Dictionary<string, object>
                    {
                        { "userId", userId },
                        { "action", "GuestAccessRevoked" },
                        { "effect", "Guest user is disabled and all sessions revoked" }
                    }, startTime);
                }

                return CreateFailureResponse(request, "Failed to revoke refresh tokens", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        /// <summary>
        /// Delete guest user permanently
        /// DELETE /users/{id}
        /// Permission: User.ReadWrite.All
        /// </summary>
        public async Task<XDRRemediationResponse> DeleteGuestUserAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "DeleteGuestUser");
                await SetAuthHeaderAsync(request.TenantId);

                var userId = GetRequiredParameter(request, "userId", out var failureResponse);
                if (userId == null) return failureResponse!;

                Logger.LogCritical("DELETE GUEST USER: Permanently deleting guest user {UserId}", userId);

                var result = await DeleteAsync($"{GraphBaseUrl}/users/{userId}");

                if (result.Success)
                {
                    LogOperationComplete(request, "DeleteGuestUser", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"Guest user deleted successfully", new Dictionary<string, object>
                    {
                        { "userId", userId },
                        { "action", "GuestUserDeleted" },
                        { "effect", "Guest user is permanently removed from directory" },
                        { "warning", "This action cannot be undone" }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error deleting guest user", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        // ==================== Additional Identity Actions ====================

        /// <summary>
        /// Force password change on next sign-in
        /// PATCH /users/{id}
        /// Permission: User.ReadWrite.All
        /// </summary>
        public async Task<XDRRemediationResponse> ForcePasswordChangeAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "ForcePasswordChange");
                await SetAuthHeaderAsync(request.TenantId);

                var userId = GetRequiredParameter(request, "userId", out var failureResponse);
                if (userId == null) return failureResponse!;

                Logger.LogWarning("FORCE PASSWORD CHANGE: Requiring password change for user {UserId}", userId);

                var updateBody = new
                {
                    passwordProfile = new
                    {
                        forceChangePasswordNextSignIn = true
                    }
                };

                var result = await PatchJsonAsync<JsonElement>(
                    $"{GraphBaseUrl}/users/{userId}",
                    updateBody);

                if (result.Success)
                {
                    LogOperationComplete(request, "ForcePasswordChange", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"Password change required successfully", new Dictionary<string, object>
                    {
                        { "userId", userId },
                        { "action", "ForcePasswordChange" },
                        { "effect", "User must change password on next sign-in" }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error forcing password change", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        /// <summary>
        /// Confirm user as compromised (triggers identity protection)
        /// POST /identityProtection/riskyUsers/confirmCompromised
        /// Permission: IdentityRiskyUser.ReadWrite.All
        /// </summary>
        public async Task<XDRRemediationResponse> ConfirmUserCompromisedAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "ConfirmUserCompromised");
                await SetAuthHeaderAsync(request.TenantId);

                var userId = GetRequiredParameter(request, "userId", out var failureResponse);
                if (userId == null) return failureResponse!;

                Logger.LogCritical("CONFIRM COMPROMISED: Marking user {UserId} as compromised in Identity Protection", userId);

                var confirmRequest = new
                {
                    userIds = new[] { userId }
                };

                var result = await PostJsonAsync<JsonElement>(
                    $"{GraphBaseUrl}/identityProtection/riskyUsers/confirmCompromised",
                    confirmRequest);

                if (result.Success)
                {
                    LogOperationComplete(request, "ConfirmUserCompromised", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"User confirmed as compromised successfully", new Dictionary<string, object>
                    {
                        { "userId", userId },
                        { "action", "ConfirmCompromised" },
                        { "effect", "Identity Protection will enforce remediation policies" },
                        { "note", "Conditional Access policies will now apply to this user" }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error confirming user compromised", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }
    }
}

