using SentryXDR.Services.Authentication;

namespace SentryXDR.Services
{
    public interface IGraphServiceFactory
    {
        Task<object> CreateGraphClientAsync(string tenantId);
        Task<object> CreateBetaGraphClientAsync(string tenantId);
    }

    public class GraphServiceFactory : IGraphServiceFactory
    {
        private readonly IMultiTenantAuthService _authService;

        public GraphServiceFactory(IMultiTenantAuthService authService)
        {
            _authService = authService;
        }

        public async Task<object> CreateGraphClientAsync(string tenantId)
        {
            // Placeholder - will be implemented with proper Kiota adapter
            await _authService.GetGraphTokenAsync(tenantId);
            return new object();
        }

        public async Task<object> CreateBetaGraphClientAsync(string tenantId)
        {
            // Placeholder - will be implemented with proper Kiota adapter
            await _authService.GetGraphBetaTokenAsync(tenantId);
            return new object();
        }
    }
}
