using Microsoft.Extensions.Logging;
using SentryXDR.Models;
using SentryXDR.Services.Authentication;
using System.Text.Json;

namespace SentryXDR.Services.Workers
{
    public interface IMDEIndicatorService
    {
        // Indicator Submission
        Task<XDRRemediationResponse> SubmitFileHashIndicatorAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> SubmitIPAddressIndicatorAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> SubmitURLIndicatorAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> SubmitDomainIndicatorAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> BatchSubmitIndicatorsAsync(XDRRemediationRequest request);
        
        // Indicator Management
        Task<XDRRemediationResponse> UpdateIndicatorAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> DeleteIndicatorAsync(XDRRemediationRequest request);
    }

    /// <summary>
    /// MDE Indicator (IOC) Management Service
    /// Manages Indicators of Compromise across the environment
    /// API Reference: https://learn.microsoft.com/en-us/microsoft-365/security/defender-endpoint/ti-indicator
    /// </summary>
    public class MDEIndicatorService : BaseWorkerService, IMDEIndicatorService
    {
        private readonly IMultiTenantAuthService _authService;
        private const string MdeBaseUrl = "https://api.securitycenter.microsoft.com/api";

        public MDEIndicatorService(
            ILogger<MDEIndicatorService> logger,
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

        // ==================== File Hash Indicators ====================

        /// <summary>
        /// Submit file hash indicator (SHA256, SHA1, MD5)
        /// POST /api/indicators
        /// Permission: Ti.ReadWrite.All
        /// </summary>
        public async Task<XDRRemediationResponse> SubmitFileHashIndicatorAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "SubmitFileHashIndicator");
                await SetAuthHeaderAsync(request.TenantId);

                if (!ValidateRequiredParameters(request, out var failureResponse, 
                    "indicatorValue", "indicatorType", "action", "severity"))
                {
                    return failureResponse!;
                }

                var indicatorValue = request.Parameters["indicatorValue"]!.ToString()!;
                var indicatorType = request.Parameters["indicatorType"]!.ToString()!; // FileSha256, FileSha1, FileMd5
                var action = request.Parameters["action"]!.ToString()!; // Alert, Block, Warn
                var severity = request.Parameters["severity"]!.ToString()!; // Informational, Low, Medium, High
                var title = GetOptionalParameter(request, "title", $"IOC: {indicatorValue}");
                var description = GetOptionalParameter(request, "description", request.Justification ?? "Submitted by SentryXDR");
                var recommendedActions = GetOptionalParameter(request, "recommendedActions", "Investigate and remediate");
                var expirationTime = request.Parameters.GetValueOrDefault("expirationTime")?.ToString();

                var indicator = new
                {
                    indicatorValue = indicatorValue,
                    indicatorType = indicatorType,
                    action = action,
                    severity = severity,
                    title = title,
                    description = description,
                    recommendedActions = recommendedActions,
                    expirationTime = expirationTime ?? DateTime.UtcNow.AddDays(90).ToString("yyyy-MM-ddTHH:mm:ssZ")
                };

                var result = await PostJsonAsync<JsonElement>($"{MdeBaseUrl}/indicators", indicator);

                if (result.Success && result.Data.HasValue)
                {
                    var indicatorId = result.Data.Value.GetProperty("id").GetString();
                    
                    LogOperationComplete(request, "SubmitFileHashIndicator", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"File hash indicator submitted successfully", new Dictionary<string, object>
                    {
                        { "indicatorId", indicatorId! },
                        { "indicatorValue", indicatorValue },
                        { "indicatorType", indicatorType },
                        { "action", action },
                        { "severity", severity },
                        { "expiresAt", expirationTime ?? DateTime.UtcNow.AddDays(90).ToString("yyyy-MM-ddTHH:mm:ssZ") }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error submitting indicator", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        // ==================== IP Address Indicators ====================

        /// <summary>
        /// Submit IP address indicator to block malicious IPs
        /// POST /api/indicators
        /// Permission: Ti.ReadWrite.All
        /// </summary>
        public async Task<XDRRemediationResponse> SubmitIPAddressIndicatorAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "SubmitIPAddressIndicator");
                await SetAuthHeaderAsync(request.TenantId);

                var ipAddress = GetRequiredParameter(request, "ipAddress", out var failureResponse);
                if (ipAddress == null) return failureResponse!;

                var action = GetOptionalParameter(request, "action", "Block"); // Alert, Block, Warn
                var severity = GetOptionalParameter(request, "severity", "High"); // Informational, Low, Medium, High
                var title = GetOptionalParameter(request, "title", $"Block IP: {ipAddress}");
                var description = GetOptionalParameter(request, "description", request.Justification ?? "Malicious IP blocked by SentryXDR");
                var expirationDays = int.Parse(GetOptionalParameter(request, "expirationDays", "90"));

                var indicator = new
                {
                    indicatorValue = ipAddress,
                    indicatorType = "IpAddress",
                    action = action,
                    severity = severity,
                    title = title,
                    description = description,
                    recommendedActions = "Block all traffic from this IP address",
                    expirationTime = DateTime.UtcNow.AddDays(expirationDays).ToString("yyyy-MM-ddTHH:mm:ssZ")
                };

                var result = await PostJsonAsync<JsonElement>($"{MdeBaseUrl}/indicators", indicator);

                if (result.Success && result.Data.HasValue)
                {
                    var indicatorId = result.Data.Value.GetProperty("id").GetString();
                    
                    LogOperationComplete(request, "SubmitIPAddressIndicator", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"IP address indicator submitted successfully", new Dictionary<string, object>
                    {
                        { "indicatorId", indicatorId! },
                        { "ipAddress", ipAddress },
                        { "action", action },
                        { "severity", severity },
                        { "expiresAt", DateTime.UtcNow.AddDays(expirationDays).ToString("yyyy-MM-ddTHH:mm:ssZ") }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error submitting IP indicator", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        // ==================== URL Indicators ====================

        /// <summary>
        /// Submit URL indicator to block malicious URLs
        /// POST /api/indicators
        /// Permission: Ti.ReadWrite.All
        /// </summary>
        public async Task<XDRRemediationResponse> SubmitURLIndicatorAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "SubmitURLIndicator");
                await SetAuthHeaderAsync(request.TenantId);

                var url = GetRequiredParameter(request, "url", out var failureResponse);
                if (url == null) return failureResponse!;

                var action = GetOptionalParameter(request, "action", "Block");
                var severity = GetOptionalParameter(request, "severity", "High");
                var title = GetOptionalParameter(request, "title", $"Block URL: {url}");
                var description = GetOptionalParameter(request, "description", request.Justification ?? "Malicious URL blocked by SentryXDR");
                var expirationDays = int.Parse(GetOptionalParameter(request, "expirationDays", "90"));

                var indicator = new
                {
                    indicatorValue = url,
                    indicatorType = "Url",
                    action = action,
                    severity = severity,
                    title = title,
                    description = description,
                    recommendedActions = "Block access to this URL",
                    expirationTime = DateTime.UtcNow.AddDays(expirationDays).ToString("yyyy-MM-ddTHH:mm:ssZ")
                };

                var result = await PostJsonAsync<JsonElement>($"{MdeBaseUrl}/indicators", indicator);

                if (result.Success && result.Data.HasValue)
                {
                    var indicatorId = result.Data.Value.GetProperty("id").GetString();
                    
                    LogOperationComplete(request, "SubmitURLIndicator", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"URL indicator submitted successfully", new Dictionary<string, object>
                    {
                        { "indicatorId", indicatorId! },
                        { "url", url },
                        { "action", action },
                        { "severity", severity },
                        { "expiresAt", DateTime.UtcNow.AddDays(expirationDays).ToString("yyyy-MM-ddTHH:mm:ssZ") }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error submitting URL indicator", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        // ==================== Domain Indicators ====================

        /// <summary>
        /// Submit domain indicator to block malicious domains
        /// POST /api/indicators
        /// Permission: Ti.ReadWrite.All
        /// </summary>
        public async Task<XDRRemediationResponse> SubmitDomainIndicatorAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "SubmitDomainIndicator");
                await SetAuthHeaderAsync(request.TenantId);

                var domain = GetRequiredParameter(request, "domain", out var failureResponse);
                if (domain == null) return failureResponse!;

                var action = GetOptionalParameter(request, "action", "Block");
                var severity = GetOptionalParameter(request, "severity", "High");
                var title = GetOptionalParameter(request, "title", $"Block Domain: {domain}");
                var description = GetOptionalParameter(request, "description", request.Justification ?? "Malicious domain blocked by SentryXDR");
                var expirationDays = int.Parse(GetOptionalParameter(request, "expirationDays", "90"));

                var indicator = new
                {
                    indicatorValue = domain,
                    indicatorType = "DomainName",
                    action = action,
                    severity = severity,
                    title = title,
                    description = description,
                    recommendedActions = "Block all traffic to this domain",
                    expirationTime = DateTime.UtcNow.AddDays(expirationDays).ToString("yyyy-MM-ddTHH:mm:ssZ")
                };

                var result = await PostJsonAsync<JsonElement>($"{MdeBaseUrl}/indicators", indicator);

                if (result.Success && result.Data.HasValue)
                {
                    var indicatorId = result.Data.Value.GetProperty("id").GetString();
                    
                    LogOperationComplete(request, "SubmitDomainIndicator", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"Domain indicator submitted successfully", new Dictionary<string, object>
                    {
                        { "indicatorId", indicatorId! },
                        { "domain", domain },
                        { "action", action },
                        { "severity", severity },
                        { "expiresAt", DateTime.UtcNow.AddDays(expirationDays).ToString("yyyy-MM-ddTHH:mm:ssZ") }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error submitting domain indicator", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        // ==================== Batch Operations ====================

        /// <summary>
        /// Batch submit multiple indicators (up to 20 per request)
        /// POST /api/indicators/import
        /// Permission: Ti.ReadWrite.All
        /// </summary>
        public async Task<XDRRemediationResponse> BatchSubmitIndicatorsAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "BatchSubmitIndicators");
                await SetAuthHeaderAsync(request.TenantId);

                var indicatorsJson = GetRequiredParameter(request, "indicators", out var failureResponse);
                if (indicatorsJson == null) return failureResponse!;

                var indicators = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(indicatorsJson);
                
                if (indicators == null || !indicators.Any())
                {
                    return CreateFailureResponse(request, "No indicators provided", startTime);
                }

                if (indicators.Count > 20)
                {
                    return CreateFailureResponse(request, "Maximum 20 indicators allowed per batch request", startTime);
                }

                // Add default values to indicators if not specified
                foreach (var indicator in indicators)
                {
                    if (!indicator.ContainsKey("expirationTime"))
                    {
                        indicator["expirationTime"] = DateTime.UtcNow.AddDays(90).ToString("yyyy-MM-ddTHH:mm:ssZ");
                    }
                    if (!indicator.ContainsKey("severity"))
                    {
                        indicator["severity"] = "High";
                    }
                    if (!indicator.ContainsKey("action"))
                    {
                        indicator["action"] = "Block";
                    }
                }

                var batchRequest = new
                {
                    indicators = indicators
                };

                var result = await PostJsonAsync<JsonElement>($"{MdeBaseUrl}/indicators/import", batchRequest);

                if (result.Success && result.Data.HasValue)
                {
                    var response = result.Data.Value;
                    var successCount = response.GetProperty("successCount").GetInt32();
                    var failureCount = response.GetProperty("failureCount").GetInt32();
                    
                    LogOperationComplete(request, "BatchSubmitIndicators", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"Batch submitted: {successCount} succeeded, {failureCount} failed", new Dictionary<string, object>
                    {
                        { "totalSubmitted", indicators.Count },
                        { "successCount", successCount },
                        { "failureCount", failureCount },
                        { "indicators", indicators.Select(i => i.GetValueOrDefault("indicatorValue")).ToList()! }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error in batch submission", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        // ==================== Indicator Management ====================

        /// <summary>
        /// Update existing indicator properties
        /// PATCH /api/indicators/{id}
        /// Permission: Ti.ReadWrite.All
        /// </summary>
        public async Task<XDRRemediationResponse> UpdateIndicatorAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "UpdateIndicator");
                await SetAuthHeaderAsync(request.TenantId);

                var indicatorId = GetRequiredParameter(request, "indicatorId", out var failureResponse);
                if (indicatorId == null) return failureResponse!;

                var updates = new Dictionary<string, object>();
                
                // Optional fields to update
                if (request.Parameters.ContainsKey("action"))
                    updates["action"] = request.Parameters["action"]!;
                if (request.Parameters.ContainsKey("severity"))
                    updates["severity"] = request.Parameters["severity"]!;
                if (request.Parameters.ContainsKey("title"))
                    updates["title"] = request.Parameters["title"]!;
                if (request.Parameters.ContainsKey("description"))
                    updates["description"] = request.Parameters["description"]!;
                if (request.Parameters.ContainsKey("expirationTime"))
                    updates["expirationTime"] = request.Parameters["expirationTime"]!;

                if (!updates.Any())
                {
                    return CreateFailureResponse(request, "No update fields provided", startTime);
                }

                var result = await PatchJsonAsync<JsonElement>($"{MdeBaseUrl}/indicators/{indicatorId}", updates);

                if (result.Success)
                {
                    LogOperationComplete(request, "UpdateIndicator", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"Indicator updated successfully", new Dictionary<string, object>
                    {
                        { "indicatorId", indicatorId },
                        { "updatedFields", updates.Keys.ToList() }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error updating indicator", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        /// <summary>
        /// Delete indicator by ID
        /// DELETE /api/indicators/{id}
        /// Permission: Ti.ReadWrite.All
        /// </summary>
        public async Task<XDRRemediationResponse> DeleteIndicatorAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "DeleteIndicator");
                await SetAuthHeaderAsync(request.TenantId);

                var indicatorId = GetRequiredParameter(request, "indicatorId", out var failureResponse);
                if (indicatorId == null) return failureResponse!;

                var result = await DeleteAsync($"{MdeBaseUrl}/indicators/{indicatorId}");

                if (result.Success)
                {
                    LogOperationComplete(request, "DeleteIndicator", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"Indicator deleted successfully", new Dictionary<string, object>
                    {
                        { "indicatorId", indicatorId },
                        { "status", "Deleted" }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error deleting indicator", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }
    }
}
