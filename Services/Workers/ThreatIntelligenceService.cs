using Microsoft.Extensions.Logging;
using SentryXDR.Models;
using SentryXDR.Services.Authentication;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SentryXDR.Services.Workers
{
    public interface IThreatIntelligenceService
    {
        // IOC Management
        Task<XDRRemediationResponse> SubmitIOCAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> UpdateIOCAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> DeleteIOCAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> GetIOCAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> ListIOCsAsync(XDRRemediationRequest request);
        
        // Specific Indicator Types
        Task<XDRRemediationResponse> SubmitFileIndicatorAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> SubmitIPIndicatorAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> SubmitDomainIndicatorAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> SubmitURLIndicatorAsync(XDRRemediationRequest request);
        
        // Bulk Operations
        Task<XDRRemediationResponse> BatchSubmitIndicatorsAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> GetIOCByValueAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> BulkDeleteIOCsAsync(XDRRemediationRequest request);
    }

    /// <summary>
    /// Threat Intelligence Service for Microsoft Defender for Endpoint
    /// Manages Indicators of Compromise (IOCs) for threat prevention
    /// API Reference: https://learn.microsoft.com/en-us/microsoft-365/security/defender-endpoint/ti-indicator
    /// </summary>
    public class ThreatIntelligenceService : IThreatIntelligenceService
    {
        private readonly ILogger<ThreatIntelligenceService> _logger;
        private readonly IMultiTenantAuthService _authService;
        private readonly HttpClient _httpClient;
        private const string MdeBaseUrl = "https://api.securitycenter.microsoft.com/api";

        public ThreatIntelligenceService(
            ILogger<ThreatIntelligenceService> logger,
            IMultiTenantAuthService authService,
            HttpClient httpClient)
        {
            _logger = logger;
            _authService = authService;
            _httpClient = httpClient;
        }

        private async Task SetAuthHeaderAsync(string tenantId)
        {
            var token = await _authService.GetMdeTokenAsync(tenantId);
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        // ==================== IOC Management ====================

        /// <summary>
        /// Submit a new IOC to MDE
        /// POST /indicators
        /// </summary>
        public async Task<XDRRemediationResponse> SubmitIOCAsync(XDRRemediationRequest request)
        {
            try
            {
                await SetAuthHeaderAsync(request.TenantId);
                
                var indicatorValue = request.Parameters["indicatorValue"]?.ToString();
                var indicatorType = request.Parameters["indicatorType"]?.ToString(); // FileSha1, FileSha256, IpAddress, DomainName, Url
                var action = request.Parameters["action"]?.ToString() ?? "Alert"; // Alert, AlertAndBlock, Allowed
                var title = request.Parameters["title"]?.ToString() ?? $"IOC: {indicatorValue}";
                var description = request.Parameters["description"]?.ToString();
                var severity = request.Parameters["severity"]?.ToString() ?? "Medium"; // Informational, Low, Medium, High
                var expirationTime = request.Parameters.GetValueOrDefault("expirationTime", DateTime.UtcNow.AddDays(30));

                _logger.LogInformation("Submitting IOC {Type}: {Value}", indicatorType, indicatorValue);

                var iocBody = new
                {
                    indicatorValue = indicatorValue,
                    indicatorType = indicatorType,
                    action = action,
                    title = title,
                    description = description,
                    severity = severity,
                    expirationTime = expirationTime,
                    application = "SentryXDR",
                    recommendedActions = request.Parameters.GetValueOrDefault("recommendedActions", "Investigate and remediate")
                };

                var url = $"{MdeBaseUrl}/indicators";
                var content = new StringContent(JsonSerializer.Serialize(iocBody), Encoding.UTF8, "application/json");
                var result = await _httpClient.PostAsync(url, content);

                if (result.IsSuccessStatusCode)
                {
                    var responseContent = await result.Content.ReadAsStringAsync();
                    var indicator = JsonSerializer.Deserialize<JsonElement>(responseContent);
                    var indicatorId = indicator.GetProperty("id").GetString();

                    return CreateSuccessResponse(request, $"IOC {indicatorValue} submitted successfully", new Dictionary<string, object>
                    {
                        { "indicatorId", indicatorId! },
                        { "indicatorValue", indicatorValue! },
                        { "indicatorType", indicatorType! },
                        { "action", action }
                    });
                }
                else
                {
                    var error = await result.Content.ReadAsStringAsync();
                    return CreateFailureResponse(request, $"Failed to submit IOC: {error}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to submit IOC");
                return CreateExceptionResponse(request, ex);
            }
        }

        /// <summary>
        /// Update an existing IOC
        /// PATCH /indicators/{id}
        /// </summary>
        public async Task<XDRRemediationResponse> UpdateIOCAsync(XDRRemediationRequest request)
        {
            try
            {
                await SetAuthHeaderAsync(request.TenantId);
                
                var indicatorId = request.Parameters["indicatorId"]?.ToString();
                var action = request.Parameters["action"]?.ToString();
                var title = request.Parameters["title"]?.ToString();
                var description = request.Parameters["description"]?.ToString();
                var severity = request.Parameters["severity"]?.ToString();
                var expirationTime = request.Parameters.GetValueOrDefault("expirationTime");

                _logger.LogInformation("Updating IOC {IndicatorId}", indicatorId);

                var updateBody = new Dictionary<string, object>();
                if (action != null) updateBody["action"] = action;
                if (title != null) updateBody["title"] = title;
                if (description != null) updateBody["description"] = description;
                if (severity != null) updateBody["severity"] = severity;
                if (expirationTime != null) updateBody["expirationTime"] = expirationTime;

                var url = $"{MdeBaseUrl}/indicators/{indicatorId}";
                var content = new StringContent(JsonSerializer.Serialize(updateBody), Encoding.UTF8, "application/json");
                var result = await _httpClient.PatchAsync(url, content);

                if (result.IsSuccessStatusCode)
                {
                    return CreateSuccessResponse(request, $"IOC {indicatorId} updated successfully");
                }
                else
                {
                    var error = await result.Content.ReadAsStringAsync();
                    return CreateFailureResponse(request, $"Failed to update IOC: {error}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update IOC");
                return CreateExceptionResponse(request, ex);
            }
        }

        /// <summary>
        /// Delete an IOC
        /// DELETE /indicators/{id}
        /// </summary>
        public async Task<XDRRemediationResponse> DeleteIOCAsync(XDRRemediationRequest request)
        {
            try
            {
                await SetAuthHeaderAsync(request.TenantId);
                
                var indicatorId = request.Parameters["indicatorId"]?.ToString();

                _logger.LogInformation("Deleting IOC {IndicatorId}", indicatorId);

                var url = $"{MdeBaseUrl}/indicators/{indicatorId}";
                var result = await _httpClient.DeleteAsync(url);

                if (result.IsSuccessStatusCode)
                {
                    return CreateSuccessResponse(request, $"IOC {indicatorId} deleted successfully");
                }
                else
                {
                    var error = await result.Content.ReadAsStringAsync();
                    return CreateFailureResponse(request, $"Failed to delete IOC: {error}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete IOC");
                return CreateExceptionResponse(request, ex);
            }
        }

        /// <summary>
        /// Get IOC details
        /// GET /indicators/{id}
        /// </summary>
        public async Task<XDRRemediationResponse> GetIOCAsync(XDRRemediationRequest request)
        {
            try
            {
                await SetAuthHeaderAsync(request.TenantId);
                
                var indicatorId = request.Parameters["indicatorId"]?.ToString();

                _logger.LogInformation("Getting IOC {IndicatorId}", indicatorId);

                var url = $"{MdeBaseUrl}/indicators/{indicatorId}";
                var result = await _httpClient.GetAsync(url);

                if (result.IsSuccessStatusCode)
                {
                    var indicatorData = await result.Content.ReadAsStringAsync();
                    var indicator = JsonSerializer.Deserialize<JsonElement>(indicatorData);

                    return CreateSuccessResponse(request, $"IOC {indicatorId} retrieved", new Dictionary<string, object>
                    {
                        { "indicator", indicatorData }
                    });
                }
                else
                {
                    var error = await result.Content.ReadAsStringAsync();
                    return CreateFailureResponse(request, $"Failed to get IOC: {error}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get IOC");
                return CreateExceptionResponse(request, ex);
            }
        }

        /// <summary>
        /// List all IOCs
        /// GET /indicators
        /// </summary>
        public async Task<XDRRemediationResponse> ListIOCsAsync(XDRRemediationRequest request)
        {
            try
            {
                await SetAuthHeaderAsync(request.TenantId);

                _logger.LogInformation("Listing IOCs");

                var url = $"{MdeBaseUrl}/indicators";
                var result = await _httpClient.GetAsync(url);

                if (result.IsSuccessStatusCode)
                {
                    var indicatorsData = await result.Content.ReadAsStringAsync();
                    var indicators = JsonSerializer.Deserialize<JsonElement>(indicatorsData);
                    var count = indicators.GetProperty("value").GetArrayLength();

                    return CreateSuccessResponse(request, $"Retrieved {count} IOCs", new Dictionary<string, object>
                    {
                        { "indicators", indicatorsData },
                        { "count", count }
                    });
                }
                else
                {
                    var error = await result.Content.ReadAsStringAsync();
                    return CreateFailureResponse(request, $"Failed to list IOCs: {error}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to list IOCs");
                return CreateExceptionResponse(request, ex);
            }
        }

        // ==================== Specific Indicator Types ====================

        /// <summary>
        /// Submit file hash indicator
        /// POST /indicators (type: FileSha1 or FileSha256)
        /// </summary>
        public async Task<XDRRemediationResponse> SubmitFileIndicatorAsync(XDRRemediationRequest request)
        {
            try
            {
                var fileHash = request.Parameters["fileHash"]?.ToString();
                var hashType = fileHash?.Length == 40 ? "FileSha1" : "FileSha256";

                request.Parameters["indicatorValue"] = fileHash;
                request.Parameters["indicatorType"] = hashType;
                request.Parameters["title"] = request.Parameters.GetValueOrDefault("title", $"Malicious File: {fileHash}");

                return await SubmitIOCAsync(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to submit file indicator");
                return CreateExceptionResponse(request, ex);
            }
        }

        /// <summary>
        /// Submit IP address indicator
        /// POST /indicators (type: IpAddress)
        /// </summary>
        public async Task<XDRRemediationResponse> SubmitIPIndicatorAsync(XDRRemediationRequest request)
        {
            try
            {
                var ipAddress = request.Parameters["ipAddress"]?.ToString();

                request.Parameters["indicatorValue"] = ipAddress;
                request.Parameters["indicatorType"] = "IpAddress";
                request.Parameters["title"] = request.Parameters.GetValueOrDefault("title", $"Malicious IP: {ipAddress}");

                return await SubmitIOCAsync(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to submit IP indicator");
                return CreateExceptionResponse(request, ex);
            }
        }

        /// <summary>
        /// Submit domain indicator
        /// POST /indicators (type: DomainName)
        /// </summary>
        public async Task<XDRRemediationResponse> SubmitDomainIndicatorAsync(XDRRemediationRequest request)
        {
            try
            {
                var domain = request.Parameters["domain"]?.ToString();

                request.Parameters["indicatorValue"] = domain;
                request.Parameters["indicatorType"] = "DomainName";
                request.Parameters["title"] = request.Parameters.GetValueOrDefault("title", $"Malicious Domain: {domain}");

                return await SubmitIOCAsync(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to submit domain indicator");
                return CreateExceptionResponse(request, ex);
            }
        }

        /// <summary>
        /// Submit URL indicator
        /// POST /indicators (type: Url)
        /// </summary>
        public async Task<XDRRemediationResponse> SubmitURLIndicatorAsync(XDRRemediationRequest request)
        {
            try
            {
                var url = request.Parameters["url"]?.ToString();

                request.Parameters["indicatorValue"] = url;
                request.Parameters["indicatorType"] = "Url";
                request.Parameters["title"] = request.Parameters.GetValueOrDefault("title", $"Malicious URL: {url}");

                return await SubmitIOCAsync(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to submit URL indicator");
                return CreateExceptionResponse(request, ex);
            }
        }

        // ==================== Bulk Operations ====================

        /// <summary>
        /// Submit multiple indicators in batch
        /// POST /indicators/batch
        /// </summary>
        public async Task<XDRRemediationResponse> BatchSubmitIndicatorsAsync(XDRRemediationRequest request)
        {
            try
            {
                await SetAuthHeaderAsync(request.TenantId);
                
                var indicators = request.Parameters["indicators"] as List<Dictionary<string, object>> ?? new List<Dictionary<string, object>>();

                _logger.LogInformation("Batch submitting {Count} indicators", indicators.Count);

                var url = $"{MdeBaseUrl}/indicators/batch";
                var content = new StringContent(JsonSerializer.Serialize(new { indicators = indicators }), Encoding.UTF8, "application/json");
                var result = await _httpClient.PostAsync(url, content);

                if (result.IsSuccessStatusCode)
                {
                    return CreateSuccessResponse(request, $"{indicators.Count} indicators submitted successfully");
                }
                else
                {
                    var error = await result.Content.ReadAsStringAsync();
                    return CreateFailureResponse(request, $"Failed to batch submit indicators: {error}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to batch submit indicators");
                return CreateExceptionResponse(request, ex);
            }
        }

        /// <summary>
        /// Get IOC by value
        /// GET /indicators?$filter=indicatorValue eq '{value}'
        /// </summary>
        public async Task<XDRRemediationResponse> GetIOCByValueAsync(XDRRemediationRequest request)
        {
            try
            {
                await SetAuthHeaderAsync(request.TenantId);
                
                var indicatorValue = request.Parameters["indicatorValue"]?.ToString();

                _logger.LogInformation("Getting IOC by value: {Value}", indicatorValue);

                var url = $"{MdeBaseUrl}/indicators?$filter=indicatorValue eq '{indicatorValue}'";
                var result = await _httpClient.GetAsync(url);

                if (result.IsSuccessStatusCode)
                {
                    var indicatorData = await result.Content.ReadAsStringAsync();
                    return CreateSuccessResponse(request, $"IOC retrieved for value: {indicatorValue}", new Dictionary<string, object>
                    {
                        { "indicator", indicatorData }
                    });
                }
                else
                {
                    var error = await result.Content.ReadAsStringAsync();
                    return CreateFailureResponse(request, $"Failed to get IOC by value: {error}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get IOC by value");
                return CreateExceptionResponse(request, ex);
            }
        }

        /// <summary>
        /// Delete multiple IOCs
        /// POST /indicators/batchDelete
        /// </summary>
        public async Task<XDRRemediationResponse> BulkDeleteIOCsAsync(XDRRemediationRequest request)
        {
            try
            {
                await SetAuthHeaderAsync(request.TenantId);
                
                var indicatorIds = request.Parameters["indicatorIds"] as List<string> ?? new List<string>();

                _logger.LogInformation("Bulk deleting {Count} indicators", indicatorIds.Count);

                var url = $"{MdeBaseUrl}/indicators/batchDelete";
                var content = new StringContent(JsonSerializer.Serialize(new { indicatorIds = indicatorIds }), Encoding.UTF8, "application/json");
                var result = await _httpClient.PostAsync(url, content);

                if (result.IsSuccessStatusCode)
                {
                    return CreateSuccessResponse(request, $"{indicatorIds.Count} indicators deleted successfully");
                }
                else
                {
                    var error = await result.Content.ReadAsStringAsync();
                    return CreateFailureResponse(request, $"Failed to bulk delete indicators: {error}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to bulk delete indicators");
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
