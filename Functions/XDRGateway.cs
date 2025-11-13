using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using SentryXDR.Models;
using SentryXDR.Services;
using System.Net;
using System.Text.Json;

namespace SentryXDR.Functions
{
    /// <summary>
    /// XDR Gateway - Entry point for all remediation requests
    /// Accepts multi-tenant requests and initiates orchestration
    /// </summary>
    public class XDRGateway
    {
        private readonly ILogger<XDRGateway> _logger;
        private readonly IRemediationValidator _validator;

        public XDRGateway(
            ILogger<XDRGateway> logger,
            IRemediationValidator validator)
        {
            _logger = logger;
            _validator = validator;
        }

        /// <summary>
        /// POST /api/xdr/remediate
        /// Initiate XDR remediation action
        /// </summary>
        [Function("XDRGateway")]
        public async Task<HttpResponseData> RunAsync(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "xdr/remediate")] 
            HttpRequestData req,
            [DurableClient] DurableTaskClient durableClient)
        {
            _logger.LogInformation($"XDR Gateway received request at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");

            try
            {
                // Parse request
                var requestBody = await req.ReadAsStringAsync();
                if (string.IsNullOrEmpty(requestBody))
                {
                    return await CreateErrorResponseAsync(req, "Request body is empty", HttpStatusCode.BadRequest);
                }

                var remediationRequest = JsonSerializer.Deserialize<XDRRemediationRequest>(
                    requestBody, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (remediationRequest == null)
                {
                    return await CreateErrorResponseAsync(req, "Invalid request format", HttpStatusCode.BadRequest);
                }

                // Log request details
                _logger.LogInformation(
                    "Remediation request - Tenant: {TenantId}, Incident: {IncidentId}, Platform: {Platform}, Action: {Action}",
                    remediationRequest.TenantId,
                    remediationRequest.IncidentId,
                    remediationRequest.Platform,
                    remediationRequest.Action);

                // Validate request
                var validationResult = await _validator.ValidateRequestAsync(remediationRequest);
                if (!validationResult.IsValid)
                {
                    _logger.LogWarning("Request validation failed: {Message}", validationResult.Message);
                    return await CreateErrorResponseAsync(req, validationResult.Message, HttpStatusCode.BadRequest);
                }

                // Start orchestration
                string instanceId = await durableClient.ScheduleNewOrchestrationInstanceAsync(
                    "XDROrchestrator",
                    remediationRequest);

                _logger.LogInformation(
                    "Started orchestration {InstanceId} for tenant {TenantId}, incident {IncidentId}",
                    instanceId,
                    remediationRequest.TenantId,
                    remediationRequest.IncidentId);

                // Return accepted response with orchestration details
                var response = req.CreateResponse(HttpStatusCode.Accepted);
                response.Headers.Add("X-Orchestration-Id", instanceId);
                response.Headers.Add("Location", $"/api/xdr/status/{instanceId}");
                
                await response.WriteAsJsonAsync(new
                {
                    orchestrationId = instanceId,
                    requestId = remediationRequest.RequestId,
                    tenantId = remediationRequest.TenantId,
                    incidentId = remediationRequest.IncidentId,
                    platform = remediationRequest.Platform.ToString(),
                    action = remediationRequest.Action.ToString(),
                    status = "Accepted",
                    message = "Remediation request accepted and processing",
                    statusUrl = $"/api/xdr/status/{instanceId}",
                    timestamp = DateTime.UtcNow
                });

                return response;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON deserialization error");
                return await CreateErrorResponseAsync(req, "Invalid JSON format", HttpStatusCode.BadRequest);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing request");
                return await CreateErrorResponseAsync(req, "Internal server error", HttpStatusCode.InternalServerError);
            }
        }

        /// <summary>
        /// GET /api/xdr/status/{instanceId}
        /// Get orchestration status
        /// </summary>
        [Function("GetOrchestrationStatus")]
        public async Task<HttpResponseData> GetStatusAsync(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "xdr/status/{instanceId}")] 
            HttpRequestData req,
            string instanceId,
            [DurableClient] DurableTaskClient durableClient)
        {
            _logger.LogInformation("Checking status for orchestration {InstanceId}", instanceId);

            try
            {
                var metadata = await durableClient.GetInstanceAsync(instanceId);
                
                if (metadata == null)
                {
                    return await CreateErrorResponseAsync(req, "Orchestration not found", HttpStatusCode.NotFound);
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    instanceId = metadata.InstanceId,
                    name = metadata.Name,
                    runtimeStatus = metadata.RuntimeStatus.ToString(),
                    createdAt = metadata.CreatedAt,
                    lastUpdatedAt = metadata.LastUpdatedAt,
                    input = metadata.ReadInputAs<XDRRemediationRequest>(),
                    output = metadata.RuntimeStatus == Microsoft.DurableTask.Client.OrchestrationRuntimeStatus.Completed
                        ? metadata.ReadOutputAs<XDRRemediationResponse>()
                        : null
                });

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving orchestration status");
                return await CreateErrorResponseAsync(req, "Error retrieving status", HttpStatusCode.InternalServerError);
            }
        }

        /// <summary>
        /// GET /api/xdr/health
        /// Health check endpoint
        /// </summary>
        [Function("HealthCheck")]
        public async Task<HttpResponseData> HealthCheckAsync(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "xdr/health")] 
            HttpRequestData req)
        {
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                status = "Healthy",
                service = "SentryXDR",
                version = "1.0.0",
                timestamp = DateTime.UtcNow
            });

            return response;
        }

        private async Task<HttpResponseData> CreateErrorResponseAsync(
            HttpRequestData req, 
            string message, 
            HttpStatusCode statusCode)
        {
            var response = req.CreateResponse(statusCode);
            await response.WriteAsJsonAsync(new
            {
                error = message,
                statusCode = (int)statusCode,
                timestamp = DateTime.UtcNow
            });

            return response;
        }
    }
}
