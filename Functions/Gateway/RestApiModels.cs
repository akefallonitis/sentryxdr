namespace SentryXDR.Functions.Gateway
{
    /// <summary>
    /// REST API request to submit a remediation action
    /// </summary>
    public class RemediationSubmitRequest
    {
        /// <summary>
        /// Azure AD Tenant ID
        /// </summary>
        public string TenantId { get; set; } = string.Empty;

        /// <summary>
        /// Incident ID from Microsoft Defender
        /// </summary>
        public string? IncidentId { get; set; }

        /// <summary>
        /// Platform: MDE, MDO, EntraID, Intune, MCAS, Azure, MDI
        /// </summary>
        public string Platform { get; set; } = string.Empty;

        /// <summary>
        /// Action to perform (e.g., "IsolateDevice", "BlockIP", "DisableUser")
        /// </summary>
        public string Action { get; set; } = string.Empty;

        /// <summary>
        /// Action-specific parameters
        /// </summary>
        public Dictionary<string, object>? Parameters { get; set; }

        /// <summary>
        /// User or system that initiated the request
        /// </summary>
        public string? InitiatedBy { get; set; }

        /// <summary>
        /// Priority: Low, Medium, High, Critical
        /// </summary>
        public string? Priority { get; set; }

        /// <summary>
        /// Justification for the action
        /// </summary>
        public string? Justification { get; set; }
    }

    /// <summary>
    /// REST API response after submitting a remediation request
    /// </summary>
    public class RemediationSubmitResponse
    {
        /// <summary>
        /// Unique request identifier
        /// </summary>
        public string RequestId { get; set; } = string.Empty;

        /// <summary>
        /// Orchestration instance ID
        /// </summary>
        public string InstanceId { get; set; } = string.Empty;

        /// <summary>
        /// Current status: Accepted, Queued, Running
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// URL to check status
        /// </summary>
        public string StatusUrl { get; set; } = string.Empty;

        /// <summary>
        /// URL to cancel the request
        /// </summary>
        public string CancelUrl { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp when request was submitted
        /// </summary>
        public DateTime SubmittedAt { get; set; }
    }

    /// <summary>
    /// REST API response for status queries
    /// </summary>
    public class RemediationStatusResponse
    {
        /// <summary>
        /// Request identifier
        /// </summary>
        public string RequestId { get; set; } = string.Empty;

        /// <summary>
        /// Orchestration instance ID
        /// </summary>
        public string InstanceId { get; set; } = string.Empty;

        /// <summary>
        /// Current status: Pending, Running, Completed, Failed, Cancelled
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp when request was created
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Last update timestamp
        /// </summary>
        public DateTime? LastUpdated { get; set; }

        /// <summary>
        /// Serialized input request
        /// </summary>
        public string? SerializedInput { get; set; }
    }

    /// <summary>
    /// REST API response for history queries (uses native APIs, no blob storage!)
    /// </summary>
    public class RemediationHistoryResponse
    {
        /// <summary>
        /// Tenant ID filter
        /// </summary>
        public string? TenantId { get; set; }

        /// <summary>
        /// From date filter
        /// </summary>
        public DateTime FromDate { get; set; }

        /// <summary>
        /// To date filter
        /// </summary>
        public DateTime ToDate { get; set; }

        /// <summary>
        /// Total count of history items
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// History items (sourced from native Defender APIs)
        /// </summary>
        public List<RemediationHistoryItem> Items { get; set; } = new();
    }

    /// <summary>
    /// Individual history item (sourced from native APIs)
    /// Data sources:
    /// - MDE: GET /api/machineactions
    /// - Graph: GET /security/incidents/{id}/comments
    /// - Orchestration history
    /// </summary>
    public class RemediationHistoryItem
    {
        /// <summary>
        /// Request identifier
        /// </summary>
        public string RequestId { get; set; } = string.Empty;

        /// <summary>
        /// Platform (MDE, MDO, etc.)
        /// </summary>
        public string Platform { get; set; } = string.Empty;

        /// <summary>
        /// Action performed
        /// </summary>
        public string Action { get; set; } = string.Empty;

        /// <summary>
        /// Final status
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Execution timestamp
        /// </summary>
        public DateTime ExecutedAt { get; set; }

        /// <summary>
        /// Who initiated the action
        /// </summary>
        public string InitiatedBy { get; set; } = string.Empty;

        /// <summary>
        /// Duration of execution
        /// </summary>
        public TimeSpan? Duration { get; set; }

        /// <summary>
        /// Incident ID if applicable
        /// </summary>
        public string? IncidentId { get; set; }

        /// <summary>
        /// Success/failure message
        /// </summary>
        public string? Message { get; set; }
    }
}
