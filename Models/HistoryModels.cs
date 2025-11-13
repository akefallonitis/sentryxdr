using System;
using System.Collections.Generic;

namespace SentryXDR.Models
{
    /// <summary>
    /// History entry for tracking all remediation actions
    /// </summary>
    public class RemediationHistoryEntry
    {
        public string HistoryId { get; set; } = Guid.NewGuid().ToString();
        public string RequestId { get; set; } = string.Empty;
        public string OrchestrationId { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;
        public string IncidentId { get; set; } = string.Empty;
        public XDRPlatform Platform { get; set; }
        public XDRAction Action { get; set; }
        public string Status { get; set; } = string.Empty;
        public Dictionary<string, object> Parameters { get; set; } = new();
        public string InitiatedBy { get; set; } = string.Empty;
        public RemediationPriority Priority { get; set; }
        public string Justification { get; set; } = string.Empty;
        public DateTime InitiatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
        public DateTime? CancelledAt { get; set; }
        public string? CancelledBy { get; set; }
        public string? CancellationReason { get; set; }
        public TimeSpan? Duration { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public List<string>? Errors { get; set; }
        public Dictionary<string, object>? Details { get; set; }
    }

    /// <summary>
    /// Request to cancel an in-progress remediation action
    /// </summary>
    public class CancelRemediationRequest
    {
        public string OrchestrationId { get; set; } = string.Empty;
        public string RequestId { get; set; } = string.Empty;
        public string CancelledBy { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public bool ForceTerminate { get; set; } = false;
    }

    /// <summary>
    /// Response for cancellation request
    /// </summary>
    public class CancelRemediationResponse
    {
        public string OrchestrationId { get; set; } = string.Empty;
        public string RequestId { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime CancelledAt { get; set; } = DateTime.UtcNow;
        public string CancelledBy { get; set; } = string.Empty;
    }

    /// <summary>
    /// Query parameters for history search
    /// </summary>
    public class HistoryQueryParameters
    {
        public string? TenantId { get; set; }
        public string? IncidentId { get; set; }
        public XDRPlatform? Platform { get; set; }
        public XDRAction? Action { get; set; }
        public string? Status { get; set; }
        public string? InitiatedBy { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int PageSize { get; set; } = 50;
        public int PageNumber { get; set; } = 1;
        public string? SortBy { get; set; } = "InitiatedAt";
        public string? SortOrder { get; set; } = "desc";
    }

    /// <summary>
    /// Paginated history response
    /// </summary>
    public class HistoryResponse
    {
        public int TotalCount { get; set; }
        public int PageSize { get; set; }
        public int PageNumber { get; set; }
        public int TotalPages { get; set; }
        public List<RemediationHistoryEntry> Items { get; set; } = new();
    }

    /// <summary>
    /// Statistics for history dashboard
    /// </summary>
    public class HistoryStatistics
    {
        public int TotalActions { get; set; }
        public int SuccessfulActions { get; set; }
        public int FailedActions { get; set; }
        public int CancelledActions { get; set; }
        public int InProgressActions { get; set; }
        public Dictionary<XDRPlatform, int> ActionsByPlatform { get; set; } = new();
        public Dictionary<XDRAction, int> ActionsByType { get; set; } = new();
        public Dictionary<string, int> ActionsByTenant { get; set; } = new();
        public double AverageCompletionTime { get; set; }
        public double SuccessRate { get; set; }
    }
}
