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
                    XDRAction.SoftDeleteEmail => await _mdoService.SoftDeleteEmailAsync(request),
                    XDRAction.MoveEmailToJunk => await _mdoService.MoveEmailToJunkAsync(request),
                    XDRAction.SubmitEmailForAnalysis => await _mdoService.SubmitEmailForAnalysisAsync(request),
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
                    XDRAction.DisableUserAccount => await _entraService.DisableUserAccountAsync(request),
                    XDRAction.RevokeUserSignInSessions => await _entraService.RevokeUserSignInSessionsAsync(request),
                    XDRAction.ResetUserPassword => await _entraService.ResetUserPasswordAsync(request),
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
                    XDRAction.WipeDevice => await _intuneService.WipeDeviceAsync(request),
                    XDRAction.RetireDevice => await _intuneService.RetireDeviceAsync(request),
                    XDRAction.RemoteLockDevice => await _intuneService.RemoteLockDeviceAsync(request),
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
