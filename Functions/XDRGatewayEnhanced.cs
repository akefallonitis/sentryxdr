using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using SentryXDR.Models;

namespace SentryXDR.Functions
{
    /// <summary>
    /// Enhanced XDR Gateway with Batch Processing Support
    /// Supports single, batch, and multi-tenant remediation requests
    /// </summary>
    public class XDRGatewayEnhanced
    {
        private readonly ILogger<XDRGatewayEnhanced> _logger;

        public XDRGatewayEnhanced(ILogger<XDRGatewayEnhanced> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Batch remediation endpoint - multiple targets, single action
        /// POST /api/xdr/batch-remediate
        /// </summary>
        [Function("BatchRemediateHTTP")]
        public async Task<HttpResponseData> BatchRemediateAsync(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "xdr/batch-remediate")] HttpRequestData req,
            [DurableClient] DurableTaskClient client)
        {
            _logger.LogInformation("Batch remediation request received");

            try
            {
                var batchRequest = await JsonSerializer.DeserializeAsync<BatchRemediationRequest>(
                    req.Body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (batchRequest == null || batchRequest.Targets == null || !batchRequest.Targets.Any())
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteAsJsonAsync(new { error = "Invalid batch request or no targets specified" });
                    return badRequest;
                }

                // Create individual requests for each target
                var requests = batchRequest.Targets.Select(target => new XDRRemediationRequest
                {
                    RequestId = Guid.NewGuid().ToString(),
                    TenantId = batchRequest.TenantId,
                    IncidentId = batchRequest.IncidentId,
                    Platform = batchRequest.Platform,
                    Action = batchRequest.Action,
                    Parameters = target,
                    InitiatedBy = batchRequest.InitiatedBy,
                    Priority = batchRequest.Priority,
                    Justification = batchRequest.Justification
                }).ToList();

                // Start orchestrations
                var orchestrationIds = new List<string>();
                foreach (var request in requests)
                {
                    var instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
                        "DefenderXDROrchestrator",
                        request);
                    orchestrationIds.Add(instanceId);
                }

                var response = req.CreateResponse(HttpStatusCode.Accepted);
                await response.WriteAsJsonAsync(new
                {
                    batchId = batchRequest.BatchId,
                    tenantId = batchRequest.TenantId,
                    incidentId = batchRequest.IncidentId,
                    totalTargets = requests.Count,
                    orchestrationIds = orchestrationIds,
                    status = "Processing",
                    message = $"Batch remediation initiated for {requests.Count} targets",
                    statusUrls = orchestrationIds.Select(id => $"/api/xdr/status/{id}").ToList()
                });

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing batch remediation request");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(new { error = ex.Message });
                return errorResponse;
            }
        }

        /// <summary>
        /// Multi-tenant batch endpoint - multiple tenants, multiple requests
        /// POST /api/xdr/multi-tenant-batch
        /// </summary>
        [Function("MultiTenantBatchHTTP")]
        public async Task<HttpResponseData> MultiTenantBatchAsync(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "xdr/multi-tenant-batch")] HttpRequestData req,
            [DurableClient] DurableTaskClient client)
        {
            _logger.LogInformation("Multi-tenant batch request received");

            try
            {
                var multiTenantRequest = await JsonSerializer.DeserializeAsync<MultiTenantBatchRequest>(
                    req.Body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (multiTenantRequest == null || multiTenantRequest.Requests == null || !multiTenantRequest.Requests.Any())
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteAsJsonAsync(new { error = "Invalid multi-tenant request" });
                    return badRequest;
                }

                // Start orchestrations for each request
                var results = new List<object>();
                foreach (var request in multiTenantRequest.Requests)
                {
                    var instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
                        "DefenderXDROrchestrator",
                        request);

                    results.Add(new
                    {
                        requestId = request.RequestId,
                        tenantId = request.TenantId,
                        orchestrationId = instanceId,
                        statusUrl = $"/api/xdr/status/{instanceId}"
                    });
                }

                var response = req.CreateResponse(HttpStatusCode.Accepted);
                await response.WriteAsJsonAsync(new
                {
                    batchId = multiTenantRequest.BatchId,
                    totalRequests = multiTenantRequest.Requests.Count,
                    results = results,
                    status = "Processing",
                    message = $"Multi-tenant batch initiated for {multiTenantRequest.Requests.Count} requests"
                });

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing multi-tenant batch request");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(new { error = ex.Message });
                return errorResponse;
            }
        }

        /// <summary>
        /// Batch status endpoint - get status for multiple orchestrations
        /// GET /api/xdr/batch-status?ids=id1,id2,id3
        /// </summary>
        [Function("BatchStatusHTTP")]
        public async Task<HttpResponseData> GetBatchStatusAsync(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "xdr/batch-status")] HttpRequestData req,
            [DurableClient] DurableTaskClient client)
        {
            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var ids = query["ids"]?.Split(',') ?? Array.Empty<string>();

            if (!ids.Any())
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteAsJsonAsync(new { error = "No orchestration IDs provided" });
                return badRequest;
            }

            try
            {
                var statuses = new List<object>();
                foreach (var id in ids)
                {
                    var metadata = await client.GetInstanceAsync(id);
                    if (metadata != null)
                    {
                        statuses.Add(new
                        {
                            orchestrationId = id,
                            status = metadata.RuntimeStatus.ToString(),
                            createdAt = metadata.CreatedAt,
                            lastUpdatedAt = metadata.LastUpdatedAt
                        });
                    }
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    total = ids.Length,
                    retrieved = statuses.Count,
                    statuses = statuses
                });

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving batch status");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(new { error = ex.Message });
                return errorResponse;
            }
        }
    }
}
