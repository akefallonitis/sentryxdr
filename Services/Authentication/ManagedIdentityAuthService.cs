using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;

namespace SentryXDR.Services.Authentication
{
    /// <summary>
    /// Managed Identity authentication service for Azure resources
    /// Supports both System-assigned and User-assigned Managed Identities
    /// </summary>
    public interface IManagedIdentityAuthService
    {
        Task<string> GetAzureManagementTokenAsync();
        Task<string> GetAzureTokenAsync(string resource);
        Task<string> GetAzureTokenWithScopeAsync(string[] scopes);
        Task<string> GetGraphTokenAsync(string tenantId);
        Task<bool> ValidateRBACPermissionsAsync(string subscriptionId, string[] requiredRoles);
        Task<string> GetManagedIdentityObjectIdAsync();
        Task<string> GetManagedIdentityClientIdAsync();
        Task<bool> IsManagedIdentityAvailableAsync();
    }

    public class ManagedIdentityAuthService : IManagedIdentityAuthService
    {
        private readonly ILogger<ManagedIdentityAuthService> _logger;
        private readonly IConfiguration _configuration;
        private readonly DefaultAzureCredential _defaultCredential;
        private readonly ManagedIdentityCredential? _managedIdentityCredential;
        private readonly bool _useManagedIdentity;
        private readonly string? _userAssignedClientId;

        public ManagedIdentityAuthService(
            ILogger<ManagedIdentityAuthService> logger,
            IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;

            // Check if Managed Identity should be used
            _useManagedIdentity = configuration.GetValue<bool>("Azure:UseManagedIdentity", true);
            _userAssignedClientId = configuration["Azure:ManagedIdentity:ClientId"];

            // Initialize credentials
            if (_useManagedIdentity)
            {
                try
                {
                    if (!string.IsNullOrEmpty(_userAssignedClientId))
                    {
                        // User-assigned Managed Identity
                        _managedIdentityCredential = new ManagedIdentityCredential(_userAssignedClientId);
                        _logger.LogInformation("Initialized User-Assigned Managed Identity: {ClientId}", 
                            _userAssignedClientId);
                    }
                    else
                    {
                        // System-assigned Managed Identity
                        _managedIdentityCredential = new ManagedIdentityCredential();
                        _logger.LogInformation("Initialized System-Assigned Managed Identity");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to initialize Managed Identity, falling back to DefaultAzureCredential");
                }
            }

            // Default credential (tries multiple auth methods in order)
            _defaultCredential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                ExcludeVisualStudioCredential = false,
                ExcludeVisualStudioCodeCredential = false,
                ExcludeAzureCliCredential = false,
                ExcludeAzurePowerShellCredential = false,
                ExcludeInteractiveBrowserCredential = true,
                ManagedIdentityClientId = _userAssignedClientId
            });
        }

        public async Task<string> GetAzureManagementTokenAsync()
        {
            return await GetAzureTokenAsync("https://management.azure.com");
        }

        public async Task<string> GetAzureTokenAsync(string resource)
        {
            try
            {
                var scope = resource.EndsWith("/.default") ? resource : $"{resource}/.default";
                var tokenRequestContext = new TokenRequestContext(new[] { scope });

                AccessToken token;

                // Try Managed Identity first if available
                if (_managedIdentityCredential != null)
                {
                    try
                    {
                        token = await _managedIdentityCredential.GetTokenAsync(tokenRequestContext);
                        _logger.LogDebug("Acquired token using Managed Identity for resource: {Resource}", resource);
                        return token.Token;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Managed Identity failed, falling back to DefaultAzureCredential");
                    }
                }

                // Fallback to DefaultAzureCredential
                token = await _defaultCredential.GetTokenAsync(tokenRequestContext);
                _logger.LogDebug("Acquired token using DefaultAzureCredential for resource: {Resource}", resource);
                return token.Token;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to acquire Azure token for resource: {Resource}", resource);
                throw new UnauthorizedAccessException($"Failed to authenticate with Azure for resource: {resource}", ex);
            }
        }

        public async Task<string> GetAzureTokenWithScopeAsync(string[] scopes)
        {
            try
            {
                var tokenRequestContext = new TokenRequestContext(scopes);

                AccessToken token;

                if (_managedIdentityCredential != null)
                {
                    try
                    {
                        token = await _managedIdentityCredential.GetTokenAsync(tokenRequestContext);
                        _logger.LogDebug("Acquired token using Managed Identity for scopes: {Scopes}", 
                            string.Join(", ", scopes));
                        return token.Token;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Managed Identity failed, falling back");
                    }
                }

                token = await _defaultCredential.GetTokenAsync(tokenRequestContext);
                _logger.LogDebug("Acquired token for scopes: {Scopes}", string.Join(", ", scopes));
                return token.Token;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to acquire token with scopes");
                throw;
            }
        }

        /// <summary>
        /// Get Microsoft Graph API token for tenant operations
        /// </summary>
        public async Task<string> GetGraphTokenAsync(string tenantId)
        {
            try
            {
                // Microsoft Graph API resource
                var graphResource = "https://graph.microsoft.com";
                var scope = $"{graphResource}/.default";
                var tokenRequestContext = new TokenRequestContext(new[] { scope });

                AccessToken token;

                // Try Managed Identity first
                if (_managedIdentityCredential != null)
                {
                    try
                    {
                        token = await _managedIdentityCredential.GetTokenAsync(tokenRequestContext);
                        _logger.LogDebug("Acquired Graph token using Managed Identity for tenant: {TenantId}", tenantId);
                        return token.Token;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Managed Identity failed for Graph token, falling back");
                    }
                }

                // Fall back to default credential
                token = await _defaultCredential.GetTokenAsync(tokenRequestContext);
                _logger.LogDebug("Acquired Graph token for tenant: {TenantId}", tenantId);
                return token.Token;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to acquire Graph token for tenant: {TenantId}", tenantId);
                throw;
            }
        }

        public async Task<bool> ValidateRBACPermissionsAsync(string subscriptionId, string[] requiredRoles)
        {
            try
            {
                var token = await GetAzureManagementTokenAsync();
                
                // Get Managed Identity Object ID
                var objectId = await GetManagedIdentityObjectIdAsync();

                // Call Azure Management API to check role assignments
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var url = $"https://management.azure.com/subscriptions/{subscriptionId}/providers/Microsoft.Authorization/roleAssignments?api-version=2022-04-01&$filter=principalId eq '{objectId}'";
                var response = await httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to retrieve RBAC role assignments. Status: {Status}", 
                        response.StatusCode);
                    return false;
                }

                var content = await response.Content.ReadAsStringAsync();
                
                // Parse and check if any required role is assigned
                // Simplified - in production, parse JSON and verify role definitions
                foreach (var role in requiredRoles)
                {
                    if (content.Contains(role, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation("Found required role: {Role}", role);
                        return true;
                    }
                }

                _logger.LogWarning("None of the required roles found: {Roles}", string.Join(", ", requiredRoles));
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating RBAC permissions for subscription: {SubscriptionId}", 
                    subscriptionId);
                return false;
            }
        }

        public async Task<string> GetManagedIdentityObjectIdAsync()
        {
            try
            {
                var token = await GetAzureManagementTokenAsync();

                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                // Get instance metadata to retrieve identity info
                var response = await httpClient.GetAsync(
                    "http://169.254.169.254/metadata/identity/oauth2/token?api-version=2018-02-01&resource=https://management.azure.com");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    // Parse JSON to get object_id
                    // Simplified - use JSON parser in production
                    var match = System.Text.RegularExpressions.Regex.Match(content, "\"object_id\":\"([^\"]+)\"");
                    if (match.Success)
                    {
                        return match.Groups[1].Value;
                    }
                }

                _logger.LogWarning("Could not retrieve Managed Identity Object ID");
                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving Managed Identity Object ID");
                return string.Empty;
            }
        }

        public async Task<string> GetManagedIdentityClientIdAsync()
        {
            // If user-assigned, return configured client ID
            if (!string.IsNullOrEmpty(_userAssignedClientId))
            {
                return _userAssignedClientId;
            }

            try
            {
                var token = await GetAzureManagementTokenAsync();

                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var response = await httpClient.GetAsync(
                    "http://169.254.169.254/metadata/identity/oauth2/token?api-version=2018-02-01&resource=https://management.azure.com");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var match = System.Text.RegularExpressions.Regex.Match(content, "\"client_id\":\"([^\"]+)\"");
                    if (match.Success)
                    {
                        return match.Groups[1].Value;
                    }
                }

                _logger.LogWarning("Could not retrieve Managed Identity Client ID");
                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving Managed Identity Client ID");
                return string.Empty;
            }
        }

        public async Task<bool> IsManagedIdentityAvailableAsync()
        {
            try
            {
                // Try to get a token to verify MI is available
                var token = await GetAzureManagementTokenAsync();
                return !string.IsNullOrEmpty(token);
            }
            catch
            {
                return false;
            }
        }
    }
}
