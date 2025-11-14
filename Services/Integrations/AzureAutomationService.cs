using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace SentryXDR.Services.Workers
{
    public interface IAzureAutomationService
    {
        Task<string> StartRunbookAsync(string runbookName, Dictionary<string, object> parameters);
        Task<JsonElement> GetJobStatusAsync(string jobId);
        Task<string> GetJobOutputAsync(string jobId);
    }

    /// <summary>
    /// Azure Automation Service
    /// Manages Azure Automation Account runbook execution
    /// Used for on-premise Active Directory operations via Hybrid Worker
    /// API Reference: https://learn.microsoft.com/en-us/rest/api/automation/
    /// </summary>
    public class AzureAutomationService : IAzureAutomationService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AzureAutomationService> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _subscriptionId;
        private readonly string _resourceGroupName;
        private readonly string _automationAccountName;
        private readonly string _armBaseUrl = "https://management.azure.com";

        public AzureAutomationService(
            HttpClient httpClient,
            ILogger<AzureAutomationService> logger,
            IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;
            _configuration = configuration;
            
            _subscriptionId = configuration["Azure:SubscriptionId"] ?? throw new InvalidOperationException("Azure:SubscriptionId not configured");
            _resourceGroupName = configuration["Azure:ResourceGroupName"] ?? throw new InvalidOperationException("Azure:ResourceGroupName not configured");
            _automationAccountName = configuration["Azure:AutomationAccountName"] ?? throw new InvalidOperationException("Azure:AutomationAccountName not configured");
        }

        /// <summary>
        /// Start an Azure Automation runbook
        /// POST /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Automation/automationAccounts/{automationAccountName}/jobs/{jobId}
        /// Uses Managed Identity for authentication
        /// </summary>
        public async Task<string> StartRunbookAsync(string runbookName, Dictionary<string, object> parameters)
        {
            try
            {
                _logger.LogInformation("Starting runbook: {RunbookName} with {ParamCount} parameters", runbookName, parameters.Count);

                // Generate job ID
                var jobId = Guid.NewGuid().ToString();

                var url = $"{_armBaseUrl}/subscriptions/{_subscriptionId}/resourceGroups/{_resourceGroupName}/providers/Microsoft.Automation/automationAccounts/{_automationAccountName}/jobs/{jobId}?api-version=2023-11-01";

                var jobRequest = new
                {
                    properties = new
                    {
                        runbook = new
                        {
                            name = runbookName
                        },
                        parameters = parameters,
                        runOn = _configuration["Azure:HybridWorkerGroupName"] ?? "HybridWorkerGroup" // Target hybrid worker
                    }
                };

                var response = await _httpClient.PutAsJsonAsync(url, jobRequest);
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Runbook job started successfully: {JobId}", jobId);
                    return jobId;
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to start runbook: {StatusCode} - {Error}", response.StatusCode, errorContent);
                throw new Exception($"Failed to start runbook: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting runbook {RunbookName}", runbookName);
                throw;
            }
        }

        /// <summary>
        /// Get runbook job status
        /// GET /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Automation/automationAccounts/{automationAccountName}/jobs/{jobId}
        /// </summary>
        public async Task<JsonElement> GetJobStatusAsync(string jobId)
        {
            try
            {
                var url = $"{_armBaseUrl}/subscriptions/{_subscriptionId}/resourceGroups/{_resourceGroupName}/providers/Microsoft.Automation/automationAccounts/{_automationAccountName}/jobs/{jobId}?api-version=2023-11-01";

                var response = await _httpClient.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<JsonElement>(content);
                }

                throw new Exception($"Failed to get job status: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting job status for {JobId}", jobId);
                throw;
            }
        }

        /// <summary>
        /// Get runbook job output
        /// GET /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Automation/automationAccounts/{automationAccountName}/jobs/{jobId}/output
        /// </summary>
        public async Task<string> GetJobOutputAsync(string jobId)
        {
            try
            {
                var url = $"{_armBaseUrl}/subscriptions/{_subscriptionId}/resourceGroups/{_resourceGroupName}/providers/Microsoft.Automation/automationAccounts/{_automationAccountName}/jobs/{jobId}/output?api-version=2023-11-01";

                var response = await _httpClient.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync();
                }

                throw new Exception($"Failed to get job output: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting job output for {JobId}", jobId);
                throw;
            }
        }
    }
}
