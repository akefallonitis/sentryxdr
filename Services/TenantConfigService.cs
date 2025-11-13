using SentryXDR.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace SentryXDR.Services
{
    public interface ITenantConfigService
    {
        Task<TenantConfiguration?> GetTenantConfigAsync(string tenantId);
        Task<bool> IsTenantActiveAsync(string tenantId);
        Task<Dictionary<XDRPlatform, bool>> GetEnabledPlatformsAsync(string tenantId);
    }

    public class TenantConfigService : ITenantConfigService
    {
        private readonly ILogger<TenantConfigService> _logger;
        private readonly ConcurrentDictionary<string, TenantConfiguration> _configCache = new();

        public TenantConfigService(ILogger<TenantConfigService> logger)
        {
            _logger = logger;
            InitializeDefaultConfigs();
        }

        public async Task<TenantConfiguration?> GetTenantConfigAsync(string tenantId)
        {
            if (_configCache.TryGetValue(tenantId, out var config))
            {
                return config;
            }

            // In production, this would load from Azure Table Storage or Cosmos DB
            _logger.LogWarning($"Tenant configuration not found for {tenantId}");
            return await Task.FromResult<TenantConfiguration?>(null);
        }

        public async Task<bool> IsTenantActiveAsync(string tenantId)
        {
            var config = await GetTenantConfigAsync(tenantId);
            return config?.IsActive ?? false;
        }

        public async Task<Dictionary<XDRPlatform, bool>> GetEnabledPlatformsAsync(string tenantId)
        {
            var config = await GetTenantConfigAsync(tenantId);
            return config?.EnabledPlatforms ?? new Dictionary<XDRPlatform, bool>();
        }

        private void InitializeDefaultConfigs()
        {
            // Default configuration - all platforms enabled
            // In production, load these from storage
            var defaultConfig = new TenantConfiguration
            {
                TenantId = "default",
                TenantName = "Default Tenant",
                IsActive = true,
                EnabledPlatforms = new Dictionary<XDRPlatform, bool>
                {
                    { XDRPlatform.MDE, true },
                    { XDRPlatform.MDO, true },
                    { XDRPlatform.MCAS, true },
                    { XDRPlatform.MDI, true },
                    { XDRPlatform.EntraID, true },
                    { XDRPlatform.Intune, true },
                    { XDRPlatform.Azure, true }
                }
            };

            _configCache.TryAdd("default", defaultConfig);
        }
    }
}
