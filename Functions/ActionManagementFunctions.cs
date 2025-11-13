using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using SentryXDR.Models;
using SentryXDR.Services.Storage;
using System.Net;
using System.Text.Json;

namespace SentryXDR.Functions
{
    /// <summary>
    /// Action Cancellation and History Management HTTP Endpoints
    /// </summary>
    public class ActionManagementFunctions
    {
        private readonly ILogger<ActionManagementFunctions> _logger;
        private readonly IHistoryService _historyService;

        public ActionManagementFunctions(
            ILogger<ActionManagementFunctions> logger,
            IHistoryService historyService)
        {
            _logger = logger;
            _historyService = historyService;
        }

        /// <summary>
        /// Cancel an in-progress remediation action
        /// POST /api/xdr/cancel
        /// </summary>
        [Function("CancelRemediationHTTP")]
        public async Task<HttpResponseData> CancelRemediationAsync(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "xdr/cancel")] HttpRequestData req,
            [DurableClient] DurableTaskClient client)
        {
            _logger.LogInformation("Cancel remediation request received");

            try
            {
                var cancelRequest = await JsonSerializer.DeserializeAsync<CancelRemediationRequest>(
                    req.Body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (cancelRequest == null || string.IsNullOrEmpty(cancelRequest.OrchestrationId))
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteAsJsonAsync(new { error = "Invalid cancellation request" });
                    return badRequest;
                }

                // Get orchestration status
                var metadata = await client.GetInstanceAsync(cancelRequest.OrchestrationId);
                
                if (metadata == null)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteAsJsonAsync(new { error = "Orchestration not found" });
                    return notFound;
                }

                // Check if already completed or cancelled
                if (metadata.RuntimeStatus == OrchestrationRuntimeStatus.Completed ||
                    metadata.RuntimeStatus == OrchestrationRuntimeStatus.Failed ||
                    metadata.RuntimeStatus == OrchestrationRuntimeStatus.Terminated)
                {
                    var conflict = req.CreateResponse(HttpStatusCode.Conflict);
                    await conflict.WriteAsJsonAsync(new
                    {
                        error = "Cannot cancel",
                        status = metadata.RuntimeStatus.ToString(),
                        message = "Orchestration already completed or terminated"
                    });
                    return conflict;
                }

                // Terminate the orchestration
                await client.TerminateInstanceAsync(
                    cancelRequest.OrchestrationId,
                    $"Cancelled by {cancelRequest.CancelledBy}: {cancelRequest.Reason}");

                // Update history
                if (!string.IsNullOrEmpty(cancelRequest.RequestId))
                {
                    var historyQuery = await _historyService.QueryHistoryAsync(new HistoryQueryParameters
                    {
                        PageSize = 1
                    });

                    if (historyQuery.Items.Any())
                    {
                        var entry = historyQuery.Items.First();
                        await _historyService.MarkAsCancelledAsync(
                            entry.TenantId,
                            cancelRequest.RequestId,
                            cancelRequest.CancelledBy,
                            cancelRequest.Reason);
                    }
                }

                _logger.LogInformation("Orchestration cancelled: {OrchestrationId}", cancelRequest.OrchestrationId);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new CancelRemediationResponse
                {
                    OrchestrationId = cancelRequest.OrchestrationId,
                    RequestId = cancelRequest.RequestId,
                    Success = true,
                    Status = "Cancelled",
                    Message = $"Remediation action cancelled successfully",
                    CancelledAt = DateTime.UtcNow,
                    CancelledBy = cancelRequest.CancelledBy
                });

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling remediation");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(new { error = ex.Message });
                return errorResponse;
            }
        }

        /// <summary>
        /// Get history for a specific remediation action
        /// GET /api/xdr/history/{requestId}?tenantId={tenantId}
        /// </summary>
        [Function("GetHistoryEntryHTTP")]
        public async Task<HttpResponseData> GetHistoryEntryAsync(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "xdr/history/{requestId}")] HttpRequestData req,
            string requestId)
        {
            _logger.LogInformation("Get history entry: {RequestId}", requestId);

            try
            {
                var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                var tenantId = query["tenantId"];

                if (string.IsNullOrEmpty(tenantId))
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteAsJsonAsync(new { error = "TenantId is required" });
                    return badRequest;
                }

                var entry = await _historyService.GetHistoryEntryAsync(tenantId, requestId);

                if (entry == null)
                {
                    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFound.WriteAsJsonAsync(new { error = "History entry not found" });
                    return notFound;
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(entry);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting history entry");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(new { error = ex.Message });
                return errorResponse;
            }
        }

        /// <summary>
        /// Query remediation history with filters
        /// POST /api/xdr/history/query
        /// </summary>
        [Function("QueryHistoryHTTP")]
        public async Task<HttpResponseData> QueryHistoryAsync(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "xdr/history/query")] HttpRequestData req)
        {
            _logger.LogInformation("Query history request received");

            try
            {
                var queryParams = await JsonSerializer.DeserializeAsync<HistoryQueryParameters>(
                    req.Body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) 
                    ?? new HistoryQueryParameters();

                var results = await _historyService.QueryHistoryAsync(queryParams);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(results);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying history");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(new { error = ex.Message });
                return errorResponse;
            }
        }

        /// <summary>
        /// Get history statistics
        /// GET /api/xdr/history/statistics?tenantId={tenantId}&fromDate={date}&toDate={date}
        /// </summary>
        [Function("GetHistoryStatisticsHTTP")]
        public async Task<HttpResponseData> GetHistoryStatisticsAsync(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "xdr/history/statistics")] HttpRequestData req)
        {
            _logger.LogInformation("Get history statistics request received");

            try
            {
                var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                var tenantId = query["tenantId"];
                var fromDateStr = query["fromDate"];
                var toDateStr = query["toDate"];

                DateTime? fromDate = null;
                DateTime? toDate = null;

                if (!string.IsNullOrEmpty(fromDateStr) && DateTime.TryParse(fromDateStr, out var from))
                    fromDate = from;

                if (!string.IsNullOrEmpty(toDateStr) && DateTime.TryParse(toDateStr, out var to))
                    toDate = to;

                var statistics = await _historyService.GetStatisticsAsync(tenantId, fromDate, toDate);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(statistics);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting statistics");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(new { error = ex.Message });
                return errorResponse;
            }
        }

        /// <summary>
        /// Purge history for completed/cancelled actions older than specified date
        /// DELETE /api/xdr/history/purge?tenantId={tenantId}&beforeDate={date}
        /// </summary>
        [Function("PurgeHistoryHTTP")]
        public async Task<HttpResponseData> PurgeHistoryAsync(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "xdr/history/purge")] HttpRequestData req,
            [DurableClient] DurableTaskClient client)
        {
            _logger.LogInformation("Purge history request received");

            try
            {
                var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                var tenantId = query["tenantId"];
                var beforeDateStr = query["beforeDate"];

                if (string.IsNullOrEmpty(beforeDateStr) || !DateTime.TryParse(beforeDateStr, out var beforeDate))
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteAsJsonAsync(new { error = "Valid beforeDate is required" });
                    return badRequest;
                }

                // Query orchestrations to purge
                var filter = new OrchestrationQuery
                {
                    CreatedFrom = DateTime.MinValue,
                    CreatedTo = beforeDate
                };

                var instances = new List<OrchestrationMetadata>();
                await foreach (var instance in client.GetAllInstancesAsync(filter))
                {
                    instances.Add(instance);
                }
                
                var purgedCount = 0;

                foreach (var instance in instances)
                {
                    if (instance.RuntimeStatus == OrchestrationRuntimeStatus.Completed ||
                        instance.RuntimeStatus == OrchestrationRuntimeStatus.Failed ||
                        instance.RuntimeStatus == OrchestrationRuntimeStatus.Terminated)
                    {
                        await client.PurgeInstanceAsync(instance.InstanceId);
                        purgedCount++;
                    }
                }

                _logger.LogInformation("Purged {Count} orchestration instances", purgedCount);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    success = true,
                    purgedCount = purgedCount,
                    message = $"Purged {purgedCount} orchestration instances older than {beforeDate:yyyy-MM-dd}"
                });

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error purging history");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(new { error = ex.Message });
                return errorResponse;
            }
        }
    }
}
