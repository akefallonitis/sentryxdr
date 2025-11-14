using Microsoft.Extensions.Logging;
using SentryXDR.Models;
using SentryXDR.Services.Authentication;
using System.Text.Json;

namespace SentryXDR.Services.Workers
{
    public interface IEntraIDSessionService
    {
        // Session Management
        Task<XDRRemediationResponse> RevokeAllUserSessionsAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> InvalidateAllRefreshTokensAsync(XDRRemediationRequest request);
        
        // MFA Management
        Task<XDRRemediationResponse> DeleteAuthenticationMethodAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> ResetAllMFAMethodsAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> RequireMFAReregistrationAsync(XDRRemediationRequest request);
    }

    /// <summary>
    /// Entra ID Session and MFA Management Service
    /// Manages user session revocation and MFA reset operations
    /// API Reference: https://learn.microsoft.com/en-us/graph/api/user-revokesigninsessions
    /// </summary>
    public class EntraIDSessionService : BaseWorkerService, IEntraIDSessionService
    {
        private readonly IMultiTenantAuthService _authService;
        private const string GraphBaseUrl = "https://graph.microsoft.com/v1.0";
        private const string GraphBetaUrl = "https://graph.microsoft.com/beta";

        public EntraIDSessionService(
            ILogger<EntraIDSessionService> logger,
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

        // ==================== Session Management ====================

        /// <summary>
        /// Revoke all user sign-in sessions
        /// POST /users/{id}/revokeSignInSessions
        /// Permission: User.ReadWrite.All, Directory.ReadWrite.All
        /// </summary>
        public async Task<XDRRemediationResponse> RevokeAllUserSessionsAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "RevokeAllUserSessions");
                await SetAuthHeaderAsync(request.TenantId);

                var userId = GetRequiredParameter(request, "userId", out var failureResponse);
                if (userId == null) return failureResponse!;

                Logger.LogWarning("Revoking all sessions for user {UserId} - this will force re-authentication", userId);

                var result = await PostJsonAsync<JsonElement>(
                    $"{GraphBaseUrl}/users/{userId}/revokeSignInSessions", 
                    new { });

                if (result.Success && result.Data.HasValue)
                {
                    var success = result.Data.Value.GetProperty("value").GetBoolean();
                    
                    if (success)
                    {
                        LogOperationComplete(request, "RevokeAllUserSessions", DateTime.UtcNow - startTime, true);
                        
                        return CreateSuccessResponse(request, $"All user sessions revoked successfully", new Dictionary<string, object>
                        {
                            { "userId", userId },
                            { "status", "AllSessionsRevoked" },
                            { "effect", "User will be forced to sign in again on all devices" },
                            { "timestamp", DateTime.UtcNow.ToString("O") }
                        }, startTime);
                    }
                    else
                    {
                        return CreateFailureResponse(request, "Session revocation returned false", startTime);
                    }
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error revoking sessions", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        /// <summary>
        /// Invalidate all refresh tokens for a user
        /// POST /users/{id}/invalidateAllRefreshTokens (Graph Beta)
        /// Permission: User.ReadWrite.All, Directory.ReadWrite.All
        /// </summary>
        public async Task<XDRRemediationResponse> InvalidateAllRefreshTokensAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "InvalidateAllRefreshTokens");
                await SetAuthHeaderAsync(request.TenantId);

                var userId = GetRequiredParameter(request, "userId", out var failureResponse);
                if (userId == null) return failureResponse!;

                Logger.LogWarning("Invalidating all refresh tokens for user {UserId} - this is a critical security action", userId);

                var result = await PostJsonAsync<JsonElement>(
                    $"{GraphBetaUrl}/users/{userId}/invalidateAllRefreshTokens",
                    new { });

                if (result.Success && result.Data.HasValue)
                {
                    var success = result.Data.Value.GetProperty("value").GetBoolean();
                    
                    if (success)
                    {
                        LogOperationComplete(request, "InvalidateAllRefreshTokens", DateTime.UtcNow - startTime, true);
                        
                        return CreateSuccessResponse(request, $"All refresh tokens invalidated successfully", new Dictionary<string, object>
                        {
                            { "userId", userId },
                            { "status", "AllRefreshTokensInvalidated" },
                            { "effect", "All existing refresh tokens are now invalid, user must re-authenticate" },
                            { "timestamp", DateTime.UtcNow.ToString("O") }
                        }, startTime);
                    }
                    else
                    {
                        return CreateFailureResponse(request, "Token invalidation returned false", startTime);
                    }
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error invalidating tokens", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        // ==================== MFA Management ====================

        /// <summary>
        /// Delete specific authentication method for a user
        /// DELETE /users/{id}/authentication/methods/{methodId}
        /// Permission: UserAuthenticationMethod.ReadWrite.All
        /// </summary>
        public async Task<XDRRemediationResponse> DeleteAuthenticationMethodAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "DeleteAuthenticationMethod");
                await SetAuthHeaderAsync(request.TenantId);

                if (!ValidateRequiredParameters(request, out var failureResponse, "userId", "methodId"))
                {
                    return failureResponse!;
                }

                var userId = request.Parameters["userId"]!.ToString()!;
                var methodId = request.Parameters["methodId"]!.ToString()!;

                Logger.LogWarning("Deleting authentication method {MethodId} for user {UserId}", methodId, userId);

                var result = await DeleteAsync($"{GraphBaseUrl}/users/{userId}/authentication/methods/{methodId}");

                if (result.Success)
                {
                    LogOperationComplete(request, "DeleteAuthenticationMethod", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"Authentication method deleted successfully", new Dictionary<string, object>
                    {
                        { "userId", userId },
                        { "methodId", methodId },
                        { "status", "Deleted" },
                        { "note", "User may need to re-register this authentication method" }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error deleting authentication method", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        /// <summary>
        /// Reset all MFA methods for a user (delete all non-password methods)
        /// GET /users/{id}/authentication/methods + DELETE for each method
        /// Permission: UserAuthenticationMethod.ReadWrite.All
        /// </summary>
        public async Task<XDRRemediationResponse> ResetAllMFAMethodsAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "ResetAllMFAMethods");
                await SetAuthHeaderAsync(request.TenantId);

                var userId = GetRequiredParameter(request, "userId", out var failureResponse);
                if (userId == null) return failureResponse!;

                Logger.LogWarning("Resetting all MFA methods for user {UserId} - this is a critical security action", userId);

                // Step 1: Get all authentication methods
                var getResult = await GetJsonAsync<JsonElement>($"{GraphBaseUrl}/users/{userId}/authentication/methods");

                if (!getResult.Success || !getResult.Data.HasValue)
                {
                    return CreateFailureResponse(request, getResult.Error ?? "Failed to retrieve authentication methods", startTime);
                }

                var methods = getResult.Data.Value.GetProperty("value").EnumerateArray().ToList();
                var deletedMethods = new List<string>();
                var failedMethods = new List<string>();

                // Step 2: Delete all non-password methods
                foreach (var method in methods)
                {
                    var methodId = method.GetProperty("id").GetString()!;
                    var methodType = method.GetProperty("@odata.type").GetString()!;

                    // Skip password methods (can't delete primary password)
                    if (methodType.Contains("passwordAuthenticationMethod", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var deleteResult = await DeleteAsync($"{GraphBaseUrl}/users/{userId}/authentication/methods/{methodId}");
                    
                    if (deleteResult.Success)
                    {
                        deletedMethods.Add($"{methodType}:{methodId}");
                        Logger.LogInformation("Deleted authentication method {MethodType} ({MethodId})", methodType, methodId);
                    }
                    else
                    {
                        failedMethods.Add($"{methodType}:{methodId}");
                        Logger.LogWarning("Failed to delete authentication method {MethodType} ({MethodId}): {Error}", 
                            methodType, methodId, deleteResult.Error);
                    }
                }

                LogOperationComplete(request, "ResetAllMFAMethods", DateTime.UtcNow - startTime, true);
                
                return CreateSuccessResponse(request, $"MFA reset completed: {deletedMethods.Count} methods deleted, {failedMethods.Count} failed", new Dictionary<string, object>
                {
                    { "userId", userId },
                    { "deletedMethods", deletedMethods },
                    { "failedMethods", failedMethods },
                    { "status", failedMethods.Any() ? "PartialSuccess" : "AllMethodsDeleted" },
                    { "note", "User will need to re-register MFA on next sign-in" }
                }, startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        /// <summary>
        /// Require user to re-register MFA methods
        /// PATCH /users/{id}/authentication/requirements (Graph Beta)
        /// Permission: UserAuthenticationMethod.ReadWrite.All
        /// </summary>
        public async Task<XDRRemediationResponse> RequireMFAReregistrationAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "RequireMFAReregistration");
                await SetAuthHeaderAsync(request.TenantId);

                var userId = GetRequiredParameter(request, "userId", out var failureResponse);
                if (userId == null) return failureResponse!;

                Logger.LogWarning("Requiring MFA re-registration for user {UserId}", userId);

                // This requires setting authentication requirements
                var requirements = new
                {
                    perUserMfaState = "Enforced",
                    requireReregistration = true
                };

                var result = await PatchJsonAsync<JsonElement>(
                    $"{GraphBetaUrl}/users/{userId}/authentication/requirements",
                    requirements);

                if (result.Success)
                {
                    LogOperationComplete(request, "RequireMFAReregistration", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"MFA re-registration required successfully", new Dictionary<string, object>
                    {
                        { "userId", userId },
                        { "status", "ReregistrationRequired" },
                        { "effect", "User must re-register all MFA methods on next sign-in" },
                        { "timestamp", DateTime.UtcNow.ToString("O") }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error requiring MFA re-registration", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }
    }
}
