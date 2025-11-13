using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SentryXDR.Models;
using SentryXDR.Services;
using SentryXDR.Services.Storage;

namespace SentryXDR.Functions.Activities
{
    /// <summary>
    /// Support activities for orchestration
    /// </summary>
    public class SupportActivities
    {
        private readonly ILogger<SupportActivities> _logger;
        private readonly IRemediationValidator _validator;
        private readonly IAuditLogService _auditLogService;
        private readonly IHistoryService _historyService;

        public SupportActivities(
            ILogger<SupportActivities> logger,
            IRemediationValidator validator,
            IAuditLogService auditLogService,
            IHistoryService historyService)
        {
            _logger = logger;
            _validator = validator;
            _auditLogService = auditLogService;
            _historyService = historyService;
        }

        [Function("ValidateRemediationActivity")]
        public async Task<bool> ValidateRemediationAsync(
            [ActivityTrigger] XDRRemediationRequest request)
        {
            _logger.LogInformation("Validating remediation request {RequestId}", request.RequestId);

            try
            {
                var result = await _validator.ValidateRequestAsync(request);
                return result.IsValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Validation error for request {RequestId}", request.RequestId);
                return false;
            }
        }

        [Function("LogAuditActivity")]
        public async Task LogAuditAsync(
            [ActivityTrigger] AuditLogEntry entry)
        {
            _logger.LogInformation("Logging audit entry {Id} for tenant {TenantId}", entry.Id, entry.TenantId);

            try
            {
                await _auditLogService.LogRemediationAsync(entry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log audit entry {Id}", entry.Id);
                // Don't throw - audit logging shouldn't fail the orchestration
            }
        }

        [Function("SendNotificationActivity")]
        public async Task SendNotificationAsync(
            [ActivityTrigger] object notificationData)
        {
            _logger.LogInformation("Sending notification: {Data}", notificationData);

            // In production, integrate with:
            // - Microsoft Teams via Webhook
            // - Azure Logic Apps
            // - Email via SendGrid
            // - SMS via Twilio
            // - PagerDuty for critical alerts

            await Task.CompletedTask;
        }

        /// <summary>
        /// Record history entry for action tracking
        /// </summary>
        [Function("RecordHistoryActivity")]
        public async Task RecordHistoryAsync(
            [ActivityTrigger] RemediationHistoryEntry entry)
        {
            _logger.LogInformation("Recording history for request: {RequestId}, Status: {Status}",
                entry.RequestId, entry.Status);

            try
            {
                await _historyService.AddHistoryEntryAsync(entry);
                _logger.LogInformation("History recorded successfully for request: {RequestId}", entry.RequestId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to record history for request: {RequestId}", entry.RequestId);
                // Don't throw - history recording shouldn't fail the orchestration
            }
        }
    }
}
