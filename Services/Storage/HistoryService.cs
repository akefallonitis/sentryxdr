using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SentryXDR.Models;
using System.Text.Json;

namespace SentryXDR.Services.Storage
{
    public interface IHistoryService
    {
        Task AddHistoryEntryAsync(RemediationHistoryEntry entry);
        Task<RemediationHistoryEntry?> GetHistoryEntryAsync(string tenantId, string requestId);
        Task<HistoryResponse> QueryHistoryAsync(HistoryQueryParameters query);
        Task<HistoryStatistics> GetStatisticsAsync(string? tenantId = null, DateTime? fromDate = null, DateTime? toDate = null);
        Task UpdateHistoryEntryAsync(RemediationHistoryEntry entry);
        Task MarkAsCancelledAsync(string tenantId, string requestId, string cancelledBy, string reason);
    }

    public class HistoryService : IHistoryService
    {
        private readonly TableServiceClient _tableServiceClient;
        private readonly BlobServiceClient _blobServiceClient;
        private readonly ILogger<HistoryService> _logger;
        private readonly IConfiguration _configuration;
        private const string HistoryTableName = "XDRRemediationHistory";
        private const string HistoryContainerName = "xdr-history";

        public HistoryService(
            TableServiceClient tableServiceClient,
            BlobServiceClient blobServiceClient,
            ILogger<HistoryService> logger,
            IConfiguration configuration)
        {
            _tableServiceClient = tableServiceClient;
            _blobServiceClient = blobServiceClient;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task AddHistoryEntryAsync(RemediationHistoryEntry entry)
        {
            try
            {
                // Store in Table Storage for querying
                var tableClient = _tableServiceClient.GetTableClient(HistoryTableName);
                await tableClient.CreateIfNotExistsAsync();

                var entity = new TableEntity(entry.TenantId, entry.RequestId)
                {
                    ["OrchestrationId"] = entry.OrchestrationId,
                    ["IncidentId"] = entry.IncidentId,
                    ["Platform"] = entry.Platform.ToString(),
                    ["Action"] = entry.Action.ToString(),
                    ["Status"] = entry.Status,
                    ["InitiatedBy"] = entry.InitiatedBy,
                    ["Priority"] = entry.Priority.ToString(),
                    ["InitiatedAt"] = entry.InitiatedAt,
                    ["CompletedAt"] = entry.CompletedAt,
                    ["Success"] = entry.Success,
                    ["ErrorMessage"] = entry.ErrorMessage ?? string.Empty,
                    ["CancelledAt"] = entry.CancelledAt,
                    ["CancelledBy"] = entry.CancelledBy ?? string.Empty
                };

                await tableClient.UpsertEntityAsync(entity);

                // Store detailed entry in Blob Storage
                var containerClient = _blobServiceClient.GetBlobContainerClient(HistoryContainerName);
                await containerClient.CreateIfNotExistsAsync();

                var blobName = $"{entry.TenantId}/{entry.InitiatedAt:yyyy/MM/dd}/{entry.RequestId}.json";
                var blobClient = containerClient.GetBlobClient(blobName);

                var json = JsonSerializer.Serialize(entry, new JsonSerializerOptions { WriteIndented = true });
                await blobClient.UploadAsync(
                    BinaryData.FromString(json),
                    overwrite: true);

                _logger.LogInformation("History entry added: {RequestId}", entry.RequestId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding history entry: {RequestId}", entry.RequestId);
                throw;
            }
        }

        public async Task<RemediationHistoryEntry?> GetHistoryEntryAsync(string tenantId, string requestId)
        {
            try
            {
                // Try Table Storage first (faster)
                var tableClient = _tableServiceClient.GetTableClient(HistoryTableName);
                var entity = await tableClient.GetEntityIfExistsAsync<TableEntity>(tenantId, requestId);

                if (!entity.HasValue)
                {
                    return null;
                }

                // Get full details from Blob Storage
                var containerClient = _blobServiceClient.GetBlobContainerClient(HistoryContainerName);
                var initiatedAt = entity.Value.GetDateTime("InitiatedAt") ?? DateTime.UtcNow;
                var blobName = $"{tenantId}/{initiatedAt:yyyy/MM/dd}/{requestId}.json";
                var blobClient = containerClient.GetBlobClient(blobName);

                if (await blobClient.ExistsAsync())
                {
                    var download = await blobClient.DownloadContentAsync();
                    var json = download.Value.Content.ToString();
                    return JsonSerializer.Deserialize<RemediationHistoryEntry>(json);
                }

                // Fallback to Table Storage data
                return MapTableEntityToHistoryEntry(entity.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting history entry: {RequestId}", requestId);
                return null;
            }
        }

        public async Task<HistoryResponse> QueryHistoryAsync(HistoryQueryParameters query)
        {
            try
            {
                var tableClient = _tableServiceClient.GetTableClient(HistoryTableName);
                await tableClient.CreateIfNotExistsAsync();

                // Build filter
                var filters = new List<string>();
                
                if (!string.IsNullOrEmpty(query.TenantId))
                    filters.Add($"PartitionKey eq '{query.TenantId}'");
                
                if (!string.IsNullOrEmpty(query.IncidentId))
                    filters.Add($"IncidentId eq '{query.IncidentId}'");
                
                if (query.Platform.HasValue)
                    filters.Add($"Platform eq '{query.Platform}'");
                
                if (query.Action.HasValue)
                    filters.Add($"Action eq '{query.Action}'");
                
                if (!string.IsNullOrEmpty(query.Status))
                    filters.Add($"Status eq '{query.Status}'");
                
                if (!string.IsNullOrEmpty(query.InitiatedBy))
                    filters.Add($"InitiatedBy eq '{query.InitiatedBy}'");
                
                if (query.FromDate.HasValue)
                    filters.Add($"InitiatedAt ge datetime'{query.FromDate.Value:yyyy-MM-ddTHH:mm:ssZ}'");
                
                if (query.ToDate.HasValue)
                    filters.Add($"InitiatedAt le datetime'{query.ToDate.Value:yyyy-MM-ddTHH:mm:ssZ}'");

                var filter = filters.Any() ? string.Join(" and ", filters) : string.Empty;

                // Query Table Storage
                var pages = tableClient.QueryAsync<TableEntity>(filter: filter);
                var allEntities = new List<TableEntity>();

                await foreach (var page in pages.AsPages())
                {
                    allEntities.AddRange(page.Values);
                }

                // Apply sorting
                var sorted = query.SortBy?.ToLower() switch
                {
                    "initiatedat" => query.SortOrder?.ToLower() == "asc" 
                        ? allEntities.OrderBy(e => e.GetDateTime("InitiatedAt")).ToList()
                        : allEntities.OrderByDescending(e => e.GetDateTime("InitiatedAt")).ToList(),
                    "completedat" => query.SortOrder?.ToLower() == "asc"
                        ? allEntities.OrderBy(e => e.GetDateTime("CompletedAt")).ToList()
                        : allEntities.OrderByDescending(e => e.GetDateTime("CompletedAt")).ToList(),
                    "platform" => query.SortOrder?.ToLower() == "asc"
                        ? allEntities.OrderBy(e => e.GetString("Platform")).ToList()
                        : allEntities.OrderByDescending(e => e.GetString("Platform")).ToList(),
                    _ => allEntities.OrderByDescending(e => e.GetDateTime("InitiatedAt")).ToList()
                };

                // Apply pagination
                var totalCount = sorted.Count;
                var totalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize);
                var skip = (query.PageNumber - 1) * query.PageSize;
                var items = sorted.Skip(skip).Take(query.PageSize).ToList();

                return new HistoryResponse
                {
                    TotalCount = totalCount,
                    PageSize = query.PageSize,
                    PageNumber = query.PageNumber,
                    TotalPages = totalPages,
                    Items = items.Select(MapTableEntityToHistoryEntry).ToList()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying history");
                return new HistoryResponse();
            }
        }

        public async Task<HistoryStatistics> GetStatisticsAsync(string? tenantId = null, DateTime? fromDate = null, DateTime? toDate = null)
        {
            try
            {
                var tableClient = _tableServiceClient.GetTableClient(HistoryTableName);
                var filters = new List<string>();

                if (!string.IsNullOrEmpty(tenantId))
                    filters.Add($"PartitionKey eq '{tenantId}'");
                
                if (fromDate.HasValue)
                    filters.Add($"InitiatedAt ge datetime'{fromDate.Value:yyyy-MM-ddTHH:mm:ssZ}'");
                
                if (toDate.HasValue)
                    filters.Add($"InitiatedAt le datetime'{toDate.Value:yyyy-MM-ddTHH:mm:ssZ}'");

                var filter = filters.Any() ? string.Join(" and ", filters) : string.Empty;
                var entities = new List<TableEntity>();

                await foreach (var entity in tableClient.QueryAsync<TableEntity>(filter: filter))
                {
                    entities.Add(entity);
                }

                var stats = new HistoryStatistics
                {
                    TotalActions = entities.Count,
                    SuccessfulActions = entities.Count(e => e.GetBoolean("Success") == true),
                    FailedActions = entities.Count(e => e.GetBoolean("Success") == false && e.GetDateTime("CancelledAt") == null),
                    CancelledActions = entities.Count(e => e.GetDateTime("CancelledAt") != null),
                    InProgressActions = entities.Count(e => e.GetString("Status") == "InProgress"),
                    ActionsByPlatform = entities
                        .GroupBy(e => Enum.Parse<XDRPlatform>(e.GetString("Platform") ?? "MDE"))
                        .ToDictionary(g => g.Key, g => g.Count()),
                    ActionsByType = entities
                        .GroupBy(e => Enum.Parse<XDRAction>(e.GetString("Action") ?? "Unknown"))
                        .ToDictionary(g => g.Key, g => g.Count()),
                    ActionsByTenant = entities
                        .GroupBy(e => e.PartitionKey)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    SuccessRate = entities.Count > 0 
                        ? (double)entities.Count(e => e.GetBoolean("Success") == true) / entities.Count * 100 
                        : 0
                };

                // Calculate average completion time
                var completedActions = entities
                    .Where(e => e.GetDateTime("CompletedAt") != null)
                    .ToList();

                if (completedActions.Any())
                {
                    var totalSeconds = completedActions
                        .Select(e => (e.GetDateTime("CompletedAt")!.Value - e.GetDateTime("InitiatedAt")!.Value).TotalSeconds)
                        .Average();
                    stats.AverageCompletionTime = totalSeconds;
                }

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting statistics");
                return new HistoryStatistics();
            }
        }

        public async Task UpdateHistoryEntryAsync(RemediationHistoryEntry entry)
        {
            await AddHistoryEntryAsync(entry); // Upsert logic
        }

        public async Task MarkAsCancelledAsync(string tenantId, string requestId, string cancelledBy, string reason)
        {
            try
            {
                var entry = await GetHistoryEntryAsync(tenantId, requestId);
                if (entry != null)
                {
                    entry.Status = "Cancelled";
                    entry.CancelledAt = DateTime.UtcNow;
                    entry.CancelledBy = cancelledBy;
                    entry.CancellationReason = reason;
                    entry.CompletedAt = DateTime.UtcNow;
                    entry.Duration = DateTime.UtcNow - entry.InitiatedAt;

                    await UpdateHistoryEntryAsync(entry);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking as cancelled: {RequestId}", requestId);
            }
        }

        private RemediationHistoryEntry MapTableEntityToHistoryEntry(TableEntity entity)
        {
            return new RemediationHistoryEntry
            {
                RequestId = entity.RowKey,
                TenantId = entity.PartitionKey,
                OrchestrationId = entity.GetString("OrchestrationId") ?? string.Empty,
                IncidentId = entity.GetString("IncidentId") ?? string.Empty,
                Platform = Enum.Parse<XDRPlatform>(entity.GetString("Platform") ?? "MDE"),
                Action = Enum.Parse<XDRAction>(entity.GetString("Action") ?? "Unknown"),
                Status = entity.GetString("Status") ?? string.Empty,
                InitiatedBy = entity.GetString("InitiatedBy") ?? string.Empty,
                Priority = Enum.Parse<RemediationPriority>(entity.GetString("Priority") ?? "Medium"),
                InitiatedAt = entity.GetDateTime("InitiatedAt") ?? DateTime.UtcNow,
                CompletedAt = entity.GetDateTime("CompletedAt"),
                CancelledAt = entity.GetDateTime("CancelledAt"),
                CancelledBy = entity.GetString("CancelledBy"),
                Success = entity.GetBoolean("Success") ?? false,
                ErrorMessage = entity.GetString("ErrorMessage")
            };
        }
    }
}
