using Microsoft.Extensions.Logging;
using SentryXDR.Models;
using SentryXDR.Services.Authentication;
using System.Text.Json;

namespace SentryXDR.Services.Workers
{
    public interface IDLPRemediationService
    {
        // File Sharing Remediation
        Task<XDRRemediationResponse> RemoveExternalSharingAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> RevokeAnonymousLinkAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> BreakInheritanceAsync(XDRRemediationRequest request);
        
        // File Quarantine
        Task<XDRRemediationResponse> QuarantineSensitiveFileAsync(XDRRemediationRequest request);
        
        // Notification
        Task<XDRRemediationResponse> NotifyDataOwnerAsync(XDRRemediationRequest request);
    }

    /// <summary>
    /// DLP Remediation Service
    /// Handles Data Loss Prevention remediation actions
    /// Focus: Actual remediation, not policy configuration
    /// API Reference: https://learn.microsoft.com/en-us/graph/api/resources/driveitem
    /// </summary>
    public class DLPRemediationService : BaseWorkerService, IDLPRemediationService
    {
        private readonly IMultiTenantAuthService _authService;
        private const string GraphBaseUrl = "https://graph.microsoft.com/v1.0";

        public DLPRemediationService(
            ILogger<DLPRemediationService> logger,
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

        // ==================== File Sharing Remediation ====================

        /// <summary>
        /// Remove external sharing permissions from a file/folder
        /// DELETE /drives/{drive-id}/items/{item-id}/permissions/{perm-id}
        /// Permission: Files.ReadWrite.All
        /// </summary>
        public async Task<XDRRemediationResponse> RemoveExternalSharingAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "RemoveExternalSharing");
                await SetAuthHeaderAsync(request.TenantId);

                if (!ValidateRequiredParameters(request, out var failureResponse, "driveId", "itemId"))
                {
                    return failureResponse!;
                }

                var driveId = request.Parameters["driveId"]!.ToString()!;
                var itemId = request.Parameters["itemId"]!.ToString()!;

                Logger.LogCritical("REMOVE EXTERNAL SHARING: Revoking external permissions for item {ItemId} in drive {DriveId}", itemId, driveId);

                // Step 1: Get all permissions
                var permissionsResult = await GetJsonAsync<JsonElement>(
                    $"{GraphBaseUrl}/drives/{driveId}/items/{itemId}/permissions");

                if (!permissionsResult.Success || permissionsResult.Data.ValueKind == JsonValueKind.Undefined)
                {
                    return CreateFailureResponse(request, "Unable to get file permissions", startTime);
                }

                var permissions = permissionsResult.Data.GetProperty("value");
                var removedCount = 0;

                // Step 2: Remove external/anonymous permissions
                foreach (var permission in permissions.EnumerateArray())
                {
                    var permId = permission.GetProperty("id").GetString();
                    
                    // Check if it's external/anonymous
                    var hasLink = permission.TryGetProperty("link", out var link);
                    var hasGrantedTo = permission.TryGetProperty("grantedTo", out var grantedTo);
                    
                    var isExternal = hasLink && link.TryGetProperty("scope", out var scope) && 
                                    (scope.GetString() == "anonymous" || scope.GetString() == "organization");
                    
                    if (isExternal)
                    {
                        var deleteResult = await DeleteAsync($"{GraphBaseUrl}/drives/{driveId}/items/{itemId}/permissions/{permId}");
                        if (deleteResult.Success)
                        {
                            removedCount++;
                            Logger.LogWarning("Removed external permission: {PermId}", permId);
                        }
                    }
                }

                LogOperationComplete(request, "RemoveExternalSharing", DateTime.UtcNow - startTime, true);
                
                return CreateSuccessResponse(request, $"Removed {removedCount} external sharing permissions", new Dictionary<string, object>
                {
                    { "driveId", driveId },
                    { "itemId", itemId },
                    { "removedPermissions", removedCount },
                    { "action", "RevokeExternalAccess" },
                    { "effect", "File/folder is no longer externally accessible" }
                }, startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        /// <summary>
        /// Revoke anonymous sharing link
        /// DELETE /drives/{drive-id}/items/{item-id}/permissions/{perm-id}
        /// Permission: Files.ReadWrite.All
        /// </summary>
        public async Task<XDRRemediationResponse> RevokeAnonymousLinkAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "RevokeAnonymousLink");
                await SetAuthHeaderAsync(request.TenantId);

                if (!ValidateRequiredParameters(request, out var failureResponse, "driveId", "itemId", "permissionId"))
                {
                    return failureResponse!;
                }

                var driveId = request.Parameters["driveId"]!.ToString()!;
                var itemId = request.Parameters["itemId"]!.ToString()!;
                var permissionId = request.Parameters["permissionId"]!.ToString()!;

                Logger.LogCritical("REVOKE ANONYMOUS LINK: Deleting anonymous sharing link for item {ItemId}", itemId);

                var result = await DeleteAsync($"{GraphBaseUrl}/drives/{driveId}/items/{itemId}/permissions/{permissionId}");

                if (result.Success)
                {
                    LogOperationComplete(request, "RevokeAnonymousLink", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"Anonymous sharing link revoked successfully", new Dictionary<string, object>
                    {
                        { "driveId", driveId },
                        { "itemId", itemId },
                        { "permissionId", permissionId },
                        { "action", "RevokeAnonymousLink" },
                        { "effect", "Link is no longer accessible" }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error revoking link", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        /// <summary>
        /// Break permission inheritance to isolate file/folder
        /// POST /drives/{drive-id}/items/{item-id}/createLink (with specific permissions)
        /// Permission: Files.ReadWrite.All
        /// </summary>
        public async Task<XDRRemediationResponse> BreakInheritanceAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "BreakInheritance");
                await SetAuthHeaderAsync(request.TenantId);

                if (!ValidateRequiredParameters(request, out var failureResponse, "driveId", "itemId"))
                {
                    return failureResponse!;
                }

                var driveId = request.Parameters["driveId"]!.ToString()!;
                var itemId = request.Parameters["itemId"]!.ToString()!;

                Logger.LogWarning("BREAK INHERITANCE: Stopping permission inheritance for item {ItemId}", itemId);

                // Get current permissions
                var permissionsResult = await GetJsonAsync<JsonElement>(
                    $"{GraphBaseUrl}/drives/{driveId}/items/{itemId}/permissions");

                if (!permissionsResult.Success || permissionsResult.Data.ValueKind == JsonValueKind.Undefined)
                {
                    return CreateFailureResponse(request, "Unable to get file permissions", startTime);
                }

                var permissions = permissionsResult.Data.GetProperty("value");
                var removedCount = 0;

                // Remove inherited permissions
                foreach (var permission in permissions.EnumerateArray())
                {
                    var isInherited = permission.TryGetProperty("inheritedFrom", out var _);
                    if (isInherited)
                    {
                        var permId = permission.GetProperty("id").GetString();
                        var deleteResult = await DeleteAsync($"{GraphBaseUrl}/drives/{driveId}/items/{itemId}/permissions/{permId}");
                        if (deleteResult.Success) removedCount++;
                    }
                }

                LogOperationComplete(request, "BreakInheritance", DateTime.UtcNow - startTime, true);
                
                return CreateSuccessResponse(request, $"Inheritance broken, removed {removedCount} inherited permissions", new Dictionary<string, object>
                {
                    { "driveId", driveId },
                    { "itemId", itemId },
                    { "removedInheritedPermissions", removedCount },
                    { "action", "BreakInheritance" },
                    { "effect", "File/folder now has isolated permissions" }
                }, startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        // ==================== File Quarantine ====================

        /// <summary>
        /// Quarantine sensitive file by moving to secure location
        /// POST /drives/{drive-id}/items/{item-id}/move
        /// Permission: Files.ReadWrite.All
        /// </summary>
        public async Task<XDRRemediationResponse> QuarantineSensitiveFileAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "QuarantineSensitiveFile");
                await SetAuthHeaderAsync(request.TenantId);

                if (!ValidateRequiredParameters(request, out var failureResponse, "driveId", "itemId", "quarantineFolderId"))
                {
                    return failureResponse!;
                }

                var driveId = request.Parameters["driveId"]!.ToString()!;
                var itemId = request.Parameters["itemId"]!.ToString()!;
                var quarantineFolderId = request.Parameters["quarantineFolderId"]!.ToString()!;

                Logger.LogCritical("QUARANTINE FILE: Moving sensitive file {ItemId} to quarantine folder {QuarantineFolder}", itemId, quarantineFolderId);

                var moveRequest = new
                {
                    parentReference = new
                    {
                        id = quarantineFolderId
                    }
                };

                var result = await PatchJsonAsync<JsonElement>(
                    $"{GraphBaseUrl}/drives/{driveId}/items/{itemId}",
                    moveRequest);

                if (result.Success && result.Data.ValueKind != JsonValueKind.Undefined)
                {
                    var newLocation = result.Data.GetProperty("parentReference").GetProperty("path").GetString();
                    
                    LogOperationComplete(request, "QuarantineSensitiveFile", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"File quarantined successfully", new Dictionary<string, object>
                    {
                        { "itemId", itemId },
                        { "quarantineFolderId", quarantineFolderId },
                        { "newLocation", newLocation! },
                        { "action", "Quarantine" },
                        { "effect", "File is now in secure quarantine location" }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error quarantining file", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        // ==================== Notification ====================

        /// <summary>
        /// Notify data owner about DLP violation
        /// POST /users/{user-id}/sendMail
        /// Permission: Mail.Send
        /// </summary>
        public async Task<XDRRemediationResponse> NotifyDataOwnerAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "NotifyDataOwner");
                await SetAuthHeaderAsync(request.TenantId);

                if (!ValidateRequiredParameters(request, out var failureResponse, "ownerEmail", "fileName", "violationType"))
                {
                    return failureResponse!;
                }

                var ownerEmail = request.Parameters["ownerEmail"]!.ToString()!;
                var fileName = request.Parameters["fileName"]!.ToString()!;
                var violationType = request.Parameters["violationType"]!.ToString()!;
                var incidentId = request.IncidentId ?? "Unknown";

                Logger.LogInformation("NOTIFY OWNER: Sending DLP violation notification to {Owner} for file {FileName}", ownerEmail, fileName);

                var emailMessage = new
                {
                    message = new
                    {
                        subject = $"Data Loss Prevention Violation: {fileName}",
                        body = new
                        {
                            contentType = "HTML",
                            content = $@"
                                <html>
                                <body>
                                    <h2>Data Loss Prevention Violation Detected</h2>
                                    <p><strong>File:</strong> {fileName}</p>
                                    <p><strong>Violation Type:</strong> {violationType}</p>
                                    <p><strong>Incident ID:</strong> {incidentId}</p>
                                    <p><strong>Action Taken:</strong> {request.Justification ?? "Remediation action executed"}</p>
                                    <p><strong>Time:</strong> {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</p>
                                    <hr/>
                                    <p><em>This notification was generated by SentryXDR in response to a security incident.</em></p>
                                </body>
                                </html>
                            "
                        },
                        toRecipients = new[]
                        {
                            new
                            {
                                emailAddress = new
                                {
                                    address = ownerEmail
                                }
                            }
                        }
                    },
                    saveToSentItems = false
                };

                var result = await PostJsonAsync<JsonElement>(
                    $"{GraphBaseUrl}/users/{ownerEmail}/sendMail",
                    emailMessage);

                if (result.Success)
                {
                    LogOperationComplete(request, "NotifyDataOwner", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"DLP violation notification sent to {ownerEmail}", new Dictionary<string, object>
                    {
                        { "ownerEmail", ownerEmail },
                        { "fileName", fileName },
                        { "violationType", violationType },
                        { "incidentId", incidentId },
                        { "action", "NotificationSent" }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error sending notification", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }
    }
}

