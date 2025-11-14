using Microsoft.Extensions.Logging;
using SentryXDR.Models;
using SentryXDR.Services.Authentication;
using System.Text.Json;

namespace SentryXDR.Services.Workers
{
    public interface IMDEAIRService
    {
        // Automated Investigation Actions
        Task<XDRRemediationResponse> TriggerAutomatedInvestigationAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> GetInvestigationStatusAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> ApproveAutomatedInvestigationAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> CancelAutomatedInvestigationAsync(XDRRemediationRequest request);
        
        // Machine Action Management
        Task<XDRRemediationResponse> GetMachineActionStatusAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> CancelMachineActionAsync(XDRRemediationRequest request);
    }

    /// <summary>
    /// MDE Automated Investigation and Response (AIR) Service
    /// Manages automated investigations and their actions
    /// API Reference: https://learn.microsoft.com/en-us/defender-endpoint/api/run-advanced-query-api
    /// </summary>
    public class MDEAIRService : BaseWorkerService, IMDEAIRService
    {
        private readonly IMultiTenantAuthService _authService;
        private const string MDEBaseUrl = "https://api.securitycenter.microsoft.com/api";

        public MDEAIRService(
            ILogger<MDEAIRService> logger,
            IMultiTenantAuthService authService,
            HttpClient httpClient) : base(logger, httpClient)
        {
            _authService = authService;
        }

        private async Task SetAuthHeaderAsync(string tenantId)
        {
            var token = await _authService.GetMDETokenAsync(tenantId);
            SetBearerToken(token);
        }

        // ==================== Automated Investigation ====================

        /// <summary>
        /// Trigger automated investigation on a machine
        /// POST /api/machines/{machineId}/startInvestigation
        /// Permission: Machine.Scan
        /// </summary>
        public async Task<XDRRemediationResponse> TriggerAutomatedInvestigationAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "TriggerAutomatedInvestigation");
                await SetAuthHeaderAsync(request.TenantId);

                var machineId = GetRequiredParameter(request, "machineId", out var failureResponse);
                if (machineId == null) return failureResponse!;

                var comment = GetOptionalParameter(request, "comment", request.Justification ?? "SentryXDR: Automated investigation triggered");

                Logger.LogCritical("TRIGGER AIR: Starting automated investigation on machine {MachineId}", machineId);

                var investigationRequest = new
                {
                    Comment = comment
                };

                var result = await PostJsonAsync<JsonElement>(
                    $"{MDEBaseUrl}/machines/{machineId}/startInvestigation",
                    investigationRequest);

                if (result.Success && result.Data.HasValue)
                {
                    var investigationId = result.Data.Value.GetProperty("id").GetString();
                    var status = result.Data.Value.GetProperty("status").GetString();
                    
                    LogOperationComplete(request, "TriggerAutomatedInvestigation", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"Automated investigation triggered successfully", new Dictionary<string, object>
                    {
                        { "machineId", machineId },
                        { "investigationId", investigationId! },
                        { "status", status! },
                        { "action", "TriggerAIR" },
                        { "note", "Investigation is running asynchronously" }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error triggering investigation", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        /// <summary>
        /// Get status of automated investigation
        /// GET /api/investigations/{id}
        /// Permission: Alert.Read.All
        /// </summary>
        public async Task<XDRRemediationResponse> GetInvestigationStatusAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "GetInvestigationStatus");
                await SetAuthHeaderAsync(request.TenantId);

                var investigationId = GetRequiredParameter(request, "investigationId", out var failureResponse);
                if (investigationId == null) return failureResponse!;

                var result = await GetJsonAsync<JsonElement>($"{MDEBaseUrl}/investigations/{investigationId}");

                if (result.Success && result.Data.HasValue)
                {
                    var investigation = result.Data.Value;
                    var state = investigation.GetProperty("state").GetString();
                    var status = investigation.GetProperty("status").GetString();
                    var machineId = investigation.GetProperty("machineId").GetString();
                    
                    LogOperationComplete(request, "GetInvestigationStatus", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"Investigation status retrieved successfully", new Dictionary<string, object>
                    {
                        { "investigationId", investigationId },
                        { "state", state! },
                        { "status", status! },
                        { "machineId", machineId! },
                        { "details", investigation.ToString() }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error getting investigation status", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        /// <summary>
        /// Approve pending automated investigation actions
        /// POST /api/investigations/{id}/approve
        /// Permission: Machine.Scan
        /// </summary>
        public async Task<XDRRemediationResponse> ApproveAutomatedInvestigationAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "ApproveAutomatedInvestigation");
                await SetAuthHeaderAsync(request.TenantId);

                var investigationId = GetRequiredParameter(request, "investigationId", out var failureResponse);
                if (investigationId == null) return failureResponse!;

                var comment = GetOptionalParameter(request, "comment", request.Justification ?? "SentryXDR: Investigation actions approved");

                Logger.LogCritical("APPROVE AIR: Approving automated investigation {InvestigationId}", investigationId);

                var approveRequest = new
                {
                    Comment = comment
                };

                var result = await PostJsonAsync<JsonElement>(
                    $"{MDEBaseUrl}/investigations/{investigationId}/approve",
                    approveRequest);

                if (result.Success)
                {
                    LogOperationComplete(request, "ApproveAutomatedInvestigation", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"Investigation approved successfully", new Dictionary<string, object>
                    {
                        { "investigationId", investigationId },
                        { "action", "Approved" },
                        { "effect", "Pending remediation actions will now execute" }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error approving investigation", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        /// <summary>
        /// Cancel automated investigation
        /// POST /api/investigations/{id}/cancel
        /// Permission: Machine.Scan
        /// </summary>
        public async Task<XDRRemediationResponse> CancelAutomatedInvestigationAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "CancelAutomatedInvestigation");
                await SetAuthHeaderAsync(request.TenantId);

                var investigationId = GetRequiredParameter(request, "investigationId", out var failureResponse);
                if (investigationId == null) return failureResponse!;

                var comment = GetOptionalParameter(request, "comment", request.Justification ?? "SentryXDR: Investigation cancelled");

                Logger.LogWarning("CANCEL AIR: Cancelling automated investigation {InvestigationId}", investigationId);

                var cancelRequest = new
                {
                    Comment = comment
                };

                var result = await PostJsonAsync<JsonElement>(
                    $"{MDEBaseUrl}/investigations/{investigationId}/cancel",
                    cancelRequest);

                if (result.Success)
                {
                    LogOperationComplete(request, "CancelAutomatedInvestigation", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"Investigation cancelled successfully", new Dictionary<string, object>
                    {
                        { "investigationId", investigationId },
                        { "action", "Cancelled" },
                        { "effect", "Investigation stopped, no remediation actions will execute" }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error cancelling investigation", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        // ==================== Machine Action Management ====================

        /// <summary>
        /// Get status of a machine action
        /// GET /api/machineactions/{id}
        /// Permission: Machine.Read.All
        /// </summary>
        public async Task<XDRRemediationResponse> GetMachineActionStatusAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "GetMachineActionStatus");
                await SetAuthHeaderAsync(request.TenantId);

                var actionId = GetRequiredParameter(request, "actionId", out var failureResponse);
                if (actionId == null) return failureResponse!;

                var result = await GetJsonAsync<JsonElement>($"{MDEBaseUrl}/machineactions/{actionId}");

                if (result.Success && result.Data.HasValue)
                {
                    var action = result.Data.Value;
                    var status = action.GetProperty("status").GetString();
                    var type = action.GetProperty("type").GetString();
                    var machineId = action.GetProperty("machineId").GetString();
                    
                    LogOperationComplete(request, "GetMachineActionStatus", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"Machine action status retrieved successfully", new Dictionary<string, object>
                    {
                        { "actionId", actionId },
                        { "status", status! },
                        { "type", type! },
                        { "machineId", machineId! },
                        { "details", action.ToString() }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error getting action status", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        /// <summary>
        /// Cancel a pending machine action
        /// POST /api/machineactions/{id}/cancel
        /// Permission: Machine.Scan
        /// </summary>
        public async Task<XDRRemediationResponse> CancelMachineActionAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "CancelMachineAction");
                await SetAuthHeaderAsync(request.TenantId);

                var actionId = GetRequiredParameter(request, "actionId", out var failureResponse);
                if (actionId == null) return failureResponse!;

                var comment = GetOptionalParameter(request, "comment", request.Justification ?? "SentryXDR: Machine action cancelled");

                Logger.LogWarning("CANCEL ACTION: Cancelling machine action {ActionId}", actionId);

                var cancelRequest = new
                {
                    Comment = comment
                };

                var result = await PostJsonAsync<JsonElement>(
                    $"{MDEBaseUrl}/machineactions/{actionId}/cancel",
                    cancelRequest);

                if (result.Success)
                {
                    LogOperationComplete(request, "CancelMachineAction", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"Machine action cancelled successfully", new Dictionary<string, object>
                    {
                        { "actionId", actionId },
                        { "action", "Cancelled" },
                        { "effect", "Pending action will not execute" }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error cancelling action", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }
    }
}
