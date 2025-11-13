namespace SentryXDR.Models
{
    /// <summary>
    /// Batch remediation request for multiple entities
    /// Supports multi-tenant scenarios with multiple targets
    /// </summary>
    public class BatchRemediationRequest
    {
        public string BatchId { get; set; } = Guid.NewGuid().ToString();
        public string TenantId { get; set; } = string.Empty;
        public string IncidentId { get; set; } = string.Empty;
        public XDRPlatform Platform { get; set; }
        public XDRAction Action { get; set; }
        public List<Dictionary<string, object>> Targets { get; set; } = new();
        public string InitiatedBy { get; set; } = string.Empty;
        public RemediationPriority Priority { get; set; } = RemediationPriority.Medium;
        public string Justification { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public bool ParallelExecution { get; set; } = true;
    }

    /// <summary>
    /// Batch remediation response
    /// </summary>
    public class BatchRemediationResponse
    {
        public string BatchId { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;
        public string IncidentId { get; set; } = string.Empty;
        public int TotalTargets { get; set; }
        public int SuccessfulTargets { get; set; }
        public int FailedTargets { get; set; }
        public List<XDRRemediationResponse> Results { get; set; } = new();
        public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
        public TimeSpan TotalDuration { get; set; }
    }

    /// <summary>
    /// Multi-tenant batch request
    /// For scenarios where different tenants need the same action
    /// </summary>
    public class MultiTenantBatchRequest
    {
        public string BatchId { get; set; } = Guid.NewGuid().ToString();
        public List<XDRRemediationRequest> Requests { get; set; } = new();
        public string InitiatedBy { get; set; } = string.Empty;
        public bool ParallelExecution { get; set; } = true;
    }
}
