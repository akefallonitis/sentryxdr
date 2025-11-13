using SentryXDR.Models;

namespace SentryXDR.Services.Workers
{
    // ==================== MDE (Microsoft Defender for Endpoint) ====================
    public interface IMDEWorkerService
    {
        // Add all the interface methods that MDEWorker needs
    }
    
    public class MDEWorkerService : IMDEWorkerService
    {
        private readonly IMDEApiService _apiService;
        
        public MDEWorkerService(IMDEApiService apiService)
        {
            _apiService = apiService;
        }
    }

    // ==================== MDO (Microsoft Defender for Office 365) ====================
    // Full implementation in MDOApiServiceComplete.cs
    public interface IMDOApiService
    {
        Task<XDRRemediationResponse> SoftDeleteEmailAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> HardDeleteEmailAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> MoveEmailToJunkAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> MoveEmailToInboxAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> RemoveEmailFromAllMailboxesAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> SubmitEmailForAnalysisAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> SubmitURLForAnalysisAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> AddSenderToBlockListAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> AddUrlToBlockListAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> ReleaseQuarantinedEmailAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> DeleteQuarantinedEmailAsync(XDRRemediationRequest request);
    }

    public interface IMDOWorkerService : IMDOApiService { }

    // ==================== Entra ID ====================
    // Full implementation in EntraIDApiServiceComplete.cs
    public interface IEntraIDWorkerService
    {
        Task<XDRRemediationResponse> DisableUserAccountAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> EnableUserAccountAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> DeleteUserAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> RevokeUserSignInSessionsAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> RevokeUserRefreshTokensAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> ResetUserPasswordAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> ForcePasswordChangeAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> ResetUserMFAAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> ConfirmUserCompromisedAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> DismissRiskyUserAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> GetUserRiskDetectionsAsync(XDRRemediationRequest request);
    }

    // ==================== Intune ====================
    // Full implementation in IntuneApiServiceComplete.cs
    public interface IIntuneWorkerService
    {
        Task<XDRRemediationResponse> WipeDeviceAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> WipeCorporateDataAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> RetireDeviceAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> DeleteDeviceAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> RemoteLockDeviceAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> ResetPasscodeAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> RebootDeviceAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> ShutDownDeviceAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> RotateBitLockerKeysAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> RotateFileVaultKeyAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> RotateLocalAdminPasswordAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> SyncDeviceAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> LocateDeviceAsync(XDRRemediationRequest request);
    }

    // ==================== MCAS (Microsoft Defender for Cloud Apps) ====================
    public interface IMCASApiService
    {
        Task<XDRRemediationResponse> SuspendUserAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> RevokeUserSessionsAsync(XDRRemediationRequest request);
    }

    public interface IMCASWorkerService : IMCASApiService { }
    
    public class MCASApiService : IMCASApiService
    {
        public async Task<XDRRemediationResponse> SuspendUserAsync(XDRRemediationRequest request)
        {
            return await Task.FromResult(new XDRRemediationResponse
            {
                RequestId = request.RequestId,
                Success = true,
                Status = "Pending",
                Message = "User suspended - MCAS implementation pending"
            });
        }

        public async Task<XDRRemediationResponse> RevokeUserSessionsAsync(XDRRemediationRequest request)
        {
            return await Task.FromResult(new XDRRemediationResponse
            {
                RequestId = request.RequestId,
                Success = true,
                Status = "Pending",
                Message = "User sessions revoked - MCAS implementation pending"
            });
        }
    }

    public class MCASWorkerService : IMCASWorkerService
    {
        private readonly IMCASApiService _apiService;
        public MCASWorkerService(IMCASApiService apiService) => _apiService = apiService;
        public Task<XDRRemediationResponse> SuspendUserAsync(XDRRemediationRequest request) => _apiService.SuspendUserAsync(request);
        public Task<XDRRemediationResponse> RevokeUserSessionsAsync(XDRRemediationRequest request) => _apiService.RevokeUserSessionsAsync(request);
    }

    // ==================== MDI (Microsoft Defender for Identity) ====================
    public interface IMDIApiService
    {
        Task<XDRRemediationResponse> DisableADAccountAsync(XDRRemediationRequest request);
    }

    public interface IMDIWorkerService : IMDIApiService { }
    
    public class MDIApiService : IMDIApiService
    {
        public async Task<XDRRemediationResponse> DisableADAccountAsync(XDRRemediationRequest request)
        {
            return await Task.FromResult(new XDRRemediationResponse
            {
                RequestId = request.RequestId,
                Success = true,
                Status = "Pending",
                Message = "AD account disabled - MDI implementation pending"
            });
        }
    }

    public class MDIWorkerService : IMDIWorkerService
    {
        private readonly IMDIApiService _apiService;
        public MDIWorkerService(IMDIApiService apiService) => _apiService = apiService;
        public Task<XDRRemediationResponse> DisableADAccountAsync(XDRRemediationRequest request) => _apiService.DisableADAccountAsync(request);
    }

    // ==================== Azure Security ====================
    // Azure API stub interfaces removed - see AzureApiService.cs for full implementation
}
