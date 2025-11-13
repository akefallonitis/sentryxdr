using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using SentryXDR.Models;

namespace SentryXDR.Functions
{
    /// <summary>
    /// XDR Orchestrator - Central routing and coordination
    /// Routes requests to appropriate platform workers
    /// </summary>
    public class XDROrchestrator
    {
        [Function("XDROrchestrator")]
        public async Task<XDRRemediationResponse> RunOrchestratorAsync(
            [OrchestrationTrigger] TaskOrchestrationContext context)
        {
            var request = context.GetInput<XDRRemediationRequest>();
            var logger = context.CreateReplaySafeLogger<XDROrchestrator>();
            
            logger.LogInformation(
                "Orchestrating remediation - Tenant: {TenantId}, Incident: {IncidentId}, Platform: {Platform}, Action: {Action}",
                request.TenantId,
                request.IncidentId,
                request.Platform,
                request.Action);
            
            var startTime = context.CurrentUtcDateTime;
            XDRRemediationResponse response;

            try
            {
                // Pre-flight validation
                var validationPassed = await context.CallActivityAsync<bool>(
                    "ValidateRemediationActivity",
                    request);
                
                if (!validationPassed)
                {
                    return new XDRRemediationResponse
                    {
                        RequestId = request.RequestId,
                        TenantId = request.TenantId,
                        IncidentId = request.IncidentId,
                        Success = false,
                        Status = "ValidationFailed",
                        Message = "Pre-flight validation failed",
                        CompletedAt = context.CurrentUtcDateTime,
                        Duration = context.CurrentUtcDateTime - startTime
                    };
                }

                // Route to appropriate worker based on platform
                response = request.Platform switch
                {
                    XDRPlatform.MDE => await context.CallActivityAsync<XDRRemediationResponse>(
                        "MDEWorkerActivity", request),
                    
                    XDRPlatform.MDO => await context.CallActivityAsync<XDRRemediationResponse>(
                        "MDOWorkerActivity", request),
                    
                    XDRPlatform.MCAS => await context.CallActivityAsync<XDRRemediationResponse>(
                        "MCASWorkerActivity", request),
                    
                    XDRPlatform.MDI => await context.CallActivityAsync<XDRRemediationResponse>(
                        "MDIWorkerActivity", request),
                    
                    XDRPlatform.EntraID => await context.CallActivityAsync<XDRRemediationResponse>(
                        "EntraIDWorkerActivity", request),
                    
                    XDRPlatform.Intune => await context.CallActivityAsync<XDRRemediationResponse>(
                        "IntuneWorkerActivity", request),
                    
                    XDRPlatform.Azure => await context.CallActivityAsync<XDRRemediationResponse>(
                        "AzureWorkerActivity", request),
                    
                    _ => new XDRRemediationResponse
                    {
                        RequestId = request.RequestId,
                        TenantId = request.TenantId,
                        IncidentId = request.IncidentId,
                        Success = false,
                        Status = "PlatformNotSupported",
                        Message = $"Platform {request.Platform} is not supported",
                        CompletedAt = context.CurrentUtcDateTime
                    }
                };

                // Create audit log entry
                var auditEntry = new AuditLogEntry
                {
                    TenantId = request.TenantId,
                    RequestId = request.RequestId,
                    IncidentId = request.IncidentId,
                    Platform = request.Platform,
                    Action = request.Action,
                    InitiatedBy = request.InitiatedBy,
                    TargetResource = request.Parameters.GetValueOrDefault("deviceId", 
                        request.Parameters.GetValueOrDefault("userId", "unknown"))?.ToString() ?? "unknown",
                    Success = response.Success,
                    Result = response.Status,
                    Justification = request.Justification,
                    Details = response.Details,
                    Timestamp = context.CurrentUtcDateTime
                };

                // Log audit trail (fire-and-forget)
                await context.CallActivityAsync("LogAuditActivity", auditEntry);

                // Send notification for high-priority actions
                if (request.Priority >= RemediationPriority.High)
                {
                    await context.CallActivityAsync("SendNotificationActivity", new
                    {
                        TenantId = request.TenantId,
                        IncidentId = request.IncidentId,
                        Priority = request.Priority.ToString(),
                        Action = request.Action.ToString(),
                        Success = response.Success,
                        Recipients = new[] { request.InitiatedBy }
                    });
                }

                // Set duration
                response.Duration = context.CurrentUtcDateTime - startTime;
                
                logger.LogInformation(
                    "Orchestration completed - Status: {Status}, Success: {Success}, Duration: {Duration}ms",
                    response.Status,
                    response.Success,
                    response.Duration.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                logger.LogError("Orchestration failed: {Error}", ex.Message);
                
                response = new XDRRemediationResponse
                {
                    RequestId = request.RequestId,
                    TenantId = request.TenantId,
                    IncidentId = request.IncidentId,
                    Success = false,
                    Status = "OrchestrationFailed",
                    Message = $"Orchestration failed: {ex.Message}",
                    Errors = new List<string> { ex.ToString() },
                    CompletedAt = context.CurrentUtcDateTime,
                    Duration = context.CurrentUtcDateTime - startTime
                };
            }

            return response;
        }

        [Function("DefenderXDROrchestrator")]
        public async Task<XDRRemediationResponse> RunOrchestrator(
            [OrchestrationTrigger] TaskOrchestrationContext context)
        {
            var request = context.GetInput<XDRRemediationRequest>();
            var logger = context.CreateReplaySafeLogger<XDROrchestrator>();
            var startTime = context.CurrentUtcDateTime;
            
            logger.LogInformation("Orchestrator started for {Action} on platform {Platform}", 
                request.Action, request.Platform);

            try
            {
                // ? Create history entry at start
                var historyEntry = new RemediationHistoryEntry
                {
                    RequestId = request.RequestId,
                    OrchestrationId = context.InstanceId,
                    TenantId = request.TenantId,
                    IncidentId = request.IncidentId,
                    Platform = request.Platform,
                    Action = request.Action,
                    Parameters = request.Parameters,
                    InitiatedBy = request.InitiatedBy,
                    Priority = request.Priority,
                    Justification = request.Justification,
                    Status = "InProgress",
                    InitiatedAt = startTime
                };

                // ? Record history start
                await context.CallActivityAsync("RecordHistoryActivity", historyEntry);

                // Validate the request
                var validationResult = await context.CallActivityAsync<bool>("ValidateRemediationRequest", request);
                
                if (!validationResult)
                {
                    historyEntry.Status = "ValidationFailed";
                    historyEntry.Success = false;
                    historyEntry.CompletedAt = context.CurrentUtcDateTime;
                    historyEntry.Duration = context.CurrentUtcDateTime - startTime;
                    historyEntry.ErrorMessage = "Request validation failed";
                    
                    // ? Update history
                    await context.CallActivityAsync("RecordHistoryActivity", historyEntry);
                    
                    return new XDRRemediationResponse
                    {
                        RequestId = request.RequestId,
                        Success = false,
                        Status = "ValidationFailed",
                        Message = "Request validation failed"
                    };
                }

                // Route to appropriate worker
                var workerName = request.Platform switch
                {
                    XDRPlatform.MDE => "MDEWorkerActivity",
                    XDRPlatform.MDO => "MDOWorkerActivity",
                    XDRPlatform.EntraID => "EntraIDWorkerActivity",
                    XDRPlatform.Intune => "IntuneWorkerActivity",
                    XDRPlatform.MCAS => "MCASWorkerActivity",
                    XDRPlatform.Azure => "AzureWorkerActivity",
                    XDRPlatform.MDI => "MDIWorkerActivity",
                    _ => null
                };

                if (workerName == null)
                {
                    historyEntry.Status = "PlatformNotSupported";
                    historyEntry.Success = false;
                    historyEntry.CompletedAt = context.CurrentUtcDateTime;
                    historyEntry.Duration = context.CurrentUtcDateTime - startTime;
                    historyEntry.ErrorMessage = $"Platform {request.Platform} not supported";
                    
                    await context.CallActivityAsync("RecordHistoryActivity", historyEntry);
                    
                    return new XDRRemediationResponse
                    {
                        RequestId = request.RequestId,
                        Success = false,
                        Status = "PlatformNotSupported",
                        Message = $"Platform {request.Platform} not supported"
                    };
                }

                var response = await context.CallActivityAsync<XDRRemediationResponse>(workerName, request);

                // ? Update history with result
                historyEntry.Status = response.Success ? "Completed" : "Failed";
                historyEntry.Success = response.Success;
                historyEntry.CompletedAt = context.CurrentUtcDateTime;
                historyEntry.Duration = context.CurrentUtcDateTime - startTime;
                historyEntry.ErrorMessage = response.Success ? null : response.Message;
                historyEntry.Errors = response.Errors;
                historyEntry.Details = response.Details;
                
                await context.CallActivityAsync("RecordHistoryActivity", historyEntry);

                // Log the audit entry
                await context.CallActivityAsync("LogAuditEntry", new AuditLogEntry
                {
                    TenantId = request.TenantId,
                    RequestId = request.RequestId,
                    IncidentId = request.IncidentId,
                    Action = request.Action,
                    Platform = request.Platform,
                    InitiatedBy = request.InitiatedBy,
                    Justification = request.Justification,
                    Success = response.Success,
                    Result = response.Status,
                    TargetResource = request.Parameters.ContainsKey("deviceId") 
                        ? request.Parameters["deviceId"]?.ToString() ?? "unknown"
                        : request.Parameters.ContainsKey("userId")
                            ? request.Parameters["userId"]?.ToString() ?? "unknown"
                            : "unknown",
                    Details = response.Details ?? new Dictionary<string, object>(),
                    Timestamp = context.CurrentUtcDateTime
                });

                return response;
            }
            catch (Exception ex)
            {
                logger.LogError("Orchestrator failed: {Error}", ex.Message);
                
                // ? Record failure in history
                var historyEntry = new RemediationHistoryEntry
                {
                    RequestId = request.RequestId,
                    OrchestrationId = context.InstanceId,
                    TenantId = request.TenantId,
                    IncidentId = request.IncidentId,
                    Platform = request.Platform,
                    Action = request.Action,
                    Status = "Exception",
                    Success = false,
                    CompletedAt = context.CurrentUtcDateTime,
                    Duration = context.CurrentUtcDateTime - startTime,
                    ErrorMessage = ex.Message,
                    Errors = new List<string> { ex.ToString() }
                };
                
                await context.CallActivityAsync("RecordHistoryActivity", historyEntry);
                
                return new XDRRemediationResponse
                {
                    RequestId = request.RequestId,
                    Success = false,
                    Status = "Exception",
                    Message = $"Orchestration failed: {ex.Message}",
                    Errors = new List<string> { ex.ToString() }
                };
            }
        }
    }
}
