using Microsoft.Extensions.Logging;
using SentryXDR.Models;
using SentryXDR.Services.Authentication;
using System.Text.Json;

namespace SentryXDR.Services.Workers
{
    public interface IMCASApiService
    {
        // OAuth App Governance
        Task<XDRRemediationResponse> RevokeOAuthAppAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> DisableOAuthAppAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> BanOAuthAppAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> MarkOAuthAppAsUnusualAsync(XDRRemediationRequest request);
        
        // Session Control
        Task<XDRRemediationResponse> BlockUserSessionAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> ForceLogoutUserAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> RevokeRefreshTokensAsync(XDRRemediationRequest request);
        
        // File Governance
        Task<XDRRemediationResponse> QuarantineFileAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> RemoveExternalSharingAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> TrashFileAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> ApplySensitivityLabelAsync(XDRRemediationRequest request);
        
        // Malware Detection
        Task<XDRRemediationResponse> BlockMalwareFileAsync(XDRRemediationRequest request);
    }

    /// <summary>
    /// Microsoft Defender for Cloud Apps (MCAS) API Service
    /// Implements 12 Cloud App Security remediation actions
    /// API Reference: https://learn.microsoft.com/en-us/defender-cloud-apps/api-introduction
    /// </summary>
    public class MCASApiService : BaseWorkerService, IMCASApiService
    {
        private readonly IMultiTenantAuthService _authService;
        private const string MCASBaseUrl = "https://portal.cloudappsecurity.com/api/v1";

        public MCASApiService(
            ILogger<MCASApiService> logger,
            IMultiTenantAuthService authService,
            HttpClient httpClient) : base(logger, httpClient)
        {
            _authService = authService;
        }

        private async Task SetAuthHeaderAsync(string tenantId)
        {
            var mcasToken = await _authService.GetMCASTokenAsync(tenantId);
            SetAuthHeader("Token", mcasToken);
            HttpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        }

        // ==================== OAuth App Governance ====================

        /// <summary>
        /// Revoke OAuth app permissions
        /// POST /api/v1/oauth_apps/{appId}/revoke
        /// Permission: MCAS Admin
        /// </summary>
        public async Task<XDRRemediationResponse> RevokeOAuthAppAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "RevokeOAuthApp");
                await SetAuthHeaderAsync(request.TenantId);

                var appId = GetRequiredParameter(request, "appId", out var failureResponse);
                if (appId == null) return failureResponse!;

                Logger.LogCritical("REVOKE OAUTH: Revoking OAuth app {AppId}", appId);

                var result = await PostJsonAsync<JsonElement>(
                    $"{MCASBaseUrl}/oauth_apps/{appId}/revoke",
                    new { comment = request.Justification ?? "Revoked by SentryXDR" });

                if (result.Success)
                {
                    LogOperationComplete(request, "RevokeOAuthApp", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"OAuth app '{appId}' revoked successfully", new Dictionary<string, object>
                    {
                        { "appId", appId },
                        { "action", "Revoked" },
                        { "effect", "All user permissions for this app have been revoked" }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error revoking OAuth app", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        /// <summary>
        /// Disable OAuth app (prevent new installations)
        /// POST /api/v1/oauth_apps/{appId}/disable
        /// Permission: MCAS Admin
        /// </summary>
        public async Task<XDRRemediationResponse> DisableOAuthAppAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "DisableOAuthApp");
                await SetAuthHeaderAsync(request.TenantId);

                var appId = GetRequiredParameter(request, "appId", out var failureResponse);
                if (appId == null) return failureResponse!;

                Logger.LogWarning("DISABLE OAUTH: Disabling OAuth app {AppId}", appId);

                var result = await PostJsonAsync<JsonElement>(
                    $"{MCASBaseUrl}/oauth_apps/{appId}/disable",
                    new { comment = request.Justification ?? "Disabled by SentryXDR" });

                if (result.Success)
                {
                    LogOperationComplete(request, "DisableOAuthApp", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"OAuth app '{appId}' disabled successfully", new Dictionary<string, object>
                    {
                        { "appId", appId },
                        { "action", "Disabled" },
                        { "effect", "No new users can install this app" }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error disabling OAuth app", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        /// <summary>
        /// Ban OAuth app completely
        /// POST /api/v1/oauth_apps/{appId}/ban
        /// Permission: MCAS Admin
        /// </summary>
        public async Task<XDRRemediationResponse> BanOAuthAppAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "BanOAuthApp");
                await SetAuthHeaderAsync(request.TenantId);

                var appId = GetRequiredParameter(request, "appId", out var failureResponse);
                if (appId == null) return failureResponse!;

                Logger.LogCritical("BAN OAUTH: Banning OAuth app {AppId} - complete removal", appId);

                var result = await PostJsonAsync<JsonElement>(
                    $"{MCASBaseUrl}/oauth_apps/{appId}/ban",
                    new { comment = request.Justification ?? "Banned by SentryXDR - security threat" });

                if (result.Success)
                {
                    LogOperationComplete(request, "BanOAuthApp", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"OAuth app '{appId}' banned successfully", new Dictionary<string, object>
                    {
                        { "appId", appId },
                        { "action", "Banned" },
                        { "effect", "App is completely blocked from the organization" }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error banning OAuth app", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        /// <summary>
        /// Mark OAuth app as unusual for investigation
        /// POST /api/v1/oauth_apps/{appId}/mark_unusual
        /// Permission: MCAS Admin
        /// </summary>
        public async Task<XDRRemediationResponse> MarkOAuthAppAsUnusualAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "MarkOAuthAppAsUnusual");
                await SetAuthHeaderAsync(request.TenantId);

                var appId = GetRequiredParameter(request, "appId", out var failureResponse);
                if (appId == null) return failureResponse!;

                Logger.LogWarning("MARK UNUSUAL: Marking OAuth app {AppId} as unusual", appId);

                var result = await PostJsonAsync<JsonElement>(
                    $"{MCASBaseUrl}/oauth_apps/{appId}/mark_unusual",
                    new { comment = request.Justification ?? "Marked unusual by SentryXDR" });

                if (result.Success)
                {
                    LogOperationComplete(request, "MarkOAuthAppAsUnusual", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"OAuth app '{appId}' marked as unusual", new Dictionary<string, object>
                    {
                        { "appId", appId },
                        { "action", "MarkedUnusual" },
                        { "effect", "App is flagged for security review" }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error marking app as unusual", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        // ==================== Session Control ====================

        /// <summary>
        /// Block user session in cloud app
        /// POST /api/v1/activities/{activityId}/block_user
        /// Permission: MCAS Admin
        /// </summary>
        public async Task<XDRRemediationResponse> BlockUserSessionAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "BlockUserSession");
                await SetAuthHeaderAsync(request.TenantId);

                if (!ValidateRequiredParameters(request, out var failureResponse, "userId", "appId"))
                {
                    return failureResponse!;
                }

                var userId = request.Parameters["userId"]!.ToString()!;
                var appId = request.Parameters["appId"]!.ToString()!;

                Logger.LogWarning("BLOCK SESSION: Blocking user {UserId} session in app {AppId}", userId, appId);

                var blockRequest = new
                {
                    userId = userId,
                    appId = appId,
                    action = "block_user",
                    comment = request.Justification ?? "Session blocked by SentryXDR"
                };

                var result = await PostJsonAsync<JsonElement>(
                    $"{MCASBaseUrl}/activities/governance",
                    blockRequest);

                if (result.Success)
                {
                    LogOperationComplete(request, "BlockUserSession", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"User session blocked successfully", new Dictionary<string, object>
                    {
                        { "userId", userId },
                        { "appId", appId },
                        { "action", "SessionBlocked" }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error blocking session", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        /// <summary>
        /// Force logout user from cloud app
        /// POST /api/v1/activities/governance
        /// Permission: MCAS Admin
        /// </summary>
        public async Task<XDRRemediationResponse> ForceLogoutUserAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "ForceLogoutUser");
                await SetAuthHeaderAsync(request.TenantId);

                var userId = GetRequiredParameter(request, "userId", out var failureResponse);
                if (userId == null) return failureResponse!;

                Logger.LogWarning("FORCE LOGOUT: Forcing logout for user {UserId}", userId);

                var logoutRequest = new
                {
                    action = "LOGOUT_USER",
                    user = userId,
                    comment = request.Justification ?? "Forced logout by SentryXDR"
                };

                var result = await PostJsonAsync<JsonElement>(
                    $"{MCASBaseUrl}/activities/governance",
                    logoutRequest);

                if (result.Success)
                {
                    LogOperationComplete(request, "ForceLogoutUser", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"User forced to logout successfully", new Dictionary<string, object>
                    {
                        { "userId", userId },
                        { "action", "ForcedLogout" },
                        { "effect", "User must re-authenticate to access cloud apps" }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error forcing logout", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        /// <summary>
        /// Revoke refresh tokens for user in cloud apps
        /// POST /api/v1/activities/governance
        /// Permission: MCAS Admin
        /// </summary>
        public async Task<XDRRemediationResponse> RevokeRefreshTokensAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "RevokeRefreshTokens");
                await SetAuthHeaderAsync(request.TenantId);

                var userId = GetRequiredParameter(request, "userId", out var failureResponse);
                if (userId == null) return failureResponse!;

                Logger.LogCritical("REVOKE TOKENS: Revoking refresh tokens for user {UserId}", userId);

                var revokeRequest = new
                {
                    action = "REVOKE_REFRESH_TOKENS",
                    user = userId,
                    comment = request.Justification ?? "Tokens revoked by SentryXDR"
                };

                var result = await PostJsonAsync<JsonElement>(
                    $"{MCASBaseUrl}/activities/governance",
                    revokeRequest);

                if (result.Success)
                {
                    LogOperationComplete(request, "RevokeRefreshTokens", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"Refresh tokens revoked successfully", new Dictionary<string, object>
                    {
                        { "userId", userId },
                        { "action", "TokensRevoked" },
                        { "effect", "All existing refresh tokens are now invalid" }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error revoking tokens", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        // ==================== File Governance ====================

        /// <summary>
        /// Quarantine file in cloud storage
        /// POST /api/v1/files/{fileId}/quarantine
        /// Permission: MCAS Admin
        /// </summary>
        public async Task<XDRRemediationResponse> QuarantineFileAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "QuarantineFile");
                await SetAuthHeaderAsync(request.TenantId);

                var fileId = GetRequiredParameter(request, "fileId", out var failureResponse);
                if (fileId == null) return failureResponse!;

                Logger.LogWarning("QUARANTINE FILE: Quarantining file {FileId}", fileId);

                var quarantineRequest = new
                {
                    action = "quarantine",
                    comment = request.Justification ?? "File quarantined by SentryXDR"
                };

                var result = await PostJsonAsync<JsonElement>(
                    $"{MCASBaseUrl}/files/{fileId}/quarantine",
                    quarantineRequest);

                if (result.Success)
                {
                    LogOperationComplete(request, "QuarantineFile", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"File quarantined successfully", new Dictionary<string, object>
                    {
                        { "fileId", fileId },
                        { "action", "Quarantined" },
                        { "effect", "File is isolated and inaccessible to users" }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error quarantining file", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        /// <summary>
        /// Remove external sharing from file
        /// POST /api/v1/files/{fileId}/remove_sharing
        /// Permission: MCAS Admin
        /// </summary>
        public async Task<XDRRemediationResponse> RemoveExternalSharingAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "RemoveExternalSharing");
                await SetAuthHeaderAsync(request.TenantId);

                var fileId = GetRequiredParameter(request, "fileId", out var failureResponse);
                if (fileId == null) return failureResponse!;

                Logger.LogWarning("REMOVE SHARING: Removing external sharing from file {FileId}", fileId);

                var removeRequest = new
                {
                    action = "remove_external_sharing",
                    comment = request.Justification ?? "External sharing removed by SentryXDR"
                };

                var result = await PostJsonAsync<JsonElement>(
                    $"{MCASBaseUrl}/files/{fileId}/governance",
                    removeRequest);

                if (result.Success)
                {
                    LogOperationComplete(request, "RemoveExternalSharing", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"External sharing removed successfully", new Dictionary<string, object>
                    {
                        { "fileId", fileId },
                        { "action", "SharingRemoved" },
                        { "effect", "File is no longer shared externally" }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error removing sharing", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        /// <summary>
        /// Move file to trash
        /// POST /api/v1/files/{fileId}/trash
        /// Permission: MCAS Admin
        /// </summary>
        public async Task<XDRRemediationResponse> TrashFileAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "TrashFile");
                await SetAuthHeaderAsync(request.TenantId);

                var fileId = GetRequiredParameter(request, "fileId", out var failureResponse);
                if (fileId == null) return failureResponse!;

                Logger.LogWarning("TRASH FILE: Moving file {FileId} to trash", fileId);

                var trashRequest = new
                {
                    action = "trash",
                    comment = request.Justification ?? "File trashed by SentryXDR"
                };

                var result = await PostJsonAsync<JsonElement>(
                    $"{MCASBaseUrl}/files/{fileId}/governance",
                    trashRequest);

                if (result.Success)
                {
                    LogOperationComplete(request, "TrashFile", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"File moved to trash successfully", new Dictionary<string, object>
                    {
                        { "fileId", fileId },
                        { "action", "Trashed" },
                        { "note", "File can be recovered from trash within retention period" }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error trashing file", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        /// <summary>
        /// Apply sensitivity label to file
        /// POST /api/v1/files/{fileId}/apply_label
        /// Permission: MCAS Admin
        /// </summary>
        public async Task<XDRRemediationResponse> ApplySensitivityLabelAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "ApplySensitivityLabel");
                await SetAuthHeaderAsync(request.TenantId);

                if (!ValidateRequiredParameters(request, out var failureResponse, "fileId", "labelId"))
                {
                    return failureResponse!;
                }

                var fileId = request.Parameters["fileId"]!.ToString()!;
                var labelId = request.Parameters["labelId"]!.ToString()!;

                Logger.LogInformation("APPLY LABEL: Applying sensitivity label {LabelId} to file {FileId}", labelId, fileId);

                var labelRequest = new
                {
                    action = "apply_classification_label",
                    labelId = labelId,
                    comment = request.Justification ?? "Label applied by SentryXDR"
                };

                var result = await PostJsonAsync<JsonElement>(
                    $"{MCASBaseUrl}/files/{fileId}/governance",
                    labelRequest);

                if (result.Success)
                {
                    LogOperationComplete(request, "ApplySensitivityLabel", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"Sensitivity label applied successfully", new Dictionary<string, object>
                    {
                        { "fileId", fileId },
                        { "labelId", labelId },
                        { "action", "LabelApplied" }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error applying label", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        // ==================== Malware Detection ====================

        /// <summary>
        /// Block file detected as malware
        /// POST /api/v1/files/{fileId}/block_malware
        /// Permission: MCAS Admin
        /// </summary>
        public async Task<XDRRemediationResponse> BlockMalwareFileAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "BlockMalwareFile");
                await SetAuthHeaderAsync(request.TenantId);

                var fileId = GetRequiredParameter(request, "fileId", out var failureResponse);
                if (fileId == null) return failureResponse!;

                Logger.LogCritical("BLOCK MALWARE: Blocking malware file {FileId}", fileId);

                var blockRequest = new
                {
                    action = "block_malware",
                    comment = request.Justification ?? "Malware file blocked by SentryXDR"
                };

                var result = await PostJsonAsync<JsonElement>(
                    $"{MCASBaseUrl}/files/{fileId}/governance",
                    blockRequest);

                if (result.Success)
                {
                    LogOperationComplete(request, "BlockMalwareFile", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"Malware file blocked successfully", new Dictionary<string, object>
                    {
                        { "fileId", fileId },
                        { "action", "MalwareBlocked" },
                        { "effect", "File is quarantined and blocked from download" }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error blocking malware", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }
    }
}
