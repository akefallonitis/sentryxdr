using Microsoft.Extensions.Logging;
using SentryXDR.Models;
using SentryXDR.Services.Authentication;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Azure.Storage.Blobs;

namespace SentryXDR.Services.Workers
{
    public interface ILiveResponseService
    {
        // Live Response Session Management
        Task<XDRRemediationResponse> InitiateLiveResponseSessionAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> RunLiveResponseCommandAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> GetLiveResponseResultAsync(XDRRemediationRequest request);
        
        // Script Library Management
        Task<XDRRemediationResponse> RunLiveResponseScriptAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> UploadScriptToLibraryAsync(XDRRemediationRequest request);
        
        // File Operations
        Task<XDRRemediationResponse> PutFileToDeviceAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> GetFileFromDeviceAsync(XDRRemediationRequest request);
    }

    /// <summary>
    /// Live Response Service for Microsoft Defender for Endpoint
    /// Provides real-time incident response capabilities on devices
    /// API Reference: https://learn.microsoft.com/en-us/microsoft-365/security/defender-endpoint/run-live-response
    /// </summary>
    public class LiveResponseService : ILiveResponseService
    {
        private readonly ILogger<LiveResponseService> _logger;
        private readonly IMultiTenantAuthService _authService;
        private readonly HttpClient _httpClient;
        private readonly BlobServiceClient _blobServiceClient;
        private const string MdeBaseUrl = "https://api.securitycenter.microsoft.com/api";

        public LiveResponseService(
            ILogger<LiveResponseService> logger,
            IMultiTenantAuthService authService,
            HttpClient httpClient,
            BlobServiceClient blobServiceClient)
        {
            _logger = logger;
            _authService = authService;
            _httpClient = httpClient;
            _blobServiceClient = blobServiceClient;
        }

        private async Task SetAuthHeaderAsync(string tenantId)
        {
            var token = await _authService.GetMDETokenAsync(tenantId);
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        // ==================== Live Response Session Management ====================

        /// <summary>
        /// Initiate a Live Response session on a device
        /// POST /machines/{id}/LiveResponse
        /// Permission: Machine.LiveResponse
        /// </summary>
        public async Task<XDRRemediationResponse> InitiateLiveResponseSessionAsync(XDRRemediationRequest request)
        {
            try
            {
                await SetAuthHeaderAsync(request.TenantId);
                
                var machineId = request.Parameters["machineId"]?.ToString();
                var comment = request.Parameters["comment"]?.ToString() ?? "Live Response session initiated by SentryXDR";

                if (string.IsNullOrEmpty(machineId))
                {
                    return CreateFailureResponse(request, "Machine ID is required");
                }

                _logger.LogInformation("Initiating Live Response session on machine {MachineId}", machineId);

                var sessionBody = new
                {
                    Comment = comment
                };

                var url = $"{MdeBaseUrl}/machines/{machineId}/LiveResponse";
                var content = new StringContent(JsonSerializer.Serialize(sessionBody), Encoding.UTF8, "application/json");
                var result = await _httpClient.PostAsync(url, content);

                if (result.IsSuccessStatusCode)
                {
                    var responseContent = await result.Content.ReadAsStringAsync();
                    var session = JsonSerializer.Deserialize<JsonElement>(responseContent);
                    var sessionId = session.GetProperty("id").GetString();
                    var status = session.GetProperty("status").GetString();

                    _logger.LogInformation("Live Response session {SessionId} initiated successfully", sessionId);

                    return CreateSuccessResponse(request, $"Live Response session initiated on device", new Dictionary<string, object>
                    {
                        { "sessionId", sessionId! },
                        { "machineId", machineId },
                        { "status", status! },
                        { "comment", comment },
                        { "note", "Use RunLiveResponseCommand to execute commands in this session" }
                    });
                }
                else
                {
                    var error = await result.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to initiate Live Response session: {Error}", error);
                    return CreateFailureResponse(request, $"Failed to initiate session: {error}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception initiating Live Response session");
                return CreateExceptionResponse(request, ex);
            }
        }

        /// <summary>
        /// Run a Live Response command on a device
        /// POST /machines/{id}/runliveresponsecommand
        /// Commands: getfile, putfile, runscript
        /// </summary>
        public async Task<XDRRemediationResponse> RunLiveResponseCommandAsync(XDRRemediationRequest request)
        {
            try
            {
                await SetAuthHeaderAsync(request.TenantId);
                
                var machineId = request.Parameters["machineId"]?.ToString();
                var commandType = request.Parameters["commandType"]?.ToString(); // getfile, putfile, runscript
                var commandParameters = request.Parameters["commandParameters"]?.ToString();
                var comment = request.Parameters["comment"]?.ToString() ?? $"Live Response command: {commandType}";

                if (string.IsNullOrEmpty(machineId) || string.IsNullOrEmpty(commandType))
                {
                    return CreateFailureResponse(request, "Machine ID and command type are required");
                }

                _logger.LogInformation("Running Live Response command {CommandType} on machine {MachineId}", commandType, machineId);

                var commandBody = new
                {
                    Commands = new[]
                    {
                        new
                        {
                            type = commandType,
                            @params = new[] { new { key = "Value", value = commandParameters } }
                        }
                    },
                    Comment = comment
                };

                var url = $"{MdeBaseUrl}/machines/{machineId}/runliveresponsecommand";
                var content = new StringContent(JsonSerializer.Serialize(commandBody), Encoding.UTF8, "application/json");
                var result = await _httpClient.PostAsync(url, content);

                if (result.IsSuccessStatusCode)
                {
                    var responseContent = await result.Content.ReadAsStringAsync();
                    var commandResult = JsonSerializer.Deserialize<JsonElement>(responseContent);
                    var commandId = commandResult.GetProperty("id").GetString();
                    var status = commandResult.GetProperty("status").GetString();

                    _logger.LogInformation("Live Response command {CommandId} executed successfully", commandId);

                    return CreateSuccessResponse(request, $"Live Response command '{commandType}' executed", new Dictionary<string, object>
                    {
                        { "commandId", commandId! },
                        { "machineId", machineId },
                        { "commandType", commandType },
                        { "status", status! },
                        { "note", "Use GetLiveResponseResult to retrieve command output" }
                    });
                }
                else
                {
                    var error = await result.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to execute Live Response command: {Error}", error);
                    return CreateFailureResponse(request, $"Failed to execute command: {error}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception running Live Response command");
                return CreateExceptionResponse(request, ex);
            }
        }

        /// <summary>
        /// Get Live Response command result
        /// GET /machineactions/{id}/GetLiveResponseResultDownloadLink
        /// Downloads result file to blob storage: live-response-sessions
        /// </summary>
        public async Task<XDRRemediationResponse> GetLiveResponseResultAsync(XDRRemediationRequest request)
        {
            try
            {
                await SetAuthHeaderAsync(request.TenantId);
                
                var commandId = request.Parameters["commandId"]?.ToString();

                if (string.IsNullOrEmpty(commandId))
                {
                    return CreateFailureResponse(request, "Command ID is required");
                }

                _logger.LogInformation("Getting Live Response result for command {CommandId}", commandId);

                var url = $"{MdeBaseUrl}/machineactions/{commandId}/GetLiveResponseResultDownloadLink";
                var result = await _httpClient.GetAsync(url);

                if (result.IsSuccessStatusCode)
                {
                    var responseContent = await result.Content.ReadAsStringAsync();
                    var downloadInfo = JsonSerializer.Deserialize<JsonElement>(responseContent);
                    var downloadUrl = downloadInfo.GetProperty("value").GetString();

                    // Download result file and store in blob
                    var resultFileName = $"{commandId}_{DateTime.UtcNow:yyyyMMddHHmmss}.zip";
                    var containerClient = _blobServiceClient.GetBlobContainerClient("live-response-sessions");
                    await containerClient.CreateIfNotExistsAsync();
                    
                    var blobClient = containerClient.GetBlobClient(resultFileName);
                    
                    using (var downloadClient = new HttpClient())
                    {
                        var fileStream = await downloadClient.GetStreamAsync(downloadUrl);
                        await blobClient.UploadAsync(fileStream, overwrite: true);
                    }

                    var blobUrl = blobClient.Uri.ToString();

                    _logger.LogInformation("Live Response result downloaded to blob: {BlobUrl}", blobUrl);

                    return CreateSuccessResponse(request, "Live Response result retrieved successfully", new Dictionary<string, object>
                    {
                        { "commandId", commandId },
                        { "resultFile", resultFileName },
                        { "blobUrl", blobUrl },
                        { "container", "live-response-sessions" }
                    });
                }
                else
                {
                    var error = await result.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to get Live Response result: {Error}", error);
                    return CreateFailureResponse(request, $"Failed to get result: {error}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception getting Live Response result");
                return CreateExceptionResponse(request, ex);
            }
        }

        // ==================== Script Library Management ====================

        /// <summary>
        /// Run a PowerShell script from library on a device
        /// POST /machines/{id}/runscript
        /// </summary>
        public async Task<XDRRemediationResponse> RunLiveResponseScriptAsync(XDRRemediationRequest request)
        {
            try
            {
                await SetAuthHeaderAsync(request.TenantId);
                
                var machineId = request.Parameters["machineId"]?.ToString();
                var scriptName = request.Parameters["scriptName"]?.ToString();
                var scriptParameters = request.Parameters["scriptParameters"]?.ToString();
                var comment = request.Parameters["comment"]?.ToString() ?? $"Running script: {scriptName}";

                if (string.IsNullOrEmpty(machineId) || string.IsNullOrEmpty(scriptName))
                {
                    return CreateFailureResponse(request, "Machine ID and script name are required");
                }

                _logger.LogInformation("Running Live Response script {ScriptName} on machine {MachineId}", scriptName, machineId);

                var scriptBody = new
                {
                    ScriptName = scriptName,
                    Arguments = scriptParameters,
                    Comment = comment
                };

                var url = $"{MdeBaseUrl}/machines/{machineId}/runscript";
                var content = new StringContent(JsonSerializer.Serialize(scriptBody), Encoding.UTF8, "application/json");
                var result = await _httpClient.PostAsync(url, content);

                if (result.IsSuccessStatusCode)
                {
                    var responseContent = await result.Content.ReadAsStringAsync();
                    var scriptResult = JsonSerializer.Deserialize<JsonElement>(responseContent);
                    var actionId = scriptResult.GetProperty("id").GetString();
                    var status = scriptResult.GetProperty("status").GetString();

                    _logger.LogInformation("Live Response script {ScriptName} executed with action ID {ActionId}", scriptName, actionId);

                    return CreateSuccessResponse(request, $"Script '{scriptName}' executed successfully", new Dictionary<string, object>
                    {
                        { "actionId", actionId! },
                        { "machineId", machineId },
                        { "scriptName", scriptName },
                        { "status", status! },
                        { "note", "Use GetLiveResponseResult to retrieve script output" }
                    });
                }
                else
                {
                    var error = await result.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to run Live Response script: {Error}", error);
                    return CreateFailureResponse(request, $"Failed to run script: {error}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception running Live Response script");
                return CreateExceptionResponse(request, ex);
            }
        }

        /// <summary>
        /// Upload a PowerShell script to the library
        /// POST /libraryfiles
        /// Stores script in blob: live-response-library
        /// </summary>
        public async Task<XDRRemediationResponse> UploadScriptToLibraryAsync(XDRRemediationRequest request)
        {
            try
            {
                await SetAuthHeaderAsync(request.TenantId);
                
                var scriptName = request.Parameters["scriptName"]?.ToString();
                var scriptContent = request.Parameters["scriptContent"]?.ToString();
                var description = request.Parameters["description"]?.ToString() ?? $"IR Script: {scriptName}";

                if (string.IsNullOrEmpty(scriptName) || string.IsNullOrEmpty(scriptContent))
                {
                    return CreateFailureResponse(request, "Script name and content are required");
                }

                _logger.LogInformation("Uploading script {ScriptName} to library", scriptName);

                // Upload to blob storage first
                var containerClient = _blobServiceClient.GetBlobContainerClient("live-response-library");
                await containerClient.CreateIfNotExistsAsync();
                
                var blobClient = containerClient.GetBlobClient(scriptName);
                var scriptBytes = Encoding.UTF8.GetBytes(scriptContent);
                using (var stream = new MemoryStream(scriptBytes))
                {
                    await blobClient.UploadAsync(stream, overwrite: true);
                }

                // Register in MDE library
                var libraryBody = new
                {
                    FileName = scriptName,
                    Description = description,
                    FileContent = Convert.ToBase64String(scriptBytes)
                };

                var url = $"{MdeBaseUrl}/libraryfiles";
                var content = new StringContent(JsonSerializer.Serialize(libraryBody), Encoding.UTF8, "application/json");
                var result = await _httpClient.PostAsync(url, content);

                if (result.IsSuccessStatusCode)
                {
                    var responseContent = await result.Content.ReadAsStringAsync();
                    var library = JsonSerializer.Deserialize<JsonElement>(responseContent);
                    var libraryId = library.GetProperty("id").GetString();

                    _logger.LogInformation("Script {ScriptName} uploaded to library with ID {LibraryId}", scriptName, libraryId);

                    return CreateSuccessResponse(request, $"Script '{scriptName}' uploaded to library", new Dictionary<string, object>
                    {
                        { "libraryId", libraryId! },
                        { "scriptName", scriptName },
                        { "blobUrl", blobClient.Uri.ToString() },
                        { "container", "live-response-library" }
                    });
                }
                else
                {
                    var error = await result.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to upload script to library: {Error}", error);
                    return CreateFailureResponse(request, $"Failed to upload script: {error}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception uploading script to library");
                return CreateExceptionResponse(request, ex);
            }
        }

        // ==================== File Operations ====================

        /// <summary>
        /// Put a file to a device
        /// POST /machines/{id}/putfile
        /// </summary>
        public async Task<XDRRemediationResponse> PutFileToDeviceAsync(XDRRemediationRequest request)
        {
            try
            {
                await SetAuthHeaderAsync(request.TenantId);
                
                var machineId = request.Parameters["machineId"]?.ToString();
                var fileName = request.Parameters["fileName"]?.ToString();
                var fileContent = request.Parameters["fileContent"]?.ToString(); // Base64 encoded
                var comment = request.Parameters["comment"]?.ToString() ?? $"Uploading file: {fileName}";

                if (string.IsNullOrEmpty(machineId) || string.IsNullOrEmpty(fileName) || string.IsNullOrEmpty(fileContent))
                {
                    return CreateFailureResponse(request, "Machine ID, file name, and file content are required");
                }

                _logger.LogInformation("Putting file {FileName} to machine {MachineId}", fileName, machineId);

                var fileBody = new
                {
                    FileName = fileName,
                    FileContent = fileContent,
                    Comment = comment
                };

                var url = $"{MdeBaseUrl}/machines/{machineId}/putfile";
                var content = new StringContent(JsonSerializer.Serialize(fileBody), Encoding.UTF8, "application/json");
                var result = await _httpClient.PostAsync(url, content);

                if (result.IsSuccessStatusCode)
                {
                    var responseContent = await result.Content.ReadAsStringAsync();
                    var fileResult = JsonSerializer.Deserialize<JsonElement>(responseContent);
                    var actionId = fileResult.GetProperty("id").GetString();
                    var status = fileResult.GetProperty("status").GetString();

                    _logger.LogInformation("File {FileName} put to machine {MachineId} with action ID {ActionId}", fileName, machineId, actionId);

                    return CreateSuccessResponse(request, $"File '{fileName}' uploaded to device", new Dictionary<string, object>
                    {
                        { "actionId", actionId! },
                        { "machineId", machineId },
                        { "fileName", fileName },
                        { "status", status! }
                    });
                }
                else
                {
                    var error = await result.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to put file to device: {Error}", error);
                    return CreateFailureResponse(request, $"Failed to upload file: {error}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception putting file to device");
                return CreateExceptionResponse(request, ex);
            }
        }

        /// <summary>
        /// Get a file from a device
        /// POST /machines/{id}/getfile
        /// Downloads file to blob: live-response-sessions
        /// </summary>
        public async Task<XDRRemediationResponse> GetFileFromDeviceAsync(XDRRemediationRequest request)
        {
            try
            {
                await SetAuthHeaderAsync(request.TenantId);
                
                var machineId = request.Parameters["machineId"]?.ToString();
                var filePath = request.Parameters["filePath"]?.ToString();
                var comment = request.Parameters["comment"]?.ToString() ?? $"Collecting file: {filePath}";

                if (string.IsNullOrEmpty(machineId) || string.IsNullOrEmpty(filePath))
                {
                    return CreateFailureResponse(request, "Machine ID and file path are required");
                }

                _logger.LogInformation("Getting file {FilePath} from machine {MachineId}", filePath, machineId);

                var fileBody = new
                {
                    FilePath = filePath,
                    Comment = comment
                };

                var url = $"{MdeBaseUrl}/machines/{machineId}/getfile";
                var content = new StringContent(JsonSerializer.Serialize(fileBody), Encoding.UTF8, "application/json");
                var result = await _httpClient.PostAsync(url, content);

                if (result.IsSuccessStatusCode)
                {
                    var responseContent = await result.Content.ReadAsStringAsync();
                    var fileResult = JsonSerializer.Deserialize<JsonElement>(responseContent);
                    var actionId = fileResult.GetProperty("id").GetString();
                    var status = fileResult.GetProperty("status").GetString();

                    _logger.LogInformation("File {FilePath} collection initiated from machine {MachineId} with action ID {ActionId}", filePath, machineId, actionId);

                    return CreateSuccessResponse(request, $"File collection initiated", new Dictionary<string, object>
                    {
                        { "actionId", actionId! },
                        { "machineId", machineId },
                        { "filePath", filePath },
                        { "status", status! },
                        { "note", "Use GetLiveResponseResult to download the collected file" }
                    });
                }
                else
                {
                    var error = await result.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to get file from device: {Error}", error);
                    return CreateFailureResponse(request, $"Failed to collect file: {error}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception getting file from device");
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
