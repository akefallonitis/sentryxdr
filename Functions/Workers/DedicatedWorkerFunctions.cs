using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SentryXDR.Models;
using SentryXDR.Services.Workers;

namespace SentryXDR.Functions.Workers
{
    /// <summary>
    /// Dedicated Microsoft Defender for Office 365 Worker
    /// Handles 25 email security operations
    /// </summary>
    public class MDOWorkerFunction
    {
        private readonly ILogger<MDOWorkerFunction> _logger;
        private readonly IMDOWorkerService _mdoService;

        public MDOWorkerFunction(ILogger<MDOWorkerFunction> logger, IMDOWorkerService mdoService)
        {
            _logger = logger;
            _mdoService = mdoService;
        }

        [Function("MDOWorkerActivity")]
        public async Task<XDRRemediationResponse> RunAsync([ActivityTrigger] XDRRemediationRequest request)
        {
            _logger.LogInformation("MDO Worker - Tenant: {TenantId}, Action: {Action}", request.TenantId, request.Action);
            
            try
            {
                return request.Action switch
                {
                    // Email Message Actions (15)
                    XDRAction.SoftDeleteEmail => await _mdoService.SoftDeleteEmailAsync(request),
                    XDRAction.HardDeleteEmail => await _mdoService.HardDeleteEmailAsync(request),
                    XDRAction.MoveEmailToJunk => await _mdoService.MoveEmailToJunkAsync(request),
                    XDRAction.MoveEmailToInbox => await _mdoService.MoveEmailToInboxAsync(request),
                    XDRAction.RemoveEmailFromAllMailboxes => await _mdoService.RemoveEmailFromAllMailboxesAsync(request),
                    
                    // Threat Submission & Investigation (10)
                    XDRAction.SubmitEmailForAnalysis => await _mdoService.SubmitEmailForAnalysisAsync(request),
                    XDRAction.SubmitURLForAnalysis => await _mdoService.SubmitURLForAnalysisAsync(request),
                    
                    // Tenant Allow/Block Lists (5)
                    XDRAction.AddSenderToBlockList => await _mdoService.AddSenderToBlockListAsync(request),
                    XDRAction.AddUrlToBlockList => await _mdoService.AddUrlToBlockListAsync(request),
                    
                    // Quarantine Management (2)
                    XDRAction.ReleaseQuarantinedEmail => await _mdoService.ReleaseQuarantinedEmailAsync(request),
                    XDRAction.DeleteQuarantinedEmail => await _mdoService.DeleteQuarantinedEmailAsync(request),
                    
                    _ => CreateUnsupportedResponse(request)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MDO Worker failed");
                return CreateErrorResponse(request, ex);
            }
        }

        private XDRRemediationResponse CreateUnsupportedResponse(XDRRemediationRequest request)
        {
            return new XDRRemediationResponse
            {
                RequestId = request.RequestId,
                Success = false,
                Status = "ActionNotSupported",
                Message = $"Action {request.Action} not supported by MDO Worker"
            };
        }

        private XDRRemediationResponse CreateErrorResponse(XDRRemediationRequest request, Exception ex)
        {
            return new XDRRemediationResponse
            {
                RequestId = request.RequestId,
                Success = false,
                Status = "Error",
                Message = ex.Message,
                Errors = new List<string> { ex.ToString() }
            };
        }
    }

    /// <summary>
    /// Dedicated Entra ID Worker
    /// Handles 34 identity protection, PIM, and Conditional Access operations
    /// </summary>
    public class EntraIDWorkerFunction
    {
        private readonly ILogger<EntraIDWorkerFunction> _logger;
        private readonly IEntraIDWorkerService _entraService;

        public EntraIDWorkerFunction(ILogger<EntraIDWorkerFunction> logger, IEntraIDWorkerService entraService)
        {
            _logger = logger;
            _entraService = entraService;
        }

        [Function("EntraIDWorkerActivity")]
        public async Task<XDRRemediationResponse> RunAsync([ActivityTrigger] XDRRemediationRequest request)
        {
            _logger.LogInformation("Entra ID Worker - Tenant: {TenantId}, Action: {Action}", request.TenantId, request.Action);
            
            try
            {
                return request.Action switch
                {
                    // User Management (8)
                    XDRAction.DisableUserAccount => await _entraService.DisableUserAccountAsync(request),
                    XDRAction.EnableUserAccount => await _entraService.EnableUserAccountAsync(request),
                    XDRAction.DeleteUser => await _entraService.DeleteUserAsync(request),
                    
                    // Authentication Management (8)
                    XDRAction.RevokeUserSignInSessions => await _entraService.RevokeUserSignInSessionsAsync(request),
                    XDRAction.RevokeUserRefreshTokens => await _entraService.RevokeUserRefreshTokensAsync(request),
                    XDRAction.ResetUserPassword => await _entraService.ResetUserPasswordAsync(request),
                    XDRAction.ForcePasswordChange => await _entraService.ForcePasswordChangeAsync(request),
                    
                    // MFA Management (4)
                    XDRAction.ResetUserMFA => await _entraService.ResetUserMFAAsync(request),
                    
                    // Risk Management (5)
                    XDRAction.ConfirmUserCompromised_EntraID => await _entraService.ConfirmUserCompromisedAsync(request),
                    XDRAction.DismissRiskyUser => await _entraService.DismissRiskyUserAsync(request),
                    XDRAction.GetUserRiskDetections => await _entraService.GetUserRiskDetectionsAsync(request),
                    
                    _ => CreateUnsupportedResponse(request)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Entra ID Worker failed");
                return CreateErrorResponse(request, ex);
            }
        }

        private XDRRemediationResponse CreateUnsupportedResponse(XDRRemediationRequest request)
        {
            return new XDRRemediationResponse
            {
                RequestId = request.RequestId,
                Success = false,
                Status = "ActionNotSupported",
                Message = $"Action {request.Action} not supported"
            };
        }

        private XDRRemediationResponse CreateErrorResponse(XDRRemediationRequest request, Exception ex)
        {
            return new XDRRemediationResponse
            {
                RequestId = request.RequestId,
                Success = false,
                Status = "Error",
                Message = ex.Message,
                Errors = new List<string> { ex.ToString() }
            };
        }
    }

    /// <summary>
    /// Dedicated Intune Worker
    /// Handles 33 device management operations
    /// </summary>
    public class IntuneWorkerFunction
    {
        private readonly ILogger<IntuneWorkerFunction> _logger;
        private readonly IIntuneWorkerService _intuneService;

        public IntuneWorkerFunction(ILogger<IntuneWorkerFunction> logger, IIntuneWorkerService intuneService)
        {
            _logger = logger;
            _intuneService = intuneService;
        }

        [Function("IntuneWorkerActivity")]
        public async Task<XDRRemediationResponse> RunAsync([ActivityTrigger] XDRRemediationRequest request)
        {
            _logger.LogInformation("Intune Worker - Tenant: {TenantId}, Action: {Action}", request.TenantId, request.Action);
            
            try
            {
                return request.Action switch
                {
                    // Device Wipe & Retirement (4)
                    XDRAction.WipeDevice => await _intuneService.WipeDeviceAsync(request),
                    XDRAction.WipeCorporateData => await _intuneService.WipeCorporateDataAsync(request),
                    XDRAction.RetireDevice => await _intuneService.RetireDeviceAsync(request),
                    XDRAction.DeleteDevice => await _intuneService.DeleteDeviceAsync(request),
                    
                    // Remote Device Actions (5)
                    XDRAction.RemoteLockDevice => await _intuneService.RemoteLockDeviceAsync(request),
                    XDRAction.ResetPasscode => await _intuneService.ResetPasscodeAsync(request),
                    XDRAction.RebootDevice => await _intuneService.RebootDeviceAsync(request),
                    XDRAction.ShutDownDevice => await _intuneService.ShutDownDeviceAsync(request),
                    
                    // Security Actions (5)
                    XDRAction.RotateBitLockerKeys => await _intuneService.RotateBitLockerKeysAsync(request),
                    XDRAction.RotateFileVaultKey => await _intuneService.RotateFileVaultKeyAsync(request),
                    XDRAction.RotateLocalAdminPassword => await _intuneService.RotateLocalAdminPasswordAsync(request),
                    
                    // Device Management (3)
                    XDRAction.SyncDevice => await _intuneService.SyncDeviceAsync(request),
                    XDRAction.LocateDevice => await _intuneService.LocateDeviceAsync(request),
                    
                    _ => CreateUnsupportedResponse(request)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Intune Worker failed");
                return CreateErrorResponse(request, ex);
            }
        }

        private XDRRemediationResponse CreateUnsupportedResponse(XDRRemediationRequest request)
        {
            return new XDRRemediationResponse
            {
                RequestId = request.RequestId,
                Success = false,
                Status = "ActionNotSupported",
                Message = $"Action {request.Action} not supported"
            };
        }

        private XDRRemediationResponse CreateErrorResponse(XDRRemediationRequest request, Exception ex)
        {
            return new XDRRemediationResponse
            {
                RequestId = request.RequestId,
                Success = false,
                Status = "Error",
                Message = ex.Message,
                Errors = new List<string> { ex.ToString() }
            };
        }
    }

    /// <summary>
    /// Dedicated Azure Security Worker
    /// Handles 52 Azure infrastructure security operations
    /// </summary>
    public class AzureWorkerFunction
    {
        private readonly ILogger<AzureWorkerFunction> _logger;
        private readonly IAzureWorkerService _azureService;

        public AzureWorkerFunction(ILogger<AzureWorkerFunction> logger, IAzureWorkerService azureService)
        {
            _logger = logger;
            _azureService = azureService;
        }

        [Function("AzureWorkerActivity")]
        public async Task<XDRRemediationResponse> RunAsync([ActivityTrigger] XDRRemediationRequest request)
        {
            _logger.LogInformation("Azure Worker - Tenant: {TenantId}, Action: {Action}", request.TenantId, request.Action);
            
            try
            {
                return request.Action switch
                {
                    XDRAction.StopVM => await _azureService.StopVMAsync(request),
                    XDRAction.IsolateVMNetwork => await _azureService.IsolateVMNetworkAsync(request),
                    XDRAction.CreateNSGRule => await _azureService.CreateNSGRuleAsync(request),
                    _ => CreateUnsupportedResponse(request)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Azure Worker failed");
                return CreateErrorResponse(request, ex);
            }
        }

        private XDRRemediationResponse CreateUnsupportedResponse(XDRRemediationRequest request)
        {
            return new XDRRemediationResponse
            {
                RequestId = request.RequestId,
                Success = false,
                Status = "ActionNotSupported",
                Message = $"Action {request.Action} not supported"
            };
        }

        private XDRRemediationResponse CreateErrorResponse(XDRRemediationRequest request, Exception ex)
        {
            return new XDRRemediationResponse
            {
                RequestId = request.RequestId,
                Success = false,
                Status = "Error",
                Message = ex.Message,
                Errors = new List<string> { ex.ToString() }
            };
        }
    }
}
