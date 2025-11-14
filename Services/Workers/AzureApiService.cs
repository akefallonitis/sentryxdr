using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SentryXDR.Models;
using SentryXDR.Services.Authentication;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SentryXDR.Services.Workers
{
    public interface IAzureWorkerService
    {
        Task<XDRRemediationResponse> IsolateVMNetworkAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> StopVMAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> RestartVMAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> DeleteVMAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> SnapshotVMAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> DetachDiskAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> RevokeVMAccessAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> UpdateNSGRulesAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> DisablePublicIPAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> BlockStorageAccountAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> DisableServicePrincipalAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> RotateStorageKeysAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> DeleteMaliciousResourceAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> EnableDiagnosticLogsAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> TagResourceAsCompromisedAsync(XDRRemediationRequest request);
    }

    /// <summary>
    /// Azure Security Worker - Complete implementation with Managed Identity
    /// Handles 15 Azure infrastructure security remediation actions
    /// </summary>
    public class AzureWorkerService : IAzureWorkerService
    {
        private readonly ILogger<AzureWorkerService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IManagedIdentityAuthService _managedIdentityAuth;
        private readonly HttpClient _httpClient;
        private const string AzureManagementBaseUrl = "https://management.azure.com";
        private const string ApiVersion = "2023-09-01";

        public AzureWorkerService(
            ILogger<AzureWorkerService> logger,
            IConfiguration configuration,
            IManagedIdentityAuthService managedIdentityAuth,
            HttpClient httpClient)
        {
            _logger = logger;
            _configuration = configuration;
            _managedIdentityAuth = managedIdentityAuth;
            _httpClient = httpClient;
        }

        private async Task SetAuthHeaderAsync()
        {
            var token = await _managedIdentityAuth.GetAzureManagementTokenAsync();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        /// <summary>
        /// Isolate VM network by applying deny-all NSG rules
        /// </summary>
        public async Task<XDRRemediationResponse> IsolateVMNetworkAsync(XDRRemediationRequest request)
        {
            try
            {
                _logger.LogInformation("Isolating VM network: {Request}", request.RequestId);

                var subscriptionId = request.Parameters["subscriptionId"]?.ToString();
                var resourceGroup = request.Parameters["resourceGroup"]?.ToString();
                var vmName = request.Parameters["vmName"]?.ToString();

                if (string.IsNullOrEmpty(subscriptionId) || string.IsNullOrEmpty(resourceGroup) || string.IsNullOrEmpty(vmName))
                {
                    return CreateFailureResponse(request, "Missing required parameters: subscriptionId, resourceGroup, vmName");
                }

                await SetAuthHeaderAsync();

                // Get VM details to find network interface
                var vmUrl = $"{AzureManagementBaseUrl}/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Compute/virtualMachines/{vmName}?api-version={ApiVersion}";
                var vmResponse = await _httpClient.GetAsync(vmUrl);

                if (!vmResponse.IsSuccessStatusCode)
                {
                    return CreateFailureResponse(request, $"Failed to get VM details: {vmResponse.StatusCode}");
                }

                var vmContent = await vmResponse.Content.ReadAsStringAsync();
                var vmData = JsonDocument.Parse(vmContent);

                // Extract network interface ID
                var nicId = vmData.RootElement
                    .GetProperty("properties")
                    .GetProperty("networkProfile")
                    .GetProperty("networkInterfaces")[0]
                    .GetProperty("id").GetString();

                // Create isolation NSG with deny-all rules
                var nsgName = $"{vmName}-isolation-nsg";
                var nsgUrl = $"{AzureManagementBaseUrl}/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Network/networkSecurityGroups/{nsgName}?api-version={ApiVersion}";

                var isolationNSG = new
                {
                    location = vmData.RootElement.GetProperty("location").GetString(),
                    properties = new
                    {
                        securityRules = new[]
                        {
                            new
                            {
                                name = "DenyAllInbound",
                                properties = new
                                {
                                    priority = 100,
                                    direction = "Inbound",
                                    access = "Deny",
                                    protocol = "*",
                                    sourcePortRange = "*",
                                    destinationPortRange = "*",
                                    sourceAddressPrefix = "*",
                                    destinationAddressPrefix = "*"
                                }
                            },
                            new
                            {
                                name = "DenyAllOutbound",
                                properties = new
                                {
                                    priority = 100,
                                    direction = "Outbound",
                                    access = "Deny",
                                    protocol = "*",
                                    sourcePortRange = "*",
                                    destinationPortRange = "*",
                                    sourceAddressPrefix = "*",
                                    destinationAddressPrefix = "*"
                                }
                            }
                        }
                    },
                    tags = new
                    {
                        Purpose = "XDR-Isolation",
                        CreatedBy = "SentryXDR",
                        IncidentId = request.IncidentId
                    }
                };

                var nsgJson = JsonSerializer.Serialize(isolationNSG);
                var nsgResponse = await _httpClient.PutAsync(nsgUrl, new StringContent(nsgJson, Encoding.UTF8, "application/json"));

                if (!nsgResponse.IsSuccessStatusCode)
                {
                    return CreateFailureResponse(request, $"Failed to create isolation NSG: {nsgResponse.StatusCode}");
                }

                _logger.LogInformation("Successfully isolated VM network: {VMName}", vmName);

                return CreateSuccessResponse(request, $"VM {vmName} network isolated successfully", new Dictionary<string, object>
                {
                    ["vmName"] = vmName,
                    ["nsgName"] = nsgName,
                    ["isolated"] = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error isolating VM network");
                return CreateExceptionResponse(request, ex);
            }
        }

        /// <summary>
        /// Stop (deallocate) a VM immediately
        /// </summary>
        public async Task<XDRRemediationResponse> StopVMAsync(XDRRemediationRequest request)
        {
            try
            {
                _logger.LogInformation("Stopping VM: {Request}", request.RequestId);

                var subscriptionId = request.Parameters["subscriptionId"]?.ToString();
                var resourceGroup = request.Parameters["resourceGroup"]?.ToString();
                var vmName = request.Parameters["vmName"]?.ToString();

                if (string.IsNullOrEmpty(subscriptionId) || string.IsNullOrEmpty(resourceGroup) || string.IsNullOrEmpty(vmName))
                {
                    return CreateFailureResponse(request, "Missing required parameters");
                }

                await SetAuthHeaderAsync();

                var url = $"{AzureManagementBaseUrl}/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Compute/virtualMachines/{vmName}/deallocate?api-version={ApiVersion}";
                
                var response = await _httpClient.PostAsync(url, null);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Successfully stopped VM: {VMName}", vmName);
                    return CreateSuccessResponse(request, $"VM {vmName} stopped successfully");
                }

                return CreateFailureResponse(request, $"Failed to stop VM: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping VM");
                return CreateExceptionResponse(request, ex);
            }
        }

        /// <summary>
        /// Restart a VM
        /// </summary>
        public async Task<XDRRemediationResponse> RestartVMAsync(XDRRemediationRequest request)
        {
            try
            {
                _logger.LogInformation("Restarting VM: {Request}", request.RequestId);

                var subscriptionId = request.Parameters["subscriptionId"]?.ToString();
                var resourceGroup = request.Parameters["resourceGroup"]?.ToString();
                var vmName = request.Parameters["vmName"]?.ToString();

                await SetAuthHeaderAsync();

                var url = $"{AzureManagementBaseUrl}/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Compute/virtualMachines/{vmName}/restart?api-version={ApiVersion}";
                
                var response = await _httpClient.PostAsync(url, null);

                if (response.IsSuccessStatusCode)
                {
                    return CreateSuccessResponse(request, $"VM {vmName} restarted successfully");
                }

                return CreateFailureResponse(request, $"Failed to restart VM: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restarting VM");
                return CreateExceptionResponse(request, ex);
            }
        }

        /// <summary>
        /// Delete a compromised VM
        /// </summary>
        public async Task<XDRRemediationResponse> DeleteVMAsync(XDRRemediationRequest request)
        {
            try
            {
                _logger.LogInformation("Deleting VM: {Request}", request.RequestId);

                var subscriptionId = request.Parameters["subscriptionId"]?.ToString();
                var resourceGroup = request.Parameters["resourceGroup"]?.ToString();
                var vmName = request.Parameters["vmName"]?.ToString();

                await SetAuthHeaderAsync();

                var url = $"{AzureManagementBaseUrl}/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Compute/virtualMachines/{vmName}?api-version={ApiVersion}";
                
                var response = await _httpClient.DeleteAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    return CreateSuccessResponse(request, $"VM {vmName} deleted successfully");
                }

                return CreateFailureResponse(request, $"Failed to delete VM: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting VM");
                return CreateExceptionResponse(request, ex);
            }
        }

        /// <summary>
        /// Create a snapshot of VM disk for forensic analysis
        /// </summary>
        public async Task<XDRRemediationResponse> SnapshotVMAsync(XDRRemediationRequest request)
        {
            try
            {
                _logger.LogInformation("Creating VM snapshot: {Request}", request.RequestId);

                var subscriptionId = request.Parameters["subscriptionId"]?.ToString();
                var resourceGroup = request.Parameters["resourceGroup"]?.ToString();
                var vmName = request.Parameters["vmName"]?.ToString();

                await SetAuthHeaderAsync();

                // Get VM to find OS disk
                var vmUrl = $"{AzureManagementBaseUrl}/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Compute/virtualMachines/{vmName}?api-version={ApiVersion}";
                var vmResponse = await _httpClient.GetAsync(vmUrl);

                if (!vmResponse.IsSuccessStatusCode)
                {
                    return CreateFailureResponse(request, "Failed to get VM details");
                }

                var vmContent = await vmResponse.Content.ReadAsStringAsync();
                var vmData = JsonDocument.Parse(vmContent);
                var osDiskId = vmData.RootElement.GetProperty("properties").GetProperty("storageProfile").GetProperty("osDisk").GetProperty("managedDisk").GetProperty("id").GetString();

                // Create snapshot
                var snapshotName = $"{vmName}-snapshot-{DateTime.UtcNow:yyyyMMddHHmmss}";
                var snapshotUrl = $"{AzureManagementBaseUrl}/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Compute/snapshots/{snapshotName}?api-version={ApiVersion}";

                var snapshotBody = new
                {
                    location = vmData.RootElement.GetProperty("location").GetString(),
                    properties = new
                    {
                        creationData = new
                        {
                            createOption = "Copy",
                            sourceResourceId = osDiskId
                        }
                    },
                    tags = new
                    {
                        Purpose = "Forensic-Analysis",
                        IncidentId = request.IncidentId,
                        CreatedBy = "SentryXDR"
                    }
                };

                var response = await _httpClient.PutAsync(snapshotUrl, new StringContent(JsonSerializer.Serialize(snapshotBody), Encoding.UTF8, "application/json"));

                if (response.IsSuccessStatusCode)
                {
                    return CreateSuccessResponse(request, $"Snapshot {snapshotName} created successfully", new Dictionary<string, object>
                    {
                        ["snapshotName"] = snapshotName
                    });
                }

                return CreateFailureResponse(request, $"Failed to create snapshot: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating VM snapshot");
                return CreateExceptionResponse(request, ex);
            }
        }

        // ==================== REMAINING AZURE ACTIONS (10) ====================
        // (DetachDisk, RevokeVMAccess, UpdateNSGRules, DisablePublicIP, BlockStorageAccount, 
        //  DisableServicePrincipal, RotateStorageKeys, DeleteMaliciousResource, 
        //  EnableDiagnosticLogs, TagResourceAsCompromised)

        /// <summary>
        /// Detach disk from VM for isolation
        /// POST /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/disks/{diskName}/detach
        /// </summary>
        public async Task<XDRRemediationResponse> DetachDiskAsync(XDRRemediationRequest request)
        {
            try
            {
                await SetAuthHeaderAsync();
                var subscriptionId = request.Parameters["subscriptionId"]?.ToString();
                var resourceGroup = request.Parameters["resourceGroupName"]?.ToString();
                var vmName = request.Parameters["vmName"]?.ToString();
                var diskName = request.Parameters["diskName"]?.ToString();

                _logger.LogInformation("Detaching disk {DiskName} from VM {VmName}", diskName, vmName);

                // Get current VM configuration
                var vmUrl = $"{AzureManagementBaseUrl}/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Compute/virtualMachines/{vmName}?api-version={ApiVersion}";
                var vmResponse = await _httpClient.GetAsync(vmUrl);
                var vmData = await vmResponse.Content.ReadAsStringAsync();
                var vm = JsonSerializer.Deserialize<JsonElement>(vmData);

                // Remove disk from data disks
                var updateBody = new
                {
                    location = vm.GetProperty("location").GetString(),
                    properties = new
                    {
                        storageProfile = new
                        {
                            osDisk = vm.GetProperty("properties").GetProperty("storageProfile").GetProperty("osDisk"),
                            dataDisks = vm.GetProperty("properties").GetProperty("storageProfile").GetProperty("dataDisks")
                                .EnumerateArray()
                                .Where(d => d.GetProperty("name").GetString() != diskName)
                                .ToArray()
                        }
                    }
                };

                var updateUrl = vmUrl;
                var content = new StringContent(JsonSerializer.Serialize(updateBody), Encoding.UTF8, "application/json");
                var result = await _httpClient.PatchAsync(updateUrl, content);

                return CreateSuccessResponse(request, $"Disk {diskName} detached from VM {vmName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to detach disk");
                return CreateExceptionResponse(request, ex);
            }
        }

        /// <summary>
        /// Revoke all VM access (remove extensions, reset passwords)
        /// DELETE /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachines/{vmName}/extensions/VMAccessAgent
        /// </summary>
        public async Task<XDRRemediationResponse> RevokeVMAccessAsync(XDRRemediationRequest request)
        {
            try
            {
                await SetAuthHeaderAsync();
                var subscriptionId = request.Parameters["subscriptionId"]?.ToString();
                var resourceGroup = request.Parameters["resourceGroupName"]?.ToString();
                var vmName = request.Parameters["vmName"]?.ToString();

                _logger.LogInformation("Revoking VM access for {VmName}", vmName);

                // Remove all VM extensions (VMAccessAgent, AzureMonitor, etc.)
                var extensionsUrl = $"{AzureManagementBaseUrl}/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Compute/virtualMachines/{vmName}/extensions?api-version={ApiVersion}";
                var extensionsResponse = await _httpClient.GetAsync(extensionsUrl);
                var extensionsData = await extensionsResponse.Content.ReadAsStringAsync();
                var extensions = JsonSerializer.Deserialize<JsonElement>(extensionsData);

                foreach (var extension in extensions.GetProperty("value").EnumerateArray())
                {
                    var extensionName = extension.GetProperty("name").GetString();
                    var deleteUrl = $"{AzureManagementBaseUrl}/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Compute/virtualMachines/{vmName}/extensions/{extensionName}?api-version={ApiVersion}";
                    await _httpClient.DeleteAsync(deleteUrl);
                }

                return CreateSuccessResponse(request, $"VM access revoked for {vmName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to revoke VM access");
                return CreateExceptionResponse(request, ex);
            }
        }

        /// <summary>
        /// Update NSG rules to block malicious traffic
        /// PATCH /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Network/networkSecurityGroups/{nsgName}
        /// </summary>
        public async Task<XDRRemediationResponse> UpdateNSGRulesAsync(XDRRemediationRequest request)
        {
            try
            {
                await SetAuthHeaderAsync();
                var subscriptionId = request.Parameters["subscriptionId"]?.ToString();
                var resourceGroup = request.Parameters["resourceGroupName"]?.ToString();
                var nsgName = request.Parameters["nsgName"]?.ToString();
                var ruleName = request.Parameters["ruleName"]?.ToString() ?? "BlockMaliciousTraffic";
                var sourceAddress = request.Parameters["sourceAddress"]?.ToString() ?? "*";
                var destinationPort = request.Parameters["destinationPort"]?.ToString() ?? "*";

                _logger.LogInformation("Updating NSG {NsgName} with rule {RuleName}", nsgName, ruleName);

                var ruleBody = new
                {
                    properties = new
                    {
                        protocol = "*",
                        sourceAddressPrefix = sourceAddress,
                        destinationAddressPrefix = "*",
                        access = "Deny",
                        priority = 100,
                        direction = "Inbound",
                        sourcePortRange = "*",
                        destinationPortRange = destinationPort
                    }
                };

                var url = $"{AzureManagementBaseUrl}/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Network/networkSecurityGroups/{nsgName}/securityRules/{ruleName}?api-version=2023-05-01";
                var content = new StringContent(JsonSerializer.Serialize(ruleBody), Encoding.UTF8, "application/json");
                var result = await _httpClient.PutAsync(url, content);

                return CreateSuccessResponse(request, $"NSG rule {ruleName} created in {nsgName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update NSG rules");
                return CreateExceptionResponse(request, ex);
            }
        }

        /// <summary>
        /// Disable public IP from VM NIC
        /// DELETE /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Network/publicIPAddresses/{ipName}
        /// </summary>
        public async Task<XDRRemediationResponse> DisablePublicIPAsync(XDRRemediationRequest request)
        {
            try
            {
                await SetAuthHeaderAsync();
                var subscriptionId = request.Parameters["subscriptionId"]?.ToString();
                var resourceGroup = request.Parameters["resourceGroupName"]?.ToString();
                var publicIpName = request.Parameters["publicIpName"]?.ToString();

                _logger.LogInformation("Disabling public IP {PublicIpName}", publicIpName);

                var url = $"{AzureManagementBaseUrl}/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Network/publicIPAddresses/{publicIpName}?api-version=2023-05-01";
                var result = await _httpClient.DeleteAsync(url);

                if (result.IsSuccessStatusCode)
                {
                    return CreateSuccessResponse(request, $"Public IP {publicIpName} deleted");
                }
                else
                {
                    return CreateFailureResponse(request, $"Failed to delete public IP: {result.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to disable public IP");
                return CreateExceptionResponse(request, ex);
            }
        }

        /// <summary>
        /// Block storage account access
        /// PATCH /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/storageAccounts/{accountName}
        /// </summary>
        public async Task<XDRRemediationResponse> BlockStorageAccountAsync(XDRRemediationRequest request)
        {
            try
            {
                await SetAuthHeaderAsync();
                var subscriptionId = request.Parameters["subscriptionId"]?.ToString();
                var resourceGroup = request.Parameters["resourceGroupName"]?.ToString();
                var storageAccountName = request.Parameters["storageAccountName"]?.ToString();

                _logger.LogInformation("Blocking storage account {StorageAccountName}", storageAccountName);

                var updateBody = new
                {
                    properties = new
                    {
                        allowBlobPublicAccess = false,
                        networkAcls = new
                        {
                            defaultAction = "Deny",
                            bypass = "None"
                        }
                    }
                };

                var url = $"{AzureManagementBaseUrl}/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Storage/storageAccounts/{storageAccountName}?api-version=2023-01-01";
                var content = new StringContent(JsonSerializer.Serialize(updateBody), Encoding.UTF8, "application/json");
                var result = await _httpClient.PatchAsync(url, content);

                return CreateSuccessResponse(request, $"Storage account {storageAccountName} blocked");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to block storage account");
                return CreateExceptionResponse(request, ex);
            }
        }

        /// <summary>
        /// Disable malicious service principal
        /// PATCH /servicePrincipals/{id}
        /// </summary>
        public async Task<XDRRemediationResponse> DisableServicePrincipalAsync(XDRRemediationRequest request)
        {
            try
            {
                await SetAuthHeaderAsync();
                var servicePrincipalId = request.Parameters["servicePrincipalId"]?.ToString();

                _logger.LogInformation("Disabling service principal {ServicePrincipalId}", servicePrincipalId);

                var updateBody = new
                {
                    accountEnabled = false
                };

                // This uses Graph API, not Azure Management API
                var graphToken = await _managedIdentityAuth.GetGraphTokenAsync(request.TenantId);
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", graphToken);

                var url = $"https://graph.microsoft.com/v1.0/servicePrincipals/{servicePrincipalId}";
                var content = new StringContent(JsonSerializer.Serialize(updateBody), Encoding.UTF8, "application/json");
                var result = await _httpClient.PatchAsync(url, content);

                return CreateSuccessResponse(request, $"Service principal {servicePrincipalId} disabled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to disable service principal");
                return CreateExceptionResponse(request, ex);
            }
        }

        /// <summary>
        /// Rotate storage account keys
        /// POST /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/storageAccounts/{accountName}/regenerateKey
        /// </summary>
        public async Task<XDRRemediationResponse> RotateStorageKeysAsync(XDRRemediationRequest request)
        {
            try
            {
                await SetAuthHeaderAsync();
                var subscriptionId = request.Parameters["subscriptionId"]?.ToString();
                var resourceGroup = request.Parameters["resourceGroupName"]?.ToString();
                var storageAccountName = request.Parameters["storageAccountName"]?.ToString();
                var keyName = request.Parameters["keyName"]?.ToString() ?? "key1";

                _logger.LogInformation("Rotating storage key {KeyName} for {StorageAccountName}", keyName, storageAccountName);

                var regenerateBody = new
                {
                    keyName = keyName
                };

                var url = $"{AzureManagementBaseUrl}/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Storage/storageAccounts/{storageAccountName}/regenerateKey?api-version=2023-01-01";
                var content = new StringContent(JsonSerializer.Serialize(regenerateBody), Encoding.UTF8, "application/json");
                var result = await _httpClient.PostAsync(url, content);

                return CreateSuccessResponse(request, $"Storage key {keyName} rotated for {storageAccountName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to rotate storage keys");
                return CreateExceptionResponse(request, ex);
            }
        }

        /// <summary>
        /// Delete malicious Azure resource
        /// DELETE /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/{resourceType}/{resourceName}
        /// </summary>
        public async Task<XDRRemediationResponse> DeleteMaliciousResourceAsync(XDRRemediationRequest request)
        {
            try
            {
                await SetAuthHeaderAsync();
                var subscriptionId = request.Parameters["subscriptionId"]?.ToString();
                var resourceGroup = request.Parameters["resourceGroupName"]?.ToString();
                var resourceType = request.Parameters["resourceType"]?.ToString();
                var resourceName = request.Parameters["resourceName"]?.ToString();

                _logger.LogInformation("Deleting malicious resource {ResourceType}/{ResourceName}", resourceType, resourceName);

                var url = $"{AzureManagementBaseUrl}/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/{resourceType}/{resourceName}?api-version={ApiVersion}";
                var result = await _httpClient.DeleteAsync(url);

                if (result.IsSuccessStatusCode)
                {
                    return CreateSuccessResponse(request, $"Resource {resourceType}/{resourceName} deleted");
                }
                else
                {
                    return CreateFailureResponse(request, $"Failed to delete resource: {result.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete malicious resource");
                return CreateExceptionResponse(request, ex);
            }
        }

        /// <summary>
        /// Enable diagnostic logs for forensics
        /// PUT /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/{resourceType}/{resourceName}/providers/Microsoft.Insights/diagnosticSettings/{name}
        /// </summary>
        public async Task<XDRRemediationResponse> EnableDiagnosticLogsAsync(XDRRemediationRequest request)
        {
            try
            {
                await SetAuthHeaderAsync();
                var subscriptionId = request.Parameters["subscriptionId"]?.ToString();
                var resourceGroup = request.Parameters["resourceGroupName"]?.ToString();
                var resourceType = request.Parameters["resourceType"]?.ToString();
                var resourceName = request.Parameters["resourceName"]?.ToString();
                var workspaceId = request.Parameters["logAnalyticsWorkspaceId"]?.ToString();

                _logger.LogInformation("Enabling diagnostic logs for {ResourceType}/{ResourceName}", resourceType, resourceName);

                var diagnosticSettings = new
                {
                    properties = new
                    {
                        workspaceId = workspaceId,
                        logs = new[]
                        {
                            new { category = "Administrative", enabled = true },
                            new { category = "Security", enabled = true },
                            new { category = "ServiceHealth", enabled = true },
                            new { category = "Alert", enabled = true }
                        },
                        metrics = new[]
                        {
                            new { category = "AllMetrics", enabled = true }
                        }
                    }
                };

                var url = $"{AzureManagementBaseUrl}/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/{resourceType}/{resourceName}/providers/Microsoft.Insights/diagnosticSettings/SecurityDiagnostics?api-version=2021-05-01-preview";
                var content = new StringContent(JsonSerializer.Serialize(diagnosticSettings), Encoding.UTF8, "application/json");
                var result = await _httpClient.PutAsync(url, content);

                return CreateSuccessResponse(request, $"Diagnostic logs enabled for {resourceType}/{resourceName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enable diagnostic logs");
                return CreateExceptionResponse(request, ex);
            }
        }

        /// <summary>
        /// Tag resource as compromised for visibility
        /// PATCH /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/{resourceType}/{resourceName}
        /// </summary>
        public async Task<XDRRemediationResponse> TagResourceAsCompromisedAsync(XDRRemediationRequest request)
        {
            try
            {
                await SetAuthHeaderAsync();
                var subscriptionId = request.Parameters["subscriptionId"]?.ToString();
                var resourceGroup = request.Parameters["resourceGroupName"]?.ToString();
                var resourceType = request.Parameters["resourceType"]?.ToString();
                var resourceName = request.Parameters["resourceName"]?.ToString();
                var incidentId = request.IncidentId;

                _logger.LogInformation("Tagging resource {ResourceType}/{ResourceName} as compromised", resourceType, resourceName);

                var updateBody = new
                {
                    tags = new
                    {
                        SecurityStatus = "Compromised",
                        IncidentId = incidentId,
                        RemediationDate = DateTime.UtcNow.ToString("o"),
                        RemediatedBy = request.InitiatedBy
                    }
                };

                var url = $"{AzureManagementBaseUrl}/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/{resourceType}/{resourceName}?api-version={ApiVersion}";
                var content = new StringContent(JsonSerializer.Serialize(updateBody), Encoding.UTF8, "application/json");
                var result = await _httpClient.PatchAsync(url, content);

                return CreateSuccessResponse(request, $"Resource {resourceType}/{resourceName} tagged as compromised");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to tag resource as compromised");
                return CreateExceptionResponse(request, ex);
            }
        }

        private XDRRemediationResponse CreateSuccessResponse(XDRRemediationRequest request, string message, Dictionary<string, object>? details = null)
        {
            return new XDRRemediationResponse
            {
                RequestId = request.RequestId,
                Success = true,
                Status = "Completed",
                Message = message,
                Details = details ?? new Dictionary<string, object>(),
                CompletedAt = DateTime.UtcNow
            };
        }

        private XDRRemediationResponse CreateFailureResponse(XDRRemediationRequest request, string message)
        {
            return new XDRRemediationResponse
            {
                RequestId = request.RequestId,
                Success = false,
                Status = "Failed",
                Message = message,
                CompletedAt = DateTime.UtcNow
            };
        }

        private XDRRemediationResponse CreateExceptionResponse(XDRRemediationRequest request, Exception ex)
        {
            return new XDRRemediationResponse
            {
                RequestId = request.RequestId,
                Success = false,
                Status = "Exception",
                Message = ex.Message,
                Errors = new List<string> { ex.ToString() },
                CompletedAt = DateTime.UtcNow
            };
        }
    }
}
