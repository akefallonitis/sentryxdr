using Microsoft.Extensions.Logging;
using SentryXDR.Models;
using SentryXDR.Services.Authentication;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SentryXDR.Services.Workers
{
    public interface IIncidentManagementService
    {
        // Status & Severity Management
        Task<XDRRemediationResponse> UpdateIncidentStatusAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> UpdateIncidentSeverityAsync(XDRRemediationRequest request);
        
        // Classification & Determination
        Task<XDRRemediationResponse> UpdateIncidentClassificationAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> UpdateIncidentDeterminationAsync(XDRRemediationRequest request);
        
        // Assignment & Documentation
        Task<XDRRemediationResponse> AssignIncidentToUserAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> AddIncidentCommentAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> AddIncidentTagAsync(XDRRemediationRequest request);
        
        // Lifecycle Management
        Task<XDRRemediationResponse> ResolveIncidentAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> ReopenIncidentAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> EscalateIncidentAsync(XDRRemediationRequest request);
        
        // Incident Correlation & Merging
        Task<XDRRemediationResponse> LinkIncidentsToCaseAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> MergeIncidentsAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> MergeAlertsIntoIncidentAsync(XDRRemediationRequest request);
        
        // Incident Creation from Alerts
        Task<XDRRemediationResponse> CreateIncidentFromAlertAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> CreateIncidentFromAlertsAsync(XDRRemediationRequest request);
        
        // Automation & Response
        Task<XDRRemediationResponse> TriggerAutomatedPlaybookAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> CreateCustomDetectionFromIncidentAsync(XDRRemediationRequest request);
        
        // Reporting
        Task<XDRRemediationResponse> ExportIncidentForReportingAsync(XDRRemediationRequest request);
    }

    /// <summary>
    /// Incident Management Service for Microsoft Defender XDR
    /// Manages complete incident lifecycle including merging, alert correlation, and automation
    /// API Reference: https://learn.microsoft.com/en-us/graph/api/resources/security-incident
    /// </summary>
    public class IncidentManagementService : IIncidentManagementService
    {
        private readonly ILogger<IncidentManagementService> _logger;
        private readonly IMultiTenantAuthService _authService;
        private readonly HttpClient _httpClient;
        private const string GraphBaseUrl = "https://graph.microsoft.com/v1.0";
        private const string GraphBetaUrl = "https://graph.microsoft.com/beta";

        public IncidentManagementService(
            ILogger<IncidentManagementService> logger,
            IMultiTenantAuthService authService,
            HttpClient httpClient)
        {
            _logger = logger;
            _authService = authService;
            _httpClient = httpClient;
        }

        private async Task SetAuthHeaderAsync(string tenantId)
        {
            var token = await _authService.GetGraphTokenAsync(tenantId);
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        // ==================== Status & Severity Management ====================

        /// <summary>
        /// Update incident status (New, Active, InProgress, Resolved, Closed)
        /// PATCH /security/incidents/{id}
        /// </summary>
        public async Task<XDRRemediationResponse> UpdateIncidentStatusAsync(XDRRemediationRequest request)
        {
            try
            {
                await SetAuthHeaderAsync(request.TenantId);
                var incidentId = request.Parameters["incidentId"]?.ToString();
                var status = request.Parameters["status"]?.ToString();

                _logger.LogInformation("Updating incident {IncidentId} status to {Status}", incidentId, status);

                var updateBody = new
                {
                    status = status
                };

                var url = $"{GraphBetaUrl}/security/incidents/{incidentId}";
                var content = new StringContent(JsonSerializer.Serialize(updateBody), Encoding.UTF8, "application/json");
                var result = await _httpClient.PatchAsync(url, content);

                if (result.IsSuccessStatusCode)
                {
                    return CreateSuccessResponse(request, $"Incident {incidentId} status updated to {status}");
                }
                else
                {
                    var error = await result.Content.ReadAsStringAsync();
                    return CreateFailureResponse(request, $"Failed to update status: {error}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update incident status");
                return CreateExceptionResponse(request, ex);
            }
        }

        /// <summary>
        /// Update incident severity (Informational, Low, Medium, High, Critical)
        /// PATCH /security/incidents/{id}
        /// </summary>
        public async Task<XDRRemediationResponse> UpdateIncidentSeverityAsync(XDRRemediationRequest request)
        {
            try
            {
                await SetAuthHeaderAsync(request.TenantId);
                var incidentId = request.Parameters["incidentId"]?.ToString();
                var severity = request.Parameters["severity"]?.ToString();

                _logger.LogInformation("Updating incident {IncidentId} severity to {Severity}", incidentId, severity);

                var updateBody = new
                {
                    severity = severity
                };

                var url = $"{GraphBetaUrl}/security/incidents/{incidentId}";
                var content = new StringContent(JsonSerializer.Serialize(updateBody), Encoding.UTF8, "application/json");
                var result = await _httpClient.PatchAsync(url, content);

                if (result.IsSuccessStatusCode)
                {
                    return CreateSuccessResponse(request, $"Incident {incidentId} severity updated to {severity}");
                }
                else
                {
                    var error = await result.Content.ReadAsStringAsync();
                    return CreateFailureResponse(request, $"Failed to update severity: {error}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update incident severity");
                return CreateExceptionResponse(request, ex);
            }
        }

        // ==================== Classification & Determination ====================

        /// <summary>
        /// Update incident classification (TruePositive, FalsePositive, BenignPositive)
        /// PATCH /security/incidents/{id}
        /// </summary>
        public async Task<XDRRemediationResponse> UpdateIncidentClassificationAsync(XDRRemediationRequest request)
        {
            try
            {
                await SetAuthHeaderAsync(request.TenantId);
                var incidentId = request.Parameters["incidentId"]?.ToString();
                var classification = request.Parameters["classification"]?.ToString();

                _logger.LogInformation("Updating incident {IncidentId} classification to {Classification}", incidentId, classification);

                var updateBody = new
                {
                    classification = classification
                };

                var url = $"{GraphBetaUrl}/security/incidents/{incidentId}";
                var content = new StringContent(JsonSerializer.Serialize(updateBody), Encoding.UTF8, "application/json");
                var result = await _httpClient.PatchAsync(url, content);

                if (result.IsSuccessStatusCode)
                {
                    return CreateSuccessResponse(request, $"Incident {incidentId} classification updated to {classification}");
                }
                else
                {
                    var error = await result.Content.ReadAsStringAsync();
                    return CreateFailureResponse(request, $"Failed to update classification: {error}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update incident classification");
                return CreateExceptionResponse(request, ex);
            }
        }

        /// <summary>
        /// Update incident determination (Malware, Phishing, Suspicious, Clean, etc.)
        /// PATCH /security/incidents/{id}
        /// </summary>
        public async Task<XDRRemediationResponse> UpdateIncidentDeterminationAsync(XDRRemediationRequest request)
        {
            try
            {
                await SetAuthHeaderAsync(request.TenantId);
                var incidentId = request.Parameters["incidentId"]?.ToString();
                var determination = request.Parameters["determination"]?.ToString();

                _logger.LogInformation("Updating incident {IncidentId} determination to {Determination}", incidentId, determination);

                var updateBody = new
                {
                    determination = determination
                };

                var url = $"{GraphBetaUrl}/security/incidents/{incidentId}";
                var content = new StringContent(JsonSerializer.Serialize(updateBody), Encoding.UTF8, "application/json");
                var result = await _httpClient.PatchAsync(url, content);

                if (result.IsSuccessStatusCode)
                {
                    return CreateSuccessResponse(request, $"Incident {incidentId} determination updated to {determination}");
                }
                else
                {
                    var error = await result.Content.ReadAsStringAsync();
                    return CreateFailureResponse(request, $"Failed to update determination: {error}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update incident determination");
                return CreateExceptionResponse(request, ex);
            }
        }

        // ==================== Assignment & Documentation ====================

        /// <summary>
        /// Assign incident to a user
        /// PATCH /security/incidents/{id}
        /// </summary>
        public async Task<XDRRemediationResponse> AssignIncidentToUserAsync(XDRRemediationRequest request)
        {
            try
            {
                await SetAuthHeaderAsync(request.TenantId);
                var incidentId = request.Parameters["incidentId"]?.ToString();
                var assignedTo = request.Parameters["assignedTo"]?.ToString();

                _logger.LogInformation("Assigning incident {IncidentId} to {User}", incidentId, assignedTo);

                var updateBody = new
                {
                    assignedTo = assignedTo
                };

                var url = $"{GraphBetaUrl}/security/incidents/{incidentId}";
                var content = new StringContent(JsonSerializer.Serialize(updateBody), Encoding.UTF8, "application/json");
                var result = await _httpClient.PatchAsync(url, content);

                if (result.IsSuccessStatusCode)
                {
                    return CreateSuccessResponse(request, $"Incident {incidentId} assigned to {assignedTo}");
                }
                else
                {
                    var error = await result.Content.ReadAsStringAsync();
                    return CreateFailureResponse(request, $"Failed to assign incident: {error}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to assign incident");
                return CreateExceptionResponse(request, ex);
            }
        }

        /// <summary>
        /// Add comment to incident
        /// POST /security/incidents/{id}/comments
        /// </summary>
        public async Task<XDRRemediationResponse> AddIncidentCommentAsync(XDRRemediationRequest request)
        {
            try
            {
                await SetAuthHeaderAsync(request.TenantId);
                var incidentId = request.Parameters["incidentId"]?.ToString();
                var comment = request.Parameters["comment"]?.ToString();

                _logger.LogInformation("Adding comment to incident {IncidentId}", incidentId);

                var commentBody = new
                {
                    comment = comment
                };

                var url = $"{GraphBetaUrl}/security/incidents/{incidentId}/comments";
                var content = new StringContent(JsonSerializer.Serialize(commentBody), Encoding.UTF8, "application/json");
                var result = await _httpClient.PostAsync(url, content);

                if (result.IsSuccessStatusCode)
                {
                    return CreateSuccessResponse(request, $"Comment added to incident {incidentId}");
                }
                else
                {
                    var error = await result.Content.ReadAsStringAsync();
                    return CreateFailureResponse(request, $"Failed to add comment: {error}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add incident comment");
                return CreateExceptionResponse(request, ex);
            }
        }

        /// <summary>
        /// Add tags to incident
        /// PATCH /security/incidents/{id}
        /// </summary>
        public async Task<XDRRemediationResponse> AddIncidentTagAsync(XDRRemediationRequest request)
        {
            try
            {
                await SetAuthHeaderAsync(request.TenantId);
                var incidentId = request.Parameters["incidentId"]?.ToString();
                var tags = request.Parameters["tags"] as List<string> ?? new List<string>();

                _logger.LogInformation("Adding tags to incident {IncidentId}", incidentId);

                var updateBody = new
                {
                    tags = tags
                };

                var url = $"{GraphBetaUrl}/security/incidents/{incidentId}";
                var content = new StringContent(JsonSerializer.Serialize(updateBody), Encoding.UTF8, "application/json");
                var result = await _httpClient.PatchAsync(url, content);

                if (result.IsSuccessStatusCode)
                {
                    return CreateSuccessResponse(request, $"Tags added to incident {incidentId}");
                }
                else
                {
                    var error = await result.Content.ReadAsStringAsync();
                    return CreateFailureResponse(request, $"Failed to add tags: {error}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add incident tags");
                return CreateExceptionResponse(request, ex);
            }
        }

        // ==================== Lifecycle Management ====================

        /// <summary>
        /// Resolve incident
        /// PATCH /security/incidents/{id}
        /// </summary>
        public async Task<XDRRemediationResponse> ResolveIncidentAsync(XDRRemediationRequest request)
        {
            try
            {
                await SetAuthHeaderAsync(request.TenantId);
                var incidentId = request.Parameters["incidentId"]?.ToString();
                var resolution = request.Parameters["resolution"]?.ToString() ?? "Resolved";

                _logger.LogInformation("Resolving incident {IncidentId}", incidentId);

                var updateBody = new
                {
                    status = "Resolved",
                    classification = request.Parameters["classification"]?.ToString() ?? "TruePositive",
                    determination = request.Parameters["determination"]?.ToString(),
                    comments = new[] { new { comment = resolution } }
                };

                var url = $"{GraphBetaUrl}/security/incidents/{incidentId}";
                var content = new StringContent(JsonSerializer.Serialize(updateBody), Encoding.UTF8, "application/json");
                var result = await _httpClient.PatchAsync(url, content);

                if (result.IsSuccessStatusCode)
                {
                    return CreateSuccessResponse(request, $"Incident {incidentId} resolved");
                }
                else
                {
                    var error = await result.Content.ReadAsStringAsync();
                    return CreateFailureResponse(request, $"Failed to resolve incident: {error}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resolve incident");
                return CreateExceptionResponse(request, ex);
            }
        }

        /// <summary>
        /// Reopen incident
        /// PATCH /security/incidents/{id}
        /// </summary>
        public async Task<XDRRemediationResponse> ReopenIncidentAsync(XDRRemediationRequest request)
        {
            try
            {
                await SetAuthHeaderAsync(request.TenantId);
                var incidentId = request.Parameters["incidentId"]?.ToString();
                var reason = request.Parameters["reason"]?.ToString() ?? "Reopened for further investigation";

                _logger.LogInformation("Reopening incident {IncidentId}", incidentId);

                var updateBody = new
                {
                    status = "Active",
                    comments = new[] { new { comment = reason } }
                };

                var url = $"{GraphBetaUrl}/security/incidents/{incidentId}";
                var content = new StringContent(JsonSerializer.Serialize(updateBody), Encoding.UTF8, "application/json");
                var result = await _httpClient.PatchAsync(url, content);

                if (result.IsSuccessStatusCode)
                {
                    return CreateSuccessResponse(request, $"Incident {incidentId} reopened");
                }
                else
                {
                    var error = await result.Content.ReadAsStringAsync();
                    return CreateFailureResponse(request, $"Failed to reopen incident: {error}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reopen incident");
                return CreateExceptionResponse(request, ex);
            }
        }

        /// <summary>
        /// Escalate incident (increase severity and assign)
        /// PATCH /security/incidents/{id}
        /// </summary>
        public async Task<XDRRemediationResponse> EscalateIncidentAsync(XDRRemediationRequest request)
        {
            try
            {
                await SetAuthHeaderAsync(request.TenantId);
                var incidentId = request.Parameters["incidentId"]?.ToString();
                var escalateTo = request.Parameters["escalateTo"]?.ToString();
                var newSeverity = request.Parameters["newSeverity"]?.ToString() ?? "High";

                _logger.LogInformation("Escalating incident {IncidentId} to {User}", incidentId, escalateTo);

                var updateBody = new
                {
                    severity = newSeverity,
                    assignedTo = escalateTo,
                    comments = new[] { new { comment = $"Incident escalated to {escalateTo}" } }
                };

                var url = $"{GraphBetaUrl}/security/incidents/{incidentId}";
                var content = new StringContent(JsonSerializer.Serialize(updateBody), Encoding.UTF8, "application/json");
                var result = await _httpClient.PatchAsync(url, content);

                if (result.IsSuccessStatusCode)
                {
                    return CreateSuccessResponse(request, $"Incident {incidentId} escalated to {escalateTo}");
                }
                else
                {
                    var error = await result.Content.ReadAsStringAsync();
                    return CreateFailureResponse(request, $"Failed to escalate incident: {error}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to escalate incident");
                return CreateExceptionResponse(request, ex);
            }
        }

        // ==================== Incident Correlation & Merging ====================

        /// <summary>
        /// Link incidents to a case
        /// POST /security/cases/{caseId}/incidents
        /// </summary>
        public async Task<XDRRemediationResponse> LinkIncidentsToCaseAsync(XDRRemediationRequest request)
        {
            try
            {
                await SetAuthHeaderAsync(request.TenantId);
                var caseId = request.Parameters["caseId"]?.ToString();
                var incidentIds = request.Parameters["incidentIds"] as List<string> ?? new List<string>();

                _logger.LogInformation("Linking {Count} incidents to case {CaseId}", incidentIds.Count, caseId);

                var linkBody = new
                {
                    incidentIds = incidentIds
                };

                var url = $"{GraphBetaUrl}/security/cases/{caseId}/incidents";
                var content = new StringContent(JsonSerializer.Serialize(linkBody), Encoding.UTF8, "application/json");
                var result = await _httpClient.PostAsync(url, content);

                if (result.IsSuccessStatusCode)
                {
                    return CreateSuccessResponse(request, $"{incidentIds.Count} incidents linked to case {caseId}");
                }
                else
                {
                    var error = await result.Content.ReadAsStringAsync();
                    return CreateFailureResponse(request, $"Failed to link incidents: {error}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to link incidents to case");
                return CreateExceptionResponse(request, ex);
            }
        }

        /// <summary>
        /// Merge multiple incidents into one
        /// POST /security/incidents/{primaryId}/merge
        /// </summary>
        public async Task<XDRRemediationResponse> MergeIncidentsAsync(XDRRemediationRequest request)
        {
            try
            {
                await SetAuthHeaderAsync(request.TenantId);
                var primaryIncidentId = request.Parameters["primaryIncidentId"]?.ToString();
                var secondaryIncidentIds = request.Parameters["secondaryIncidentIds"] as List<string> ?? new List<string>();

                _logger.LogInformation("Merging {Count} incidents into {PrimaryId}", secondaryIncidentIds.Count, primaryIncidentId);

                var mergeBody = new
                {
                    incidentIds = secondaryIncidentIds
                };

                var url = $"{GraphBetaUrl}/security/incidents/{primaryIncidentId}/merge";
                var content = new StringContent(JsonSerializer.Serialize(mergeBody), Encoding.UTF8, "application/json");
                var result = await _httpClient.PostAsync(url, content);

                if (result.IsSuccessStatusCode)
                {
                    return CreateSuccessResponse(request, $"{secondaryIncidentIds.Count} incidents merged into {primaryIncidentId}");
                }
                else
                {
                    var error = await result.Content.ReadAsStringAsync();
                    return CreateFailureResponse(request, $"Failed to merge incidents: {error}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to merge incidents");
                return CreateExceptionResponse(request, ex);
            }
        }

        /// <summary>
        /// Merge alerts into an existing incident
        /// POST /security/incidents/{incidentId}/alerts
        /// ?? YOUR ENHANCEMENT - Merge alerts into incident
        /// </summary>
        public async Task<XDRRemediationResponse> MergeAlertsIntoIncidentAsync(XDRRemediationRequest request)
        {
            try
            {
                await SetAuthHeaderAsync(request.TenantId);
                var incidentId = request.Parameters["incidentId"]?.ToString();
                var alertIds = request.Parameters["alertIds"] as List<string> ?? new List<string>();

                _logger.LogInformation("Merging {Count} alerts into incident {IncidentId}", alertIds.Count, incidentId);

                var mergeBody = new
                {
                    alertIds = alertIds
                };

                var url = $"{GraphBetaUrl}/security/incidents/{incidentId}/alerts";
                var content = new StringContent(JsonSerializer.Serialize(mergeBody), Encoding.UTF8, "application/json");
                var result = await _httpClient.PostAsync(url, content);

                if (result.IsSuccessStatusCode)
                {
                    return CreateSuccessResponse(request, $"{alertIds.Count} alerts merged into incident {incidentId}");
                }
                else
                {
                    var error = await result.Content.ReadAsStringAsync();
                    return CreateFailureResponse(request, $"Failed to merge alerts: {error}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to merge alerts into incident");
                return CreateExceptionResponse(request, ex);
            }
        }

        // ==================== Incident Creation from Alerts ====================

        /// <summary>
        /// Create a new incident from a single alert
        /// POST /security/incidents
        /// ?? YOUR ENHANCEMENT - Create incident from alert
        /// </summary>
        public async Task<XDRRemediationResponse> CreateIncidentFromAlertAsync(XDRRemediationRequest request)
        {
            try
            {
                await SetAuthHeaderAsync(request.TenantId);
                var alertId = request.Parameters["alertId"]?.ToString();
                var title = request.Parameters["title"]?.ToString() ?? "Incident from Alert";
                var severity = request.Parameters["severity"]?.ToString() ?? "Medium";
                var assignedTo = request.Parameters["assignedTo"]?.ToString();

                _logger.LogInformation("Creating incident from alert {AlertId}", alertId);

                var incidentBody = new
                {
                    displayName = title,
                    severity = severity,
                    assignedTo = assignedTo,
                    status = "Active",
                    alertIds = new[] { alertId }
                };

                var url = $"{GraphBetaUrl}/security/incidents";
                var content = new StringContent(JsonSerializer.Serialize(incidentBody), Encoding.UTF8, "application/json");
                var result = await _httpClient.PostAsync(url, content);

                if (result.IsSuccessStatusCode)
                {
                    var responseContent = await result.Content.ReadAsStringAsync();
                    var incident = JsonSerializer.Deserialize<JsonElement>(responseContent);
                    var incidentId = incident.GetProperty("id").GetString();
                    
                    return CreateSuccessResponse(request, $"Incident {incidentId} created from alert {alertId}", new Dictionary<string, object>
                    {
                        { "incidentId", incidentId! },
                        { "alertId", alertId! }
                    });
                }
                else
                {
                    var error = await result.Content.ReadAsStringAsync();
                    return CreateFailureResponse(request, $"Failed to create incident: {error}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create incident from alert");
                return CreateExceptionResponse(request, ex);
            }
        }

        /// <summary>
        /// Create a new incident from multiple correlated alerts
        /// POST /security/incidents
        /// ?? YOUR ENHANCEMENT - Create incident from multiple alerts (bulk)
        /// </summary>
        public async Task<XDRRemediationResponse> CreateIncidentFromAlertsAsync(XDRRemediationRequest request)
        {
            try
            {
                await SetAuthHeaderAsync(request.TenantId);
                var alertIds = request.Parameters["alertIds"] as List<string> ?? new List<string>();
                var title = request.Parameters["title"]?.ToString() ?? $"Incident from {alertIds.Count} Alerts";
                var severity = request.Parameters["severity"]?.ToString() ?? "Medium";
                var assignedTo = request.Parameters["assignedTo"]?.ToString();
                var description = request.Parameters["description"]?.ToString();

                _logger.LogInformation("Creating incident from {Count} alerts", alertIds.Count);

                var incidentBody = new
                {
                    displayName = title,
                    description = description,
                    severity = severity,
                    assignedTo = assignedTo,
                    status = "Active",
                    alertIds = alertIds
                };

                var url = $"{GraphBetaUrl}/security/incidents";
                var content = new StringContent(JsonSerializer.Serialize(incidentBody), Encoding.UTF8, "application/json");
                var result = await _httpClient.PostAsync(url, content);

                if (result.IsSuccessStatusCode)
                {
                    var responseContent = await result.Content.ReadAsStringAsync();
                    var incident = JsonSerializer.Deserialize<JsonElement>(responseContent);
                    var incidentId = incident.GetProperty("id").GetString();
                    
                    return CreateSuccessResponse(request, $"Incident {incidentId} created from {alertIds.Count} alerts", new Dictionary<string, object>
                    {
                        { "incidentId", incidentId! },
                        { "alertCount", alertIds.Count },
                        { "alertIds", alertIds }
                    });
                }
                else
                {
                    var error = await result.Content.ReadAsStringAsync();
                    return CreateFailureResponse(request, $"Failed to create incident: {error}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create incident from alerts");
                return CreateExceptionResponse(request, ex);
            }
        }

        // ==================== Automation & Response ====================

        /// <summary>
        /// Trigger automated playbook for incident
        /// POST /security/incidents/{id}/runPlaybook
        /// </summary>
        public async Task<XDRRemediationResponse> TriggerAutomatedPlaybookAsync(XDRRemediationRequest request)
        {
            try
            {
                await SetAuthHeaderAsync(request.TenantId);
                var incidentId = request.Parameters["incidentId"]?.ToString();
                var playbookName = request.Parameters["playbookName"]?.ToString();
                var parameters = request.Parameters.GetValueOrDefault("parameters") as Dictionary<string, object>;

                _logger.LogInformation("Triggering playbook {Playbook} for incident {IncidentId}", playbookName, incidentId);

                var playbookBody = new
                {
                    playbookName = playbookName,
                    parameters = parameters ?? new Dictionary<string, object>()
                };

                var url = $"{GraphBetaUrl}/security/incidents/{incidentId}/runPlaybook";
                var content = new StringContent(JsonSerializer.Serialize(playbookBody), Encoding.UTF8, "application/json");
                var result = await _httpClient.PostAsync(url, content);

                if (result.IsSuccessStatusCode)
                {
                    return CreateSuccessResponse(request, $"Playbook {playbookName} triggered for incident {incidentId}");
                }
                else
                {
                    var error = await result.Content.ReadAsStringAsync();
                    return CreateFailureResponse(request, $"Failed to trigger playbook: {error}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to trigger automated playbook");
                return CreateExceptionResponse(request, ex);
            }
        }

        /// <summary>
        /// Create custom detection rule from incident patterns
        /// POST /security/rules/detectionRules
        /// ? YOU SELECTED THIS ACTION
        /// </summary>
        public async Task<XDRRemediationResponse> CreateCustomDetectionFromIncidentAsync(XDRRemediationRequest request)
        {
            try
            {
                await SetAuthHeaderAsync(request.TenantId);
                var incidentId = request.Parameters["incidentId"]?.ToString();
                var ruleName = request.Parameters["ruleName"]?.ToString() ?? $"Detection Rule from Incident {incidentId}";
                var kqlQuery = request.Parameters["query"]?.ToString();
                var severity = request.Parameters["severity"]?.ToString() ?? "Medium";

                _logger.LogInformation("Creating custom detection rule from incident {IncidentId}", incidentId);

                // First, analyze the incident to extract IOCs and patterns
                var incidentUrl = $"{GraphBetaUrl}/security/incidents/{incidentId}";
                var incidentResponse = await _httpClient.GetAsync(incidentUrl);
                var incidentData = await incidentResponse.Content.ReadAsStringAsync();

                // Create detection rule
                var ruleBody = new
                {
                    displayName = ruleName,
                    description = $"Auto-generated detection rule from incident {incidentId}",
                    severity = severity,
                    enabled = true,
                    queryCondition = new
                    {
                        queryText = kqlQuery
                    },
                    actions = new[]
                    {
                        new { actionType = "CreateAlert" },
                        new { actionType = "CreateIncident" }
                    }
                };

                var url = $"{GraphBetaUrl}/security/rules/detectionRules";
                var content = new StringContent(JsonSerializer.Serialize(ruleBody), Encoding.UTF8, "application/json");
                var result = await _httpClient.PostAsync(url, content);

                if (result.IsSuccessStatusCode)
                {
                    var responseContent = await result.Content.ReadAsStringAsync();
                    var rule = JsonSerializer.Deserialize<JsonElement>(responseContent);
                    var ruleId = rule.GetProperty("id").GetString();
                    
                    return CreateSuccessResponse(request, $"Custom detection rule {ruleId} created from incident {incidentId}", new Dictionary<string, object>
                    {
                        { "ruleId", ruleId! },
                        { "ruleName", ruleName },
                        { "incidentId", incidentId! }
                    });
                }
                else
                {
                    var error = await result.Content.ReadAsStringAsync();
                    return CreateFailureResponse(request, $"Failed to create detection rule: {error}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create custom detection from incident");
                return CreateExceptionResponse(request, ex);
            }
        }

        // ==================== Reporting ====================

        /// <summary>
        /// Export incident data for reporting
        /// GET /security/incidents/{id} (with full details)
        /// </summary>
        public async Task<XDRRemediationResponse> ExportIncidentForReportingAsync(XDRRemediationRequest request)
        {
            try
            {
                await SetAuthHeaderAsync(request.TenantId);
                var incidentId = request.Parameters["incidentId"]?.ToString();
                var includeAlerts = request.Parameters.GetValueOrDefault("includeAlerts", true);
                var includeEvidence = request.Parameters.GetValueOrDefault("includeEvidence", true);

                _logger.LogInformation("Exporting incident {IncidentId} for reporting", incidentId);

                var url = $"{GraphBetaUrl}/security/incidents/{incidentId}?$expand=alerts,evidence,comments";
                var result = await _httpClient.GetAsync(url);

                if (result.IsSuccessStatusCode)
                {
                    var incidentData = await result.Content.ReadAsStringAsync();
                    
                    return CreateSuccessResponse(request, $"Incident {incidentId} exported", new Dictionary<string, object>
                    {
                        { "incidentId", incidentId! },
                        { "exportData", incidentData },
                        { "exportedAt", DateTime.UtcNow }
                    });
                }
                else
                {
                    var error = await result.Content.ReadAsStringAsync();
                    return CreateFailureResponse(request, $"Failed to export incident: {error}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export incident");
                return CreateExceptionResponse(request, ex);
            }
        }

        // ==================== Helper Methods ====================

        private XDRRemediationResponse CreateSuccessResponse(XDRRemediationRequest request, string message, Dictionary<string, object>? details = null)
        {
            return new XDRRemediationResponse
            {
                RequestId = request.RequestId,
                TenantId = request.TenantId,
                IncidentId = request.IncidentId,
                Success = true,
                Status = "Completed",
                Message = message,
                Details = details ?? new Dictionary<string, object>(),
                CompletedAt = DateTime.UtcNow,
                Duration = DateTime.UtcNow - request.Timestamp
            };
        }

        private XDRRemediationResponse CreateFailureResponse(XDRRemediationRequest request, string message)
        {
            return new XDRRemediationResponse
            {
                RequestId = request.RequestId,
                TenantId = request.TenantId,
                IncidentId = request.IncidentId,
                Success = false,
                Status = "Failed",
                Message = message,
                CompletedAt = DateTime.UtcNow,
                Duration = DateTime.UtcNow - request.Timestamp
            };
        }

        private XDRRemediationResponse CreateExceptionResponse(XDRRemediationRequest request, Exception ex)
        {
            return new XDRRemediationResponse
            {
                RequestId = request.RequestId,
                TenantId = request.TenantId,
                IncidentId = request.IncidentId,
                Success = false,
                Status = "Exception",
                Message = ex.Message,
                Errors = new List<string> { ex.ToString() },
                CompletedAt = DateTime.UtcNow,
                Duration = DateTime.UtcNow - request.Timestamp
            };
        }
    }
}
