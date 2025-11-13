using Microsoft.Identity.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace SentryXDR.Services.Authentication
{
    public interface IMultiTenantAuthService
    {
        Task<string> GetAccessTokenAsync(string tenantId, string resource);
        Task<string> GetGraphTokenAsync(string tenantId);
        Task<string> GetGraphBetaTokenAsync(string tenantId);
        Task<string> GetMDETokenAsync(string tenantId);
        Task<string> GetAzureManagementTokenAsync(string tenantId);
    }

    public class MultiTenantAuthService : IMultiTenantAuthService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<MultiTenantAuthService> _logger;
        private readonly ConcurrentDictionary<string, IConfidentialClientApplication> _clientApps = new();
        private readonly ConcurrentDictionary<string, TokenCacheItem> _tokenCache = new();

        public MultiTenantAuthService(
            IConfiguration configuration,
            ILogger<MultiTenantAuthService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<string> GetAccessTokenAsync(string tenantId, string resource)
        {
            var cacheKey = $"{tenantId}:{resource}";
            
            // Check cache first
            if (_tokenCache.TryGetValue(cacheKey, out var cached) && 
                cached.ExpiresOn > DateTimeOffset.UtcNow.AddMinutes(5))
            {
                _logger.LogDebug($"Using cached token for tenant {tenantId}");
                return cached.Token;
            }

            var clientApp = GetOrCreateClientApp(tenantId);
            var scopes = new[] { $"{resource}/.default" };

            try
            {
                var result = await clientApp
                    .AcquireTokenForClient(scopes)
                    .ExecuteAsync();

                // Cache the token
                _tokenCache[cacheKey] = new TokenCacheItem
                {
                    Token = result.AccessToken,
                    ExpiresOn = result.ExpiresOn
                };

                _logger.LogInformation($"Successfully acquired token for tenant {tenantId}, resource {resource}");
                return result.AccessToken;
            }
            catch (MsalServiceException ex)
            {
                _logger.LogError(ex, $"Failed to acquire token for tenant {tenantId}, resource {resource}");
                throw new UnauthorizedAccessException($"Failed to authenticate for tenant {tenantId}", ex);
            }
        }

        public async Task<string> GetGraphTokenAsync(string tenantId)
        {
            return await GetAccessTokenAsync(tenantId, "https://graph.microsoft.com");
        }

        public async Task<string> GetGraphBetaTokenAsync(string tenantId)
        {
            return await GetAccessTokenAsync(tenantId, "https://graph.microsoft.com");
        }

        public async Task<string> GetMDETokenAsync(string tenantId)
        {
            return await GetAccessTokenAsync(tenantId, "https://api.securitycenter.microsoft.com");
        }

        public async Task<string> GetAzureManagementTokenAsync(string tenantId)
        {
            return await GetAccessTokenAsync(tenantId, "https://management.azure.com");
        }

        private IConfidentialClientApplication GetOrCreateClientApp(string tenantId)
        {
            return _clientApps.GetOrAdd(tenantId, tid =>
            {
                var clientId = _configuration["MultiTenant:ClientId"];
                var clientSecret = _configuration["MultiTenant:ClientSecret"];
                
                if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
                {
                    throw new InvalidOperationException("Multi-tenant app credentials not configured");
                }

                var authority = $"https://login.microsoftonline.com/{tid}";

                var app = ConfidentialClientApplicationBuilder
                    .Create(clientId)
                    .WithClientSecret(clientSecret)
                    .WithAuthority(authority)
                    .WithLegacyCacheCompatibility(false)
                    .Build();

                _logger.LogInformation($"Created new confidential client for tenant {tid}");
                return app;
            });
        }

        private class TokenCacheItem
        {
            public string Token { get; set; } = string.Empty;
            public DateTimeOffset ExpiresOn { get; set; }
        }
    }
}
