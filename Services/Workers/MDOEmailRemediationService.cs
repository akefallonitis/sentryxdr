using Microsoft.Extensions.Logging;
using SentryXDR.Models;
using SentryXDR.Services.Authentication;
using System.Text.Json;

namespace SentryXDR.Services.Workers
{
    public interface IMDOEmailRemediationService
    {
        // NEW Graph Beta Email Remediation API (analyzedEmails/remediate)
        Task<XDRRemediationResponse> RemediateEmailsAsync(XDRRemediationRequest request);
        
        // Tenant Allow/Block List Management
        Task<XDRRemediationResponse> BlockEmailSenderAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> BlockEmailDomainAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> BlockURLAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> AllowSenderAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> RemoveBlockEntryAsync(XDRRemediationRequest request);
        
        // Zero-Hour Auto Purge (ZAP)
        Task<XDRRemediationResponse> TriggerZAPForPhishingAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> TriggerZAPForMalwareAsync(XDRRemediationRequest request);

        // Email Investigation & Detonation (NEW)
        Task<XDRRemediationResponse> AnalyzeEmailForThreatsAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> DetonateURLInSandboxAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> DetonateFileInSandboxAsync(XDRRemediationRequest request);
    }

    /// <summary>
    /// MDO Email Remediation Service - NEW Graph Beta API
    /// Uses the new /security/analyzedEmails/remediate endpoint
    /// API Reference: https://learn.microsoft.com/en-us/graph/api/security-analyzedemail-remediate
    /// </summary>
    public class MDOEmailRemediationService : BaseWorkerService, IMDOEmailRemediationService
    {
        private readonly IMultiTenantAuthService _authService;
        private const string GraphBetaUrl = "https://graph.microsoft.com/beta";
        private const string GraphV1Url = "https://graph.microsoft.com/v1.0";

        public MDOEmailRemediationService(
            ILogger<MDOEmailRemediationService> logger,
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

        // ==================== NEW Email Remediation API ====================

        /// <summary>
        /// Remediate emails using NEW Graph Beta API
        /// POST /beta/security/analyzedEmails/remediate
        /// Supports: softDelete, hardDelete, moveToJunk, moveToInbox, moveToDeletedItems
        /// Permission: Mail.ReadWrite (delegated/application)
        /// </summary>
        public async Task<XDRRemediationResponse> RemediateEmailsAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "RemediateEmails");
                await SetAuthHeaderAsync(request.TenantId);

                if (!ValidateRequiredParameters(request, out var failureResponse, "action", "remediationScope"))
                {
                    return failureResponse!;
                }

                var action = request.Parameters["action"]!.ToString()!; // softDelete, hardDelete, moveToJunk, moveToInbox
                var remediationScope = request.Parameters["remediationScope"]!.ToString()!; // JSON object

                var displayName = GetOptionalParameter(request, "displayName", $"SentryXDR Remediation - {action}");
                var description = GetOptionalParameter(request, "description", request.Justification ?? "Automated email remediation");
                var severity = GetOptionalParameter(request, "severity", "high");

                Logger.LogWarning("EMAIL REMEDIATION: {Action} for scope {Scope}", action, remediationScope);

                var remediationRequest = new
                {
                    displayName = displayName,
                    description = description,
                    severity = severity,
                    action = action,
                    remediationScope = JsonSerializer.Deserialize<object>(remediationScope)
                };

                var result = await PostJsonAsync<JsonElement>(
                    $"{GraphBetaUrl}/security/analyzedEmails/remediate",
                    remediationRequest);

                if (result.Success && result.Data.HasValue)
                {
                    var remediationId = result.Data.Value.GetProperty("id").GetString();
                    var status = result.Data.Value.GetProperty("status").GetString();
                    
                    LogOperationComplete(request, "RemediateEmails", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"Email remediation initiated successfully", new Dictionary<string, object>
                    {
                        { "remediationId", remediationId! },
                        { "action", action },
                        { "status", status! },
                        { "severity", severity },
                        { "scope", remediationScope },
                        { "note", "Remediation is processing asynchronously" }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error remediating emails", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        // ==================== Tenant Allow/Block List ====================

        /// <summary>
        /// Block email sender via threat submission
        /// POST /v1.0/security/threatSubmission/emailThreats
        /// Permission: ThreatSubmission.ReadWrite.All
        /// </summary>
        public async Task<XDRRemediationResponse> BlockEmailSenderAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "BlockEmailSender");
                await SetAuthHeaderAsync(request.TenantId);

                var senderAddress = GetRequiredParameter(request, "senderAddress", out var failureResponse);
                if (senderAddress == null) return failureResponse!;

                var category = GetOptionalParameter(request, "category", "phishing"); // phishing, junk, malware, notJunk
                var attackTechnique = GetOptionalParameter(request, "attackTechnique", "phishing");

                Logger.LogWarning("BLOCK SENDER: Blocking email sender {Sender}", senderAddress);

                var submission = new
                {
                    category = category,
                    attackTechnique = attackTechnique,
                    emailSubject = GetOptionalParameter(request, "emailSubject", "Blocked by SentryXDR"),
                    sender = senderAddress,
                    shouldBlockSender = true, // This triggers the block action
                    tenantAllowOrBlockListAction = new
                    {
                        action = "block",
                        expirationDateTime = DateTime.UtcNow.AddDays(int.Parse(GetOptionalParameter(request, "expirationDays", "90"))).ToString("O")
                    }
                };

                var result = await PostJsonAsync<JsonElement>(
                    $"{GraphV1Url}/security/threatSubmission/emailThreats",
                    submission);

                if (result.Success && result.Data.HasValue)
                {
                    var submissionId = result.Data.Value.GetProperty("id").GetString();
                    
                    LogOperationComplete(request, "BlockEmailSender", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"Email sender blocked successfully", new Dictionary<string, object>
                    {
                        { "submissionId", submissionId! },
                        { "senderAddress", senderAddress },
                        { "action", "Block" },
                        { "category", category },
                        { "expiresAt", DateTime.UtcNow.AddDays(90).ToString("O") }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error blocking sender", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        /// <summary>
        /// Block email domain
        /// POST /v1.0/security/threatSubmission/emailThreats
        /// Permission: ThreatSubmission.ReadWrite.All
        /// </summary>
        public async Task<XDRRemediationResponse> BlockEmailDomainAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "BlockEmailDomain");
                await SetAuthHeaderAsync(request.TenantId);

                var domain = GetRequiredParameter(request, "domain", out var failureResponse);
                if (domain == null) return failureResponse!;

                Logger.LogWarning("BLOCK DOMAIN: Blocking email domain {Domain}", domain);

                var submission = new
                {
                    category = "phishing",
                    attackTechnique = "phishing",
                    emailSubject = $"Block domain: {domain}",
                    sender = $"no-reply@{domain}",
                    shouldBlockSender = true,
                    tenantAllowOrBlockListAction = new
                    {
                        action = "block",
                        expirationDateTime = DateTime.UtcNow.AddDays(int.Parse(GetOptionalParameter(request, "expirationDays", "90"))).ToString("O")
                    }
                };

                var result = await PostJsonAsync<JsonElement>(
                    $"{GraphV1Url}/security/threatSubmission/emailThreats",
                    submission);

                if (result.Success && result.Data.HasValue)
                {
                    var submissionId = result.Data.Value.GetProperty("id").GetString();
                    
                    LogOperationComplete(request, "BlockEmailDomain", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"Email domain blocked successfully", new Dictionary<string, object>
                    {
                        { "submissionId", submissionId! },
                        { "domain", domain },
                        { "action", "Block" },
                        { "expiresAt", DateTime.UtcNow.AddDays(90).ToString("O") }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error blocking domain", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        /// <summary>
        /// Block malicious URL
        /// POST /v1.0/security/threatSubmission/urlThreats
        /// Permission: ThreatSubmission.ReadWrite.All
        /// </summary>
        public async Task<XDRRemediationResponse> BlockURLAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "BlockURL");
                await SetAuthHeaderAsync(request.TenantId);

                var url = GetRequiredParameter(request, "url", out var failureResponse);
                if (url == null) return failureResponse!;

                var category = GetOptionalParameter(request, "category", "phishing"); // phishing, malware

                Logger.LogWarning("BLOCK URL: Blocking malicious URL {Url}", url);

                var submission = new
                {
                    category = category,
                    url = url,
                    shouldBlockUrl = true,
                    tenantAllowOrBlockListAction = new
                    {
                        action = "block",
                        expirationDateTime = DateTime.UtcNow.AddDays(int.Parse(GetOptionalParameter(request, "expirationDays", "90"))).ToString("O")
                    }
                };

                var result = await PostJsonAsync<JsonElement>(
                    $"{GraphV1Url}/security/threatSubmission/urlThreats",
                    submission);

                if (result.Success && result.Data.HasValue)
                {
                    var submissionId = result.Data.Value.GetProperty("id").GetString();
                    
                    LogOperationComplete(request, "BlockURL", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"URL blocked successfully", new Dictionary<string, object>
                    {
                        { "submissionId", submissionId! },
                        { "url", url },
                        { "action", "Block" },
                        { "category", category },
                        { "expiresAt", DateTime.UtcNow.AddDays(90).ToString("O") }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error blocking URL", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        /// <summary>
        /// Allow sender (false positive)
        /// POST /v1.0/security/threatSubmission/emailThreats
        /// Permission: ThreatSubmission.ReadWrite.All
        /// </summary>
        public async Task<XDRRemediationResponse> AllowSenderAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "AllowSender");
                await SetAuthHeaderAsync(request.TenantId);

                var senderAddress = GetRequiredParameter(request, "senderAddress", out var failureResponse);
                if (senderAddress == null) return failureResponse!;

                Logger.LogInformation("ALLOW SENDER: Allowing false positive sender {Sender}", senderAddress);

                var submission = new
                {
                    category = "notJunk",
                    attackTechnique = "unknown",
                    emailSubject = GetOptionalParameter(request, "emailSubject", "False positive - allow sender"),
                    sender = senderAddress,
                    tenantAllowOrBlockListAction = new
                    {
                        action = "allow",
                        expirationDateTime = DateTime.UtcNow.AddDays(int.Parse(GetOptionalParameter(request, "expirationDays", "30"))).ToString("O")
                    }
                };

                var result = await PostJsonAsync<JsonElement>(
                    $"{GraphV1Url}/security/threatSubmission/emailThreats",
                    submission);

                if (result.Success && result.Data.HasValue)
                {
                    var submissionId = result.Data.Value.GetProperty("id").GetString();
                    
                    LogOperationComplete(request, "AllowSender", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"Email sender allowed successfully", new Dictionary<string, object>
                    {
                        { "submissionId", submissionId! },
                        { "senderAddress", senderAddress },
                        { "action", "Allow" },
                        { "expiresAt", DateTime.UtcNow.AddDays(30).ToString("O") }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error allowing sender", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        /// <summary>
        /// Remove entry from tenant allow/block list
        /// DELETE /beta/security/tenantAllowBlockList/entries/{id}
        /// Permission: ThreatSubmission.ReadWrite.All
        /// </summary>
        public async Task<XDRRemediationResponse> RemoveBlockEntryAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "RemoveBlockEntry");
                await SetAuthHeaderAsync(request.TenantId);

                var entryId = GetRequiredParameter(request, "entryId", out var failureResponse);
                if (entryId == null) return failureResponse!;

                var result = await DeleteAsync($"{GraphBetaUrl}/security/tenantAllowBlockList/entries/{entryId}");

                if (result.Success)
                {
                    LogOperationComplete(request, "RemoveBlockEntry", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"Block list entry removed successfully", new Dictionary<string, object>
                    {
                        { "entryId", entryId },
                        { "status", "Removed" }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error removing entry", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        // ==================== Zero-Hour Auto Purge (ZAP) ====================

        /// <summary>
        /// Trigger Zero-Hour Auto Purge for phishing emails
        /// Uses new remediation API with ZAP-specific action
        /// POST /beta/security/analyzedEmails/remediate
        /// Permission: Mail.ReadWrite.All
        /// </summary>
        public async Task<XDRRemediationResponse> TriggerZAPForPhishingAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "TriggerZAPForPhishing");
                await SetAuthHeaderAsync(request.TenantId);

                var internetMessageId = GetRequiredParameter(request, "internetMessageId", out var failureResponse);
                if (internetMessageId == null) return failureResponse!;

                Logger.LogWarning("ZAP PHISHING: Triggering Zero-Hour Auto Purge for phishing email {MessageId}", internetMessageId);

                var zapRequest = new
                {
                    displayName = "ZAP - Phishing Detection",
                    description = request.Justification ?? "Zero-Hour Auto Purge for confirmed phishing email",
                    severity = "high",
                    action = "hardDelete", // ZAP uses hard delete for phishing
                    remediationScope = new
                    {
                        internetMessageIds = new[] { internetMessageId },
                        threatType = "phishing"
                    }
                };

                var result = await PostJsonAsync<JsonElement>(
                    $"{GraphBetaUrl}/security/analyzedEmails/remediate",
                    zapRequest);

                if (result.Success && result.Data.HasValue)
                {
                    var remediationId = result.Data.Value.GetProperty("id").GetString();
                    
                    LogOperationComplete(request, "TriggerZAPForPhishing", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"ZAP triggered for phishing email", new Dictionary<string, object>
                    {
                        { "remediationId", remediationId! },
                        { "internetMessageId", internetMessageId },
                        { "action", "ZAP-Phishing-HardDelete" },
                        { "threatType", "phishing" },
                        { "note", "Email will be purged from all mailboxes" }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error triggering ZAP", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        /// <summary>
        /// Trigger Zero-Hour Auto Purge for malware emails
        /// POST /beta/security/analyzedEmails/remediate
        /// Permission: Mail.ReadWrite.All
        /// </summary>
        public async Task<XDRRemediationResponse> TriggerZAPForMalwareAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "TriggerZAPForMalware");
                await SetAuthHeaderAsync(request.TenantId);

                var internetMessageId = GetRequiredParameter(request, "internetMessageId", out var failureResponse);
                if (internetMessageId == null) return failureResponse!;

                Logger.LogCritical("ZAP MALWARE: Triggering Zero-Hour Auto Purge for malware email {MessageId}", internetMessageId);

                var zapRequest = new
                {
                    displayName = "ZAP - Malware Detection",
                    description = request.Justification ?? "Zero-Hour Auto Purge for confirmed malware email",
                    severity = "high",
                    action = "hardDelete", // ZAP uses hard delete for malware
                    remediationScope = new
                    {
                        internetMessageIds = new[] { internetMessageId },
                        threatType = "malware"
                    }
                };

                var result = await PostJsonAsync<JsonElement>(
                    $"{GraphBetaUrl}/security/analyzedEmails/remediate",
                    zapRequest);

                if (result.Success && result.Data.HasValue)
                {
                    var remediationId = result.Data.Value.GetProperty("id").GetString();
                    
                    LogOperationComplete(request, "TriggerZAPForMalware", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"ZAP triggered for malware email", new Dictionary<string, object>
                    {
                        { "remediationId", remediationId! },
                        { "internetMessageId", internetMessageId },
                        { "action", "ZAP-Malware-HardDelete" },
                        { "threatType", "malware" },
                        { "note", "Email will be purged from all mailboxes immediately" }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error triggering ZAP", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        // ==================== Email Investigation & Detonation ====================

        /// <summary>
        /// Analyze email for threats using Microsoft's sandbox
        /// POST /security/collaboration/analyzedEmails (Graph Beta)
        /// Permission: ThreatSubmission.ReadWrite.All
        /// </summary>
        public async Task<XDRRemediationResponse> AnalyzeEmailForThreatsAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "AnalyzeEmailForThreats");
                await SetAuthHeaderAsync(request.TenantId);

                if (!ValidateRequiredParameters(request, out var failureResponse, "recipientEmailAddress", "internetMessageId"))
                {
                    return failureResponse!;
                }

                var recipientEmail = request.Parameters["recipientEmailAddress"]!.ToString()!;
                var messageId = request.Parameters["internetMessageId"]!.ToString()!;

                Logger.LogInformation("ANALYZE EMAIL: Investigating email {MessageId} for recipient {Recipient}", messageId, recipientEmail);

                var analysisRequest = new
                {
                    recipientEmailAddress = recipientEmail,
                    internetMessageId = messageId,
                    analysisType = "detailed" // detailed analysis includes detonation
                };

                var result = await PostJsonAsync<JsonElement>(
                    $"{GraphBetaUrl}/security/collaboration/analyzedEmails",
                    analysisRequest);

                if (result.Success && result.Data.HasValue)
                {
                    var analysisId = result.Data.Value.GetProperty("id").GetString();
                    var status = result.Data.Value.GetProperty("analysisStatus").GetString();
                    
                    LogOperationComplete(request, "AnalyzeEmailForThreats", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"Email analysis initiated successfully", new Dictionary<string, object>
                    {
                        { "analysisId", analysisId! },
                        { "messageId", messageId },
                        { "status", status! },
                        { "recipient", recipientEmail },
                        { "note", "Analysis includes URL and attachment detonation in sandbox" }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error analyzing email", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        /// <summary>
        /// Detonate URL in Microsoft sandbox
        /// POST /security/threatSubmission/urlThreats (with detonation flag)
        /// Permission: ThreatSubmission.ReadWrite.All
        /// </summary>
        public async Task<XDRRemediationResponse> DetonateURLInSandboxAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "DetonateURLInSandbox");
                await SetAuthHeaderAsync(request.TenantId);

                var url = GetRequiredParameter(request, "url", out var failureResponse);
                if (url == null) return failureResponse!;

                Logger.LogInformation("DETONATE URL: Submitting URL {Url} for sandbox detonation", url);

                var detonationRequest = new
                {
                    odataType = "#microsoft.graph.security.urlThreatSubmission",
                    category = "malware",
                    webUrl = url,
                    contentType = "url",
                    submissionSource = "administrator",
                    detonationRequired = true // Request detonation
                };

                var result = await PostJsonAsync<JsonElement>(
                    $"{GraphBaseUrl}/security/threatSubmission/urlThreats",
                    detonationRequest);

                if (result.Success && result.Data.HasValue)
                {
                    var submissionId = result.Data.Value.GetProperty("id").GetString();
                    
                    LogOperationComplete(request, "DetonateURLInSandbox", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"URL submitted for sandbox detonation", new Dictionary<string, object>
                    {
                        { "submissionId", submissionId! },
                        { "url", url },
                        { "action", "Detonation" },
                        { "note", "Microsoft will detonate the URL in a sandbox environment" }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error detonating URL", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        /// <summary>
        /// Detonate file in Microsoft sandbox
        /// POST /security/threatSubmission/fileThreats (with detonation flag)
        /// Permission: ThreatSubmission.ReadWrite.All
        /// </summary>
        public async Task<XDRRemediationResponse> DetonateFileInSandboxAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "DetonateFileInSandbox");
                await SetAuthHeaderAsync(request.TenantId);

                if (!ValidateRequiredParameters(request, out var failureResponse, "fileName", "fileHash"))
                {
                    return failureResponse!;
                }

                var fileName = request.Parameters["fileName"]!.ToString()!;
                var fileHash = request.Parameters["fileHash"]!.ToString()!;

                Logger.LogInformation("DETONATE FILE: Submitting file {FileName} (hash: {Hash}) for sandbox detonation", fileName, fileHash);

                var detonationRequest = new
                {
                    odataType = "#microsoft.graph.security.fileThreatSubmission",
                    category = "malware",
                    fileName = fileName,
                    fileHash = fileHash,
                    contentType = "file",
                    submissionSource = "administrator",
                    detonationRequired = true // Request detonation
                };

                var result = await PostJsonAsync<JsonElement>(
                    $"{GraphBaseUrl}/security/threatSubmission/fileThreats",
                    detonationRequest);

                if (result.Success && result.Data.HasValue)
                {
                    var submissionId = result.Data.Value.GetProperty("id").GetString();
                    
                    LogOperationComplete(request, "DetonateFileInSandbox", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"File submitted for sandbox detonation", new Dictionary<string, object>
                    {
                        { "submissionId", submissionId! },
                        { "fileName", fileName },
                        { "fileHash", fileHash },
                        { "action", "Detonation" },
                        { "note", "Microsoft will detonate the file in a sandbox environment" }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error detonating file", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }
    }
}
