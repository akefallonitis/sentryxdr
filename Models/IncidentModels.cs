using SentryXDR.Models;

namespace SentryXDR.Models
{
    /// <summary>
    /// Incident Management Models for Microsoft Defender XDR
    /// API: https://learn.microsoft.com/en-us/graph/api/resources/security-incident
    /// </summary>

    public class IncidentUpdateRequest
    {
        public string IncidentId { get; set; } = string.Empty;
        public IncidentStatus? Status { get; set; }
        public IncidentSeverity? Severity { get; set; }
        public string? AssignedTo { get; set; }
        public string? Comment { get; set; }
        public List<string>? Tags { get; set; }
        public IncidentClassification? Classification { get; set; }
        public IncidentDetermination? Determination { get; set; }
        public Dictionary<string, object>? CustomProperties { get; set; }
    }

    public enum IncidentStatus
    {
        New,
        Active,
        InProgress,
        AwaitingAction,
        Resolved,
        Closed,
        Reopened
    }

    public enum IncidentSeverity
    {
        Informational,
        Low,
        Medium,
        High,
        Critical
    }

    public enum IncidentClassification
    {
        Unknown,
        TruePositive,
        FalsePositive,
        BenignPositive,
        TrueNegative
    }

    public enum IncidentDetermination
    {
        Unknown,
        Malware,
        Phishing,
        SuspiciousActivity,
        Clean,
        InsufficientData,
        ConfirmedActivity,
        LineOfBusinessApplication,
        CompromisedAccount,
        SecurityTesting,
        UnwantedSoftware,
        Other,
        MultiStagedAttack,
        Ransomware,
        MaliciousUserActivity,
        NotMalicious
    }

    public class MergeIncidentsRequest
    {
        public string PrimaryIncidentId { get; set; } = string.Empty;
        public List<string> SecondaryIncidentIds { get; set; } = new();
        public string MergeReason { get; set; } = string.Empty;
    }

    public class MergeAlertsRequest
    {
        public string TargetIncidentId { get; set; } = string.Empty;
        public List<string> AlertIds { get; set; } = new();
        public string MergeReason { get; set; } = string.Empty;
    }

    public class CreateIncidentFromAlertRequest
    {
        public string AlertId { get; set; } = string.Empty;
        public IncidentSeverity Severity { get; set; } = IncidentSeverity.Medium;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string>? Tags { get; set; }
        public string? AssignedTo { get; set; }
    }

    public class CreateIncidentFromAlertsRequest
    {
        public List<string> AlertIds { get; set; } = new();
        public IncidentSeverity Severity { get; set; } = IncidentSeverity.Medium;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string>? Tags { get; set; }
        public string? AssignedTo { get; set; }
        public bool CorrelateAlerts { get; set; } = true;
    }

    public class CustomDetectionRule
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Query { get; set; } = string.Empty;  // KQL query
        public IncidentSeverity Severity { get; set; }
        public bool Enabled { get; set; } = true;
        public List<string> Actions { get; set; } = new();
        public Dictionary<string, object>? Properties { get; set; }
    }

    public class PlaybookTriggerRequest
    {
        public string IncidentId { get; set; } = string.Empty;
        public string PlaybookName { get; set; } = string.Empty;
        public Dictionary<string, object>? Parameters { get; set; }
        public bool WaitForCompletion { get; set; } = false;
    }

    public class IncidentExportRequest
    {
        public string IncidentId { get; set; } = string.Empty;
        public bool IncludeAlerts { get; set; } = true;
        public bool IncludeEvidence { get; set; } = true;
        public bool IncludeComments { get; set; } = true;
        public string Format { get; set; } = "json";  // json, csv, pdf
    }

    public class LinkIncidentsToCaseRequest
    {
        public string CaseId { get; set; } = string.Empty;
        public List<string> IncidentIds { get; set; } = new();
        public string LinkReason { get; set; } = string.Empty;
    }
}
