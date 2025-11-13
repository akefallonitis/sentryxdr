using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using SentryXDR.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace SentryXDR.Services.Storage
{
    public interface IAuditLogService
    {
        Task LogRemediationAsync(AuditLogEntry entry);
        Task<List<AuditLogEntry>> GetAuditLogsAsync(string tenantId, DateTime? startDate = null, DateTime? endDate = null);
    }

    public class AuditLogService : IAuditLogService
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuditLogService> _logger;
        private readonly string _containerName;

        public AuditLogService(
            BlobServiceClient blobServiceClient,
            IConfiguration configuration,
            ILogger<AuditLogService> logger)
        {
            _blobServiceClient = blobServiceClient;
            _configuration = configuration;
            _logger = logger;
            _containerName = configuration["Storage:AuditContainer"] ?? "xdr-audit-logs";
        }

        public async Task LogRemediationAsync(AuditLogEntry entry)
        {
            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
                await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);

                // Organize by tenant/year/month/day
                var blobName = $"{entry.TenantId}/{entry.Timestamp:yyyy}/{entry.Timestamp:MM}/{entry.Timestamp:dd}/{entry.Id}.json";
                var blobClient = containerClient.GetBlobClient(blobName);

                var json = JsonSerializer.Serialize(entry, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                
                var content = Encoding.UTF8.GetBytes(json);
                using var stream = new MemoryStream(content);

                await blobClient.UploadAsync(stream, overwrite: true);

                _logger.LogInformation($"Audit log saved: {entry.Id} for tenant {entry.TenantId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to save audit log {entry.Id}");
                // Don't throw - audit logging should not block remediation
            }
        }

        public async Task<List<AuditLogEntry>> GetAuditLogsAsync(string tenantId, DateTime? startDate = null, DateTime? endDate = null)
        {
            var logs = new List<AuditLogEntry>();

            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
                
                if (!await containerClient.ExistsAsync())
                {
                    return logs;
                }

                var prefix = $"{tenantId}/";
                await foreach (var blobItem in containerClient.GetBlobsAsync(prefix: prefix))
                {
                    // Filter by date if specified
                    if (startDate.HasValue && blobItem.Properties.CreatedOn < startDate.Value)
                        continue;
                    
                    if (endDate.HasValue && blobItem.Properties.CreatedOn > endDate.Value)
                        continue;

                    var blobClient = containerClient.GetBlobClient(blobItem.Name);
                    var response = await blobClient.DownloadContentAsync();
                    var json = response.Value.Content.ToString();
                    var entry = JsonSerializer.Deserialize<AuditLogEntry>(json);
                    
                    if (entry != null)
                    {
                        logs.Add(entry);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to retrieve audit logs for tenant {tenantId}");
            }

            return logs.OrderByDescending(l => l.Timestamp).ToList();
        }
    }

    public interface IStorageService
    {
        Task<string> SaveReportAsync(string tenantId, string reportName, byte[] content);
        Task<byte[]?> GetReportAsync(string tenantId, string reportName);
    }

    public class StorageService : IStorageService
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly ILogger<StorageService> _logger;
        private const string ReportsContainer = "xdr-reports";

        public StorageService(
            BlobServiceClient blobServiceClient,
            ILogger<StorageService> logger)
        {
            _blobServiceClient = blobServiceClient;
            _logger = logger;
        }

        public async Task<string> SaveReportAsync(string tenantId, string reportName, byte[] content)
        {
            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(ReportsContainer);
                await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);

                var blobName = $"{tenantId}/{DateTime.UtcNow:yyyy-MM-dd}/{reportName}";
                var blobClient = containerClient.GetBlobClient(blobName);

                using var stream = new MemoryStream(content);
                await blobClient.UploadAsync(stream, overwrite: true);

                _logger.LogInformation($"Report saved: {blobName}");
                return blobClient.Uri.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to save report {reportName}");
                throw;
            }
        }

        public async Task<byte[]?> GetReportAsync(string tenantId, string reportName)
        {
            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(ReportsContainer);
                var blobClient = containerClient.GetBlobClient($"{tenantId}/{reportName}");

                if (!await blobClient.ExistsAsync())
                {
                    return null;
                }

                var response = await blobClient.DownloadContentAsync();
                return response.Value.Content.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to retrieve report {reportName}");
                return null;
            }
        }
    }
}
