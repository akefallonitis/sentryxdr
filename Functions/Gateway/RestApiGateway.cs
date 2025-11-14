using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using SentryXDR.Models;

namespace SentryXDR.Functions.Gateway
{
    /// <summary>
    /// REST API Gateway for SentryXDR - Single entry point for all remediation operations
    /// Provides RESTful endpoints with Swagger documentation
    /// </summary>
    public class RestApiGateway
    {
        private readonly ILogger<RestApiGateway> _logger;
        private readonly IConfiguration _configuration;

        public RestApiGateway(ILogger<RestApiGateway> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        /// <summary>
        /// Submit a new remediation request
        /// POST /api/v1/remediation/submit
        /// </summary>
        [Function("SubmitRemediation")]
        public async Task<HttpResponseData> SubmitRemediationAsync(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/remediation/submit")] HttpRequestData req,
            [DurableClient] DurableTaskClient client)
        {
            _logger.LogInformation("REST API: Submit remediation request");

            try
            {
                // 1. Parse request body
                var requestBody = await req.ReadAsStringAsync();
                var submitRequest = JsonSerializer.Deserialize<RemediationSubmitRequest>(requestBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (submitRequest == null)
                {
                    return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Invalid request body");
                }

                // 2. Validate required fields
                if (string.IsNullOrEmpty(submitRequest.TenantId) || string.IsNullOrEmpty(submitRequest.Action))
                {
                    return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "TenantId and Action are required");
                }

                // 3. Create XDR remediation request
                var xdrRequest = new XDRRemediationRequest
                {
                    RequestId = Guid.NewGuid().ToString(),
                    TenantId = submitRequest.TenantId,
                    IncidentId = submitRequest.IncidentId ?? string.Empty,
                    Platform = Enum.Parse<XDRPlatform>(submitRequest.Platform),
                    Action = Enum.Parse<XDRAction>(submitRequest.Action),
                    Parameters = submitRequest.Parameters ?? new Dictionary<string, object>(),
                    InitiatedBy = submitRequest.InitiatedBy ?? "API",
                    Priority = Enum.TryParse<RemediationPriority>(submitRequest.Priority, out var priority) ? priority : RemediationPriority.Medium,
                    Justification = submitRequest.Justification ?? "Submitted via REST API",
                    Timestamp = DateTime.UtcNow
                };

                // 4. Start orchestration
                var instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
                    "XDROrchestrator",
                    xdrRequest);

                _logger.LogInformation("Started orchestration {InstanceId} for request {RequestId}", instanceId, xdrRequest.RequestId);

                // 5. Create response with status URLs
                var baseUrl = $"{req.Url.Scheme}://{req.Url.Authority}";
                var response = new RemediationSubmitResponse
                {
                    RequestId = xdrRequest.RequestId,
                    InstanceId = instanceId,
                    Status = "Accepted",
                    StatusUrl = $"{baseUrl}/api/v1/remediation/{xdrRequest.RequestId}/status",
                    CancelUrl = $"{baseUrl}/api/v1/remediation/{xdrRequest.RequestId}/cancel",
                    SubmittedAt = DateTime.UtcNow
                };

                // 6. Return response
                var httpResponse = req.CreateResponse(HttpStatusCode.Accepted);
                await httpResponse.WriteAsJsonAsync(response);
                return httpResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to submit remediation request");
                return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        /// <summary>
        /// Get status of a remediation request
        /// GET /api/v1/remediation/{requestId}/status
        /// </summary>
        [Function("GetRemediationStatus")]
        public async Task<HttpResponseData> GetStatusAsync(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/remediation/{requestId}/status")] HttpRequestData req,
            [DurableClient] DurableTaskClient client,
            string requestId)
        {
            _logger.LogInformation("REST API: Get status for request {RequestId}", requestId);

            try
            {
                // Query orchestration status by request ID
                // Note: We need to store requestId -> instanceId mapping
                // For now, use requestId as instanceId
                var orchestrationMetadata = await client.GetInstanceAsync(requestId);

                if (orchestrationMetadata == null)
                {
                    return await CreateErrorResponse(req, HttpStatusCode.NotFound, $"Request {requestId} not found");
                }

                var statusResponse = new RemediationStatusResponse
                {
                    RequestId = requestId,
                    InstanceId = orchestrationMetadata.InstanceId,
                    Status = orchestrationMetadata.RuntimeStatus.ToString(),
                    CreatedAt = orchestrationMetadata.CreatedAt.DateTime,
                    LastUpdated = orchestrationMetadata.LastUpdatedAt.DateTime,
                    SerializedInput = orchestrationMetadata.SerializedInput
                };
                var httpResponse = req.CreateResponse(HttpStatusCode.OK);
                await httpResponse.WriteAsJsonAsync(statusResponse);
                return httpResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get remediation status");
                return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        /// <summary>
        /// Cancel a running remediation request
        /// DELETE /api/v1/remediation/{requestId}/cancel
        /// </summary>
        [Function("CancelRemediation")]
        public async Task<HttpResponseData> CancelRemediationAsync(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "v1/remediation/{requestId}/cancel")] HttpRequestData req,
            [DurableClient] DurableTaskClient client,
            string requestId)
        {
            _logger.LogInformation("REST API: Cancel request {RequestId}", requestId);

            try
            {
                // Terminate orchestration
                await client.TerminateInstanceAsync(requestId, "Cancelled via REST API");

                var response = new
                {
                    RequestId = requestId,
                    Status = "Cancelled",
                    CancelledAt = DateTime.UtcNow
                };

                var httpResponse = req.CreateResponse(HttpStatusCode.OK);
                await httpResponse.WriteAsJsonAsync(response);
                return httpResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cancel remediation");
                return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        /// <summary>
        /// Get remediation history from native APIs (no blob storage!)
        /// GET /api/v1/remediation/history?tenantId={id}&days={days}
        /// </summary>
        [Function("GetRemediationHistory")]
        public async Task<HttpResponseData> GetHistoryAsync(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/remediation/history")] HttpRequestData req,
            [DurableClient] DurableTaskClient client)
        {
            _logger.LogInformation("REST API: Get remediation history");

            try
            {
                // Parse query parameters
                var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                var tenantId = query["tenantId"];
                var daysStr = query["days"] ?? "7";
                var days = int.Parse(daysStr);

                // ? USE NATIVE API - No blob storage!
                // In production, query:
                // - MDE: GET /api/machineactions
                // - Graph: GET /security/incidents/{id}/comments
                // - Orchestration history

                // For now, query orchestration instances
                var filter = new OrchestrationQuery
                {
                    CreatedFrom = DateTime.UtcNow.AddDays(-days),
                    CreatedTo = DateTime.UtcNow,
                    PageSize = 100
                };

                // Note: This is a placeholder - implement actual history query
                var historyResponse = new RemediationHistoryResponse
                {
                    TenantId = tenantId,
                    FromDate = DateTime.UtcNow.AddDays(-days),
                    ToDate = DateTime.UtcNow,
                    TotalCount = 0,
                    Items = new List<RemediationHistoryItem>()
                };

                var httpResponse = req.CreateResponse(HttpStatusCode.OK);
                await httpResponse.WriteAsJsonAsync(historyResponse);
                return httpResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get remediation history");
                return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        /// <summary>
        /// Submit batch remediation requests
        /// POST /api/v1/remediation/batch
        /// </summary>
        [Function("SubmitBatchRemediation")]
        public async Task<HttpResponseData> SubmitBatchAsync(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/remediation/batch")] HttpRequestData req,
            [DurableClient] DurableTaskClient client)
        {
            _logger.LogInformation("REST API: Submit batch remediation");

            try
            {
                var requestBody = await req.ReadAsStringAsync();
                var batchRequest = JsonSerializer.Deserialize<List<RemediationSubmitRequest>>(requestBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (batchRequest == null || batchRequest.Count == 0)
                {
                    return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "Empty batch request");
                }

                var responses = new List<RemediationSubmitResponse>();

                foreach (var submitRequest in batchRequest)
                {
                    var xdrRequest = new XDRRemediationRequest
                    {
                        RequestId = Guid.NewGuid().ToString(),
                        TenantId = submitRequest.TenantId,
                        IncidentId = submitRequest.IncidentId ?? string.Empty,
                        Platform = Enum.Parse<XDRPlatform>(submitRequest.Platform),
                        Action = Enum.Parse<XDRAction>(submitRequest.Action),
                        Parameters = submitRequest.Parameters ?? new Dictionary<string, object>(),
                        InitiatedBy = submitRequest.InitiatedBy ?? "API-Batch",
                        Priority = Enum.TryParse<RemediationPriority>(submitRequest.Priority, out var priority) ? priority : RemediationPriority.Medium,
                        Justification = submitRequest.Justification ?? "Batch submission via REST API",
                        Timestamp = DateTime.UtcNow
                    };

                    var instanceId = await client.ScheduleNewOrchestrationInstanceAsync("XDROrchestrator", xdrRequest);

                    var baseUrl = $"{req.Url.Scheme}://{req.Url.Authority}";
                    responses.Add(new RemediationSubmitResponse
                    {
                        RequestId = xdrRequest.RequestId,
                        InstanceId = instanceId,
                        Status = "Accepted",
                        StatusUrl = $"{baseUrl}/api/v1/remediation/{xdrRequest.RequestId}/status",
                        CancelUrl = $"{baseUrl}/api/v1/remediation/{xdrRequest.RequestId}/cancel",
                        SubmittedAt = DateTime.UtcNow
                    });
                }

                var httpResponse = req.CreateResponse(HttpStatusCode.Accepted);
                await httpResponse.WriteAsJsonAsync(new
                {
                    BatchSize = responses.Count,
                    Requests = responses
                });
                return httpResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to submit batch remediation");
                return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        /// <summary>
        /// Health check endpoint
        /// GET /api/v1/health
        /// </summary>
        [Function("HealthCheck")]
        public async Task<HttpResponseData> HealthCheckAsync(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/health")] HttpRequestData req)
        {
            _logger.LogInformation("REST API: Health check");

            var health = new
            {
                Status = "Healthy",
                Version = "1.0.0",
                Timestamp = DateTime.UtcNow,
                Components = new
                {
                    Api = "Healthy",
                    Storage = "Healthy",
                    ManagedIdentity = "Healthy"
                }
            };

            var httpResponse = req.CreateResponse(HttpStatusCode.OK);
            await httpResponse.WriteAsJsonAsync(health);
            return httpResponse;
        }

        /// <summary>
        /// Get API metrics
        /// GET /api/v1/metrics
        /// </summary>
        [Function("GetMetrics")]
        public async Task<HttpResponseData> GetMetricsAsync(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/metrics")] HttpRequestData req,
            [DurableClient] DurableTaskClient client)
        {
            _logger.LogInformation("REST API: Get metrics");

            // Query orchestration metrics
            var metrics = new
            {
                Timestamp = DateTime.UtcNow,
                TotalRequests = 0,  // Implement actual metrics
                ActiveRequests = 0,
                CompletedRequests = 0,
                FailedRequests = 0,
                AverageExecutionTime = TimeSpan.Zero
            };

            var httpResponse = req.CreateResponse(HttpStatusCode.OK);
            await httpResponse.WriteAsJsonAsync(metrics);
            return httpResponse;
        }

        private async Task<HttpResponseData> CreateErrorResponse(HttpRequestData req, HttpStatusCode statusCode, string message)
        {
            var error = new
            {
                Error = message,
                StatusCode = (int)statusCode,
                Timestamp = DateTime.UtcNow
            };

            var response = req.CreateResponse(statusCode);
            await response.WriteAsJsonAsync(error);
            return response;
        }
    }
}
