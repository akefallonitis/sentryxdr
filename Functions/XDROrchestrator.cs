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
    }
}
