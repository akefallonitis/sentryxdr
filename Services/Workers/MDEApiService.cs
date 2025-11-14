using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SentryXDR.Models;
using SentryXDR.Services.Authentication;

namespace SentryXDR.Services.Workers
{
    public interface IMDEApiService
    {
        Task<XDRRemediationResponse> IsolateDeviceAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> ReleaseDeviceAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> RunAntivirusScanAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> CollectInvestigationPackageAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> StopAndQuarantineFileAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> RestrictAppExecutionAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> RemoveAppRestrictionAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> StartAutomatedInvestigationAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> SubmitIndicatorAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> InitiateAutomatedInvestigationAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> CancelMachineActionAsync(XDRRemediationRequest request);
    }

    /// <summary>
    /// Microsoft Defender for Endpoint API Service
    /// API Reference: https://learn.microsoft.com/en-us/defender-endpoint/api/apis-intro
    /// </summary>
    public class MDEApiService : IMDEApiService
    {
        private readonly HttpClient _httpClient;
        private readonly IMultiTenantAuthService _authService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<MDEApiService> _logger;
        private readonly string _baseUrl;

        public MDEApiService(
            HttpClient httpClient,
            IMultiTenantAuthService authService,
            IConfiguration configuration,
            ILogger<MDEApiService> logger)
        {
            _httpClient = httpClient;
            _authService = authService;
            _configuration = configuration;
            _logger = logger;
            _baseUrl = configuration["MDE:BaseUrl"] ?? "https://api.securitycenter.microsoft.com";
        }

        private async Task SetAuthHeaderAsync(string tenantId)
        {
            var token = await _authService.GetMDETokenAsync(tenantId);
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        public async Task<XDRRemediationResponse> IsolateDeviceAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            await SetAuthHeaderAsync(request.TenantId);

            var deviceId = request.Parameters["deviceId"]?.ToString() 
                ?? request.Parameters["machineId"]?.ToString();
            
            var isolationType = request.Parameters.GetValueOrDefault("isolationType", "Full").ToString();
            var comment = $"XDR Remediation - Incident: {request.IncidentId} - {request.Justification}";

            var payload = new
            {
                Comment = comment,
                IsolationType = isolationType
            };

            try
            {
                var response = await _httpClient.PostAsync(
                    $"{_baseUrl}/api/machines/{deviceId}/isolate",
                    new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var actionResponse = JsonSerializer.Deserialize<MDEMachineActionResponse>(content);
                    
                    return new XDRRemediationResponse
                    {
                        RequestId = request.RequestId,
                        TenantId = request.TenantId,
                        IncidentId = request.IncidentId,
                        Success = true,
                        Status = "Completed",
                        Message = $"Device {deviceId} isolated successfully",
                        Details = new Dictionary<string, object>
                        {
                            ["deviceId"] = deviceId,
                            ["isolationType"] = isolationType,
                            ["actionId"] = actionResponse?.Id ?? "",
                            ["actionStatus"] = actionResponse?.Status ?? ""
                        },
                        ActionId = actionResponse?.Id,
                        CompletedAt = DateTime.UtcNow,
                        Duration = DateTime.UtcNow - startTime
                    };
                }

                return CreateFailureResponse(request, $"Failed to isolate device: {response.ReasonPhrase}", content, startTime);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error isolating device");
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        public async Task<XDRRemediationResponse> ReleaseDeviceAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            await SetAuthHeaderAsync(request.TenantId);

            var deviceId = request.Parameters["deviceId"]?.ToString() 
                ?? request.Parameters["machineId"]?.ToString();
            
            var comment = $"XDR Remediation Release - Incident: {request.IncidentId}";

            var payload = new { Comment = comment };

            try
            {
                var response = await _httpClient.PostAsync(
                    $"{_baseUrl}/api/machines/{deviceId}/unisolate",
                    new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var actionResponse = JsonSerializer.Deserialize<MDEMachineActionResponse>(content);
                    
                    return new XDRRemediationResponse
                    {
                        RequestId = request.RequestId,
                        TenantId = request.TenantId,
                        IncidentId = request.IncidentId,
                        Success = true,
                        Status = "Completed",
                        Message = $"Device {deviceId} released from isolation",
                        Details = new Dictionary<string, object>
                        {
                            ["deviceId"] = deviceId,
                            ["actionId"] = actionResponse?.Id ?? ""
                        },
                        ActionId = actionResponse?.Id,
                        CompletedAt = DateTime.UtcNow,
                        Duration = DateTime.UtcNow - startTime
                    };
                }

                return CreateFailureResponse(request, $"Failed to release device: {response.ReasonPhrase}", content, startTime);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error releasing device");
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        public async Task<XDRRemediationResponse> RunAntivirusScanAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            await SetAuthHeaderAsync(request.TenantId);

            var deviceId = request.Parameters["deviceId"]?.ToString();
            var scanType = request.Parameters.GetValueOrDefault("scanType", "Quick").ToString();
            var comment = $"XDR AV Scan - Type: {scanType} - Incident: {request.IncidentId}";

            var payload = new
            {
                Comment = comment,
                ScanType = scanType
            };

            try
            {
                var response = await _httpClient.PostAsync(
                    $"{_baseUrl}/api/machines/{deviceId}/runAntiVirusScan",
                    new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var actionResponse = JsonSerializer.Deserialize<MDEMachineActionResponse>(content);
                    
                    return new XDRRemediationResponse
                    {
                        RequestId = request.RequestId,
                        TenantId = request.TenantId,
                        IncidentId = request.IncidentId,
                        Success = true,
                        Status = "Completed",
                        Message = $"{scanType} antivirus scan initiated on device {deviceId}",
                        Details = new Dictionary<string, object>
                        {
                            ["deviceId"] = deviceId,
                            ["scanType"] = scanType,
                            ["actionId"] = actionResponse?.Id ?? ""
                        },
                        ActionId = actionResponse?.Id,
                        CompletedAt = DateTime.UtcNow,
                        Duration = DateTime.UtcNow - startTime
                    };
                }

                return CreateFailureResponse(request, $"Failed to start scan: {response.ReasonPhrase}", content, startTime);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running antivirus scan");
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        public async Task<XDRRemediationResponse> CollectInvestigationPackageAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            await SetAuthHeaderAsync(request.TenantId);

            var deviceId = request.Parameters["deviceId"]?.ToString();
            var comment = $"XDR Investigation Package - Incident: {request.IncidentId}";

            var payload = new { Comment = comment };

            try
            {
                var response = await _httpClient.PostAsync(
                    $"{_baseUrl}/api/machines/{deviceId}/collectInvestigationPackage",
                    new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var actionResponse = JsonSerializer.Deserialize<MDEMachineActionResponse>(content);
                    
                    return new XDRRemediationResponse
                    {
                        RequestId = request.RequestId,
                        TenantId = request.TenantId,
                        IncidentId = request.IncidentId,
                        Success = true,
                        Status = "Completed",
                        Message = $"Investigation package collection initiated for device {deviceId}",
                        Details = new Dictionary<string, object>
                        {
                            ["deviceId"] = deviceId,
                            ["actionId"] = actionResponse?.Id ?? ""
                        },
                        ActionId = actionResponse?.Id,
                        CompletedAt = DateTime.UtcNow,
                        Duration = DateTime.UtcNow - startTime
                    };
                }

                return CreateFailureResponse(request, $"Failed to collect package: {response.ReasonPhrase}", content, startTime);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting investigation package");
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        public async Task<XDRRemediationResponse> StopAndQuarantineFileAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            await SetAuthHeaderAsync(request.TenantId);

            var fileSha1 = request.Parameters["sha1"]?.ToString();
            var comment = $"XDR File Quarantine - Incident: {request.IncidentId}";

            var payload = new { Comment = comment };

            try
            {
                var response = await _httpClient.PostAsync(
                    $"{_baseUrl}/api/files/{fileSha1}/stopAndQuarantineFile",
                    new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var actionResponse = JsonSerializer.Deserialize<MDEMachineActionResponse>(content);
                    
                    return new XDRRemediationResponse
                    {
                        RequestId = request.RequestId,
                        TenantId = request.TenantId,
                        IncidentId = request.IncidentId,
                        Success = true,
                        Status = "Completed",
                        Message = $"File {fileSha1} stopped and quarantined across estate",
                        Details = new Dictionary<string, object>
                        {
                            ["fileSha1"] = fileSha1,
                            ["actionId"] = actionResponse?.Id ?? ""
                        },
                        ActionId = actionResponse?.Id,
                        CompletedAt = DateTime.UtcNow,
                        Duration = DateTime.UtcNow - startTime
                    };
                }

                return CreateFailureResponse(request, $"Failed to quarantine file: {response.ReasonPhrase}", content, startTime);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error quarantining file");
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        public async Task<XDRRemediationResponse> RestrictAppExecutionAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            await SetAuthHeaderAsync(request.TenantId);

            var deviceId = request.Parameters["deviceId"]?.ToString();
            var comment = $"XDR App Restriction - Incident: {request.IncidentId}";

            var payload = new { Comment = comment };

            try
            {
                var response = await _httpClient.PostAsync(
                    $"{_baseUrl}/api/machines/{deviceId}/restrictCodeExecution",
                    new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var actionResponse = JsonSerializer.Deserialize<MDEMachineActionResponse>(content);
                    
                    return new XDRRemediationResponse
                    {
                        RequestId = request.RequestId,
                        TenantId = request.TenantId,
                        IncidentId = request.IncidentId,
                        Success = true,
                        Status = "Completed",
                        Message = $"App execution restricted on device {deviceId}",
                        Details = new Dictionary<string, object>
                        {
                            ["deviceId"] = deviceId,
                            ["actionId"] = actionResponse?.Id ?? ""
                        },
                        ActionId = actionResponse?.Id,
                        CompletedAt = DateTime.UtcNow,
                        Duration = DateTime.UtcNow - startTime
                    };
                }

                return CreateFailureResponse(request, $"Failed to restrict apps: {response.ReasonPhrase}", content, startTime);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restricting app execution");
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        public async Task<XDRRemediationResponse> RemoveAppRestrictionAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            await SetAuthHeaderAsync(request.TenantId);

            var deviceId = request.Parameters["deviceId"]?.ToString();
            var comment = $"XDR Remove Restriction - Incident: {request.IncidentId}";

            var payload = new { Comment = comment };

            try
            {
                var response = await _httpClient.PostAsync(
                    $"{_baseUrl}/api/machines/{deviceId}/unrestrictCodeExecution",
                    new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var actionResponse = JsonSerializer.Deserialize<MDEMachineActionResponse>(content);
                    
                    return new XDRRemediationResponse
                    {
                        RequestId = request.RequestId,
                        TenantId = request.TenantId,
                        IncidentId = request.IncidentId,
                        Success = true,
                        Status = "Completed",
                        Message = $"App restrictions removed from device {deviceId}",
                        Details = new Dictionary<string, object>
                        {
                            ["deviceId"] = deviceId,
                            ["actionId"] = actionResponse?.Id ?? ""
                        },
                        ActionId = actionResponse?.Id,
                        CompletedAt = DateTime.UtcNow,
                        Duration = DateTime.UtcNow - startTime
                    };
                }

                return CreateFailureResponse(request, $"Failed to remove restriction: {response.ReasonPhrase}", content, startTime);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing app restriction");
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        public async Task<XDRRemediationResponse> StartAutomatedInvestigationAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            await SetAuthHeaderAsync(request.TenantId);

            var alertId = request.Parameters["alertId"]?.ToString();
            var comment = $"XDR Automated Investigation - Incident: {request.IncidentId}";

            var payload = new { Comment = comment };

            try
            {
                var response = await _httpClient.PostAsync(
                    $"{_baseUrl}/api/alerts/{alertId}/startInvestigation",
                    new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    return new XDRRemediationResponse
                    {
                        RequestId = request.RequestId,
                        TenantId = request.TenantId,
                        IncidentId = request.IncidentId,
                        Success = true,
                        Status = "Completed",
                        Message = $"Automated investigation started for alert {alertId}",
                        Details = new Dictionary<string, object>
                        {
                            ["alertId"] = alertId
                        },
                        CompletedAt = DateTime.UtcNow,
                        Duration = DateTime.UtcNow - startTime
                    };
                }

                return CreateFailureResponse(request, $"Failed to start investigation: {response.ReasonPhrase}", content, startTime);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting investigation");
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        public async Task<XDRRemediationResponse> SubmitIndicatorAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            await SetAuthHeaderAsync(request.TenantId);

            var indicatorValue = request.Parameters["indicatorValue"]?.ToString();
            var indicatorType = request.Parameters["indicatorType"]?.ToString() ?? "FileSha1";
            var action = request.Parameters.GetValueOrDefault("indicatorAction", "Block").ToString();
            var title = request.Parameters.GetValueOrDefault("title", $"XDR Indicator - {request.IncidentId}").ToString();
            var description = request.Parameters.GetValueOrDefault("description", request.Justification).ToString();

            var payload = new
            {
                indicatorValue,
                indicatorType,
                action,
                title,
                description,
                severity = "High",
                recommendedActions = $"Blocked by XDR - Incident: {request.IncidentId}"
            };

            try
            {
                var response = await _httpClient.PostAsync(
                    $"{_baseUrl}/api/indicators",
                    new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    return new XDRRemediationResponse
                    {
                        RequestId = request.RequestId,
                        TenantId = request.TenantId,
                        IncidentId = request.IncidentId,
                        Success = true,
                        Status = "Completed",
                        Message = $"Indicator submitted: {indicatorType} - {indicatorValue}",
                        Details = new Dictionary<string, object>
                        {
                            ["indicatorValue"] = indicatorValue,
                            ["indicatorType"] = indicatorType,
                            ["action"] = action
                        },
                        CompletedAt = DateTime.UtcNow,
                        Duration = DateTime.UtcNow - startTime
                    };
                }

                return CreateFailureResponse(request, $"Failed to submit indicator: {response.ReasonPhrase}", content, startTime);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting indicator");
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        private XDRRemediationResponse CreateFailureResponse(
            XDRRemediationRequest request, 
            string message, 
            string apiResponse, 
            DateTime startTime)
        {
            return new XDRRemediationResponse
            {
                RequestId = request.RequestId,
                TenantId = request.TenantId,
                IncidentId = request.IncidentId,
                Success = false,
                Status = "Failed",
                Message = message,
                Details = new Dictionary<string, object>
                {
                    ["apiResponse"] = apiResponse
                },
                Errors = new List<string> { message },
                CompletedAt = DateTime.UtcNow,
                Duration = DateTime.UtcNow - startTime
            };
        }

        private XDRRemediationResponse CreateExceptionResponse(
            XDRRemediationRequest request, 
            Exception ex, 
            DateTime startTime)
        {
            return new XDRRemediationResponse
            {
                RequestId = request.RequestId,
                TenantId = request.TenantId,
                IncidentId = request.IncidentId,
                Success = false,
                Status = "Exception",
                Message = $"Exception occurred: {ex.Message}",
                Errors = new List<string> { ex.ToString() },
                CompletedAt = DateTime.UtcNow,
                Duration = DateTime.UtcNow - startTime
            };
        }

        // ==================== Enhanced MDE Actions (2 NEW actions) ====================
        // NOTE: CollectInvestigationPackageAsync already exists above
        
        public async Task<XDRRemediationResponse> InitiateAutomatedInvestigationAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            await SetAuthHeaderAsync(request.TenantId);

            var machineId = request.Parameters["machineId"]?.ToString();
            var comment = request.Parameters["comment"]?.ToString() ?? "Initiating automated investigation via SentryXDR";

            try
            {
                var actionBody = new { Comment = comment };
                var response = await _httpClient.PostAsync(
                    $"{_baseUrl}/api/machines/{machineId}/startInvestigation",
                    new StringContent(JsonSerializer.Serialize(actionBody), Encoding.UTF8, "application/json"));

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var investigation = JsonSerializer.Deserialize<JsonElement>(content);

                    return new XDRRemediationResponse
                    {
                        RequestId = request.RequestId,
                        TenantId = request.TenantId,
                        IncidentId = request.IncidentId,
                        Success = true,
                        Status = "Completed",
                        Message = "Automated investigation initiated",
                        Details = new Dictionary<string, object>
                        {
                            ["investigationId"] = investigation.GetProperty("id").GetString()!,
                            ["machineId"] = machineId!,
                            ["status"] = investigation.GetProperty("status").GetString()!
                        },
                        CompletedAt = DateTime.UtcNow,
                        Duration = DateTime.UtcNow - startTime
                    };
                }

                return new XDRRemediationResponse
                {
                    RequestId = request.RequestId,
                    TenantId = request.TenantId,
                    IncidentId = request.IncidentId,
                    Success = false,
                    Status = "Failed",
                    Message = $"Failed to start investigation: {response.ReasonPhrase}",
                    Errors = new List<string> { await response.Content.ReadAsStringAsync() },
                    CompletedAt = DateTime.UtcNow,
                    Duration = DateTime.UtcNow - startTime
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initiating automated investigation");
                return new XDRRemediationResponse
                {
                    RequestId = request.RequestId,
                    TenantId = request.TenantId,
                    IncidentId = request.IncidentId,
                    Success = false,
                    Status = "Exception",
                    Message = $"Exception occurred: {ex.Message}",
                    Errors = new List<string> { ex.ToString() },
                    CompletedAt = DateTime.UtcNow,
                    Duration = DateTime.UtcNow - startTime
                };
            }
        }

        public async Task<XDRRemediationResponse> CancelMachineActionAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            await SetAuthHeaderAsync(request.TenantId);

            var actionId = request.Parameters["actionId"]?.ToString();
            var comment = request.Parameters["comment"]?.ToString() ?? "Cancelling action via SentryXDR";

            try
            {
                var actionBody = new { Comment = comment };
                var response = await _httpClient.PostAsync(
                    $"{_baseUrl}/api/machineactions/{actionId}/cancel",
                    new StringContent(JsonSerializer.Serialize(actionBody), Encoding.UTF8, "application/json"));

                if (response.IsSuccessStatusCode)
                {
                    return new XDRRemediationResponse
                    {
                        RequestId = request.RequestId,
                        TenantId = request.TenantId,
                        IncidentId = request.IncidentId,
                        Success = true,
                        Status = "Completed",
                        Message = "Machine action cancelled successfully",
                        Details = new Dictionary<string, object>
                        {
                            ["actionId"] = actionId!,
                            ["cancelledAt"] = DateTime.UtcNow
                        },
                        CompletedAt = DateTime.UtcNow,
                        Duration = DateTime.UtcNow - startTime
                    };
                }

                return new XDRRemediationResponse
                {
                    RequestId = request.RequestId,
                    TenantId = request.TenantId,
                    IncidentId = request.IncidentId,
                    Success = false,
                    Status = "Failed",
                    Message = $"Failed to cancel action: {response.ReasonPhrase}",
                    Errors = new List<string> { await response.Content.ReadAsStringAsync() },
                    CompletedAt = DateTime.UtcNow,
                    Duration = DateTime.UtcNow - startTime
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling machine action");
                return new XDRRemediationResponse
                {
                    RequestId = request.RequestId,
                    TenantId = request.TenantId,
                    IncidentId = request.IncidentId,
                    Success = false,
                    Status = "Exception",
                    Message = $"Exception occurred: {ex.Message}",
                    Errors = new List<string> { ex.ToString() },
                    CompletedAt = DateTime.UtcNow,
                    Duration = DateTime.UtcNow - startTime
                };
            }
        }
    }
}
