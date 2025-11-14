using Microsoft.Extensions.Logging;
using SentryXDR.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SentryXDR.Services.Workers
{
    /// <summary>
    /// Base class for all worker services providing common functionality
    /// Implements shared response handling, error logging, and HTTP operations
    /// </summary>
    public abstract class BaseWorkerService
    {
        protected readonly ILogger Logger;
        protected readonly HttpClient HttpClient;

        protected BaseWorkerService(ILogger logger, HttpClient httpClient)
        {
            Logger = logger;
            HttpClient = httpClient;
        }

        // ==================== Response Helpers ====================

        /// <summary>
        /// Creates a successful remediation response with optional details
        /// </summary>
        protected XDRRemediationResponse CreateSuccessResponse(
            XDRRemediationRequest request, 
            string message, 
            Dictionary<string, object>? details = null,
            DateTime? startTime = null)
        {
            var now = DateTime.UtcNow;
            return new XDRRemediationResponse
            {
                RequestId = request.RequestId,
                TenantId = request.TenantId,
                IncidentId = request.IncidentId,
                Success = true,
                Status = "Completed",
                Message = message,
                Details = details ?? new Dictionary<string, object>(),
                CompletedAt = now,
                Duration = startTime.HasValue ? now - startTime.Value : TimeSpan.Zero
            };
        }

        /// <summary>
        /// Creates a failure response with error message
        /// </summary>
        protected XDRRemediationResponse CreateFailureResponse(
            XDRRemediationRequest request, 
            string error,
            DateTime? startTime = null)
        {
            var now = DateTime.UtcNow;
            Logger.LogWarning("Operation failed for request {RequestId}: {Error}", request.RequestId, error);
            
            return new XDRRemediationResponse
            {
                RequestId = request.RequestId,
                TenantId = request.TenantId,
                IncidentId = request.IncidentId,
                Success = false,
                Status = "Failed",
                Message = error,
                Errors = new List<string> { error },
                CompletedAt = now,
                Duration = startTime.HasValue ? now - startTime.Value : TimeSpan.Zero
            };
        }

        /// <summary>
        /// Creates an exception response with full exception details
        /// </summary>
        protected XDRRemediationResponse CreateExceptionResponse(
            XDRRemediationRequest request, 
            Exception ex,
            DateTime? startTime = null)
        {
            var now = DateTime.UtcNow;
            Logger.LogError(ex, "Exception in {Action} for tenant {TenantId}", 
                request.Action, request.TenantId);
            
            return new XDRRemediationResponse
            {
                RequestId = request.RequestId,
                TenantId = request.TenantId,
                IncidentId = request.IncidentId,
                Success = false,
                Status = "Exception",
                Message = $"Operation failed with exception: {ex.Message}",
                Errors = new List<string> 
                { 
                    ex.Message,
                    ex.StackTrace ?? string.Empty
                },
                CompletedAt = now,
                Duration = startTime.HasValue ? now - startTime.Value : TimeSpan.Zero
            };
        }

        // ==================== Parameter Extraction Helpers ====================

        /// <summary>
        /// Gets required parameter from request, returns failure response if missing
        /// </summary>
        protected string? GetRequiredParameter(
            XDRRemediationRequest request, 
            string parameterName, 
            out XDRRemediationResponse? failureResponse)
        {
            failureResponse = null;
            var value = request.Parameters.GetValueOrDefault(parameterName)?.ToString();
            
            if (string.IsNullOrEmpty(value))
            {
                failureResponse = CreateFailureResponse(request, 
                    $"Missing required parameter: {parameterName}");
                return null;
            }
            
            return value;
        }

        /// <summary>
        /// Gets optional parameter with default value
        /// </summary>
        protected string GetOptionalParameter(
            XDRRemediationRequest request, 
            string parameterName, 
            string defaultValue = "")
        {
            return request.Parameters.GetValueOrDefault(parameterName)?.ToString() ?? defaultValue;
        }

        /// <summary>
        /// Validates multiple required parameters at once
        /// </summary>
        protected bool ValidateRequiredParameters(
            XDRRemediationRequest request,
            out XDRRemediationResponse? failureResponse,
            params string[] parameterNames)
        {
            failureResponse = null;
            var missingParams = new List<string>();

            foreach (var paramName in parameterNames)
            {
                var value = request.Parameters.GetValueOrDefault(paramName)?.ToString();
                if (string.IsNullOrEmpty(value))
                {
                    missingParams.Add(paramName);
                }
            }

            if (missingParams.Any())
            {
                failureResponse = CreateFailureResponse(request,
                    $"Missing required parameters: {string.Join(", ", missingParams)}");
                return false;
            }

            return true;
        }

        // ==================== HTTP Helpers ====================

        /// <summary>
        /// Posts JSON content to API and returns typed response
        /// </summary>
        protected async Task<(bool Success, T? Data, string? Error)> PostJsonAsync<T>(
            string url, 
            object requestBody,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await HttpClient.PostAsync(url, content, cancellationToken);
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var data = JsonSerializer.Deserialize<T>(responseBody);
                    return (true, data, null);
                }
                else
                {
                    Logger.LogWarning("API call failed: {StatusCode} - {Response}", 
                        response.StatusCode, responseBody);
                    return (false, default, $"API returned {response.StatusCode}: {responseBody}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "HTTP POST failed to {Url}", url);
                return (false, default, ex.Message);
            }
        }

        /// <summary>
        /// Gets data from API and returns typed response
        /// </summary>
        protected async Task<(bool Success, T? Data, string? Error)> GetJsonAsync<T>(
            string url,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await HttpClient.GetAsync(url, cancellationToken);
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var data = JsonSerializer.Deserialize<T>(responseBody);
                    return (true, data, null);
                }
                else
                {
                    Logger.LogWarning("API GET failed: {StatusCode} - {Response}", 
                        response.StatusCode, responseBody);
                    return (false, default, $"API returned {response.StatusCode}: {responseBody}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "HTTP GET failed from {Url}", url);
                return (false, default, ex.Message);
            }
        }

        /// <summary>
        /// Deletes resource and returns success status
        /// </summary>
        protected async Task<(bool Success, string? Error)> DeleteAsync(
            string url,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await HttpClient.DeleteAsync(url, cancellationToken);
                
                if (response.IsSuccessStatusCode)
                {
                    return (true, null);
                }
                else
                {
                    var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    Logger.LogWarning("API DELETE failed: {StatusCode} - {Response}", 
                        response.StatusCode, responseBody);
                    return (false, $"API returned {response.StatusCode}: {responseBody}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "HTTP DELETE failed for {Url}", url);
                return (false, ex.Message);
            }
        }

        /// <summary>
        /// Patches resource with JSON content
        /// </summary>
        protected async Task<(bool Success, T? Data, string? Error)> PatchJsonAsync<T>(
            string url,
            object requestBody,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(HttpMethod.Patch, url) { Content = content };
                
                var response = await HttpClient.SendAsync(request, cancellationToken);
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var data = JsonSerializer.Deserialize<T>(responseBody);
                    return (true, data, null);
                }
                else
                {
                    Logger.LogWarning("API PATCH failed: {StatusCode} - {Response}", 
                        response.StatusCode, responseBody);
                    return (false, default, $"API returned {response.StatusCode}: {responseBody}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "HTTP PATCH failed to {Url}", url);
                return (false, default, ex.Message);
            }
        }

        // ==================== Retry Logic ====================

        /// <summary>
        /// Executes operation with exponential backoff retry
        /// </summary>
        protected async Task<T> ExecuteWithRetryAsync<T>(
            Func<Task<T>> operation,
            int maxRetries = 3,
            int initialDelayMs = 1000,
            CancellationToken cancellationToken = default)
        {
            var retryCount = 0;
            var delay = initialDelayMs;

            while (true)
            {
                try
                {
                    return await operation();
                }
                catch (Exception ex) when (retryCount < maxRetries && IsTransientError(ex))
                {
                    retryCount++;
                    Logger.LogWarning(ex, "Operation failed (attempt {Attempt}/{MaxRetries}), retrying in {Delay}ms", 
                        retryCount, maxRetries, delay);
                    
                    await Task.Delay(delay, cancellationToken);
                    delay *= 2; // Exponential backoff
                }
            }
        }

        /// <summary>
        /// Determines if error is transient and should be retried
        /// </summary>
        protected virtual bool IsTransientError(Exception ex)
        {
            return ex is HttpRequestException ||
                   ex is TaskCanceledException ||
                   (ex is JsonException && ex.Message.Contains("timeout"));
        }

        // ==================== Authentication Helpers ====================

        /// <summary>
        /// Sets bearer token authentication header
        /// </summary>
        protected void SetBearerToken(string token)
        {
            HttpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", token);
        }

        /// <summary>
        /// Sets custom authentication header (e.g., for MCAS API tokens)
        /// </summary>
        protected void SetAuthHeader(string scheme, string token)
        {
            HttpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue(scheme, token);
        }

        // ==================== Logging Helpers ====================

        /// <summary>
        /// Logs operation start with structured data
        /// </summary>
        protected void LogOperationStart(XDRRemediationRequest request, string operation)
        {
            Logger.LogInformation(
                "{Platform} {Action}: Starting {Operation} for tenant {TenantId}, incident {IncidentId}",
                request.Platform, request.Action, operation, request.TenantId, request.IncidentId);
        }

        /// <summary>
        /// Logs operation completion with duration
        /// </summary>
        protected void LogOperationComplete(XDRRemediationRequest request, string operation, TimeSpan duration, bool success)
        {
            if (success)
            {
                Logger.LogInformation(
                    "{Platform} {Action}: Completed {Operation} in {Duration}ms",
                    request.Platform, request.Action, operation, duration.TotalMilliseconds);
            }
            else
            {
                Logger.LogWarning(
                    "{Platform} {Action}: Failed {Operation} after {Duration}ms",
                    request.Platform, request.Action, operation, duration.TotalMilliseconds);
            }
        }
    }
}
