using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SentryXDR.Models;
using SentryXDR.Services.Workers;

namespace SentryXDR.Functions.Workers
{
    /// <summary>
    /// Microsoft Defender for Office 365 Worker
    /// Handles email security and threat submission actions
    /// </summary>
    public class MDOWorker
    {
        private readonly ILogger<MDOWorker> _logger;
        private readonly IMDOWorkerService _mdoService;

        public MDOWorker(ILogger<MDOWorker> logger, IMDOWorkerService mdoService)
        {
            _logger = logger;
            _mdoService = mdoService;
        }

        [Function("MDOWorkerActivity")]
        public async Task<XDRRemediationResponse> RunAsync([ActivityTrigger] XDRRemediationRequest request)
        {
            _logger.LogInformation("MDO Worker processing - Action: {Action}", request.Action);
            
            return request.Action switch
            {
                XDRAction.SoftDeleteEmail => await _mdoService.SoftDeleteEmailAsync(request),
                XDRAction.MoveEmailToJunk => await _mdoService.MoveEmailToJunkAsync(request),
                XDRAction.SubmitEmailForAnalysis => await _mdoService.SubmitEmailForAnalysisAsync(request),
                _ => CreateUnsupportedResponse(request)
            };
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
    }

    /// <summary>
    /// Microsoft Defender for Cloud Apps Worker
    /// </summary>
    public class MCASWorker
    {
        private readonly ILogger<MCASWorker> _logger;
        private readonly IMCASWorkerService _mcasService;

        public MCASWorker(ILogger<MCASWorker> logger, IMCASWorkerService mcasService)
        {
            _logger = logger;
            _mcasService = mcasService;
        }

        [Function("MCASWorkerActivity")]
        public async Task<XDRRemediationResponse> RunAsync([ActivityTrigger] XDRRemediationRequest request)
        {
            _logger.LogInformation("MCAS Worker processing - Action: {Action}", request.Action);
            
            return request.Action switch
            {
                XDRAction.SuspendUser => await _mcasService.SuspendUserAsync(request),
                XDRAction.RevokeUserSessions => await _mcasService.RevokeUserSessionsAsync(request),
                _ => CreateUnsupportedResponse(request)
            };
        }

        private XDRRemediationResponse CreateUnsupportedResponse(XDRRemediationRequest request)
        {
            return new XDRRemediationResponse
            {
                RequestId = request.RequestId,
                Success = false,
                Status = "ActionNotSupported",
                Message = $"Action {request.Action} not supported by MCAS Worker"
            };
        }
    }

    /// <summary>
    /// Microsoft Defender for Identity Worker
    /// </summary>
    public class MDIWorker
    {
        private readonly ILogger<MDIWorker> _logger;
        private readonly IMDIWorkerService _mdiService;

        public MDIWorker(ILogger<MDIWorker> logger, IMDIWorkerService mdiService)
        {
            _logger = logger;
            _mdiService = mdiService;
        }

        [Function("MDIWorkerActivity")]
        public async Task<XDRRemediationResponse> RunAsync([ActivityTrigger] XDRRemediationRequest request)
        {
            _logger.LogInformation("MDI Worker processing - Action: {Action}", request.Action);
            
            return request.Action switch
            {
                XDRAction.DisableADAccount => await _mdiService.DisableADAccountAsync(request),
                _ => CreateUnsupportedResponse(request)
            };
        }

        private XDRRemediationResponse CreateUnsupportedResponse(XDRRemediationRequest request)
        {
            return new XDRRemediationResponse
            {
                RequestId = request.RequestId,
                Success = false,
                Status = "ActionNotSupported",
                Message = $"Action {request.Action} not supported by MDI Worker"
            };
        }
    }

    /// <summary>
    /// Microsoft Entra ID Worker
    /// </summary>
    public class EntraIDWorker
    {
        private readonly ILogger<EntraIDWorker> _logger;
        private readonly IEntraIDWorkerService _entraService;

        public EntraIDWorker(ILogger<EntraIDWorker> logger, IEntraIDWorkerService entraService)
        {
            _logger = logger;
            _entraService = entraService;
        }

        [Function("EntraIDWorkerActivity")]
        public async Task<XDRRemediationResponse> RunAsync([ActivityTrigger] XDRRemediationRequest request)
        {
            _logger.LogInformation("Entra ID Worker processing - Action: {Action}", request.Action);
            
            return request.Action switch
            {
                XDRAction.DisableUserAccount => await _entraService.DisableUserAccountAsync(request),
                XDRAction.RevokeUserSignInSessions => await _entraService.RevokeUserSignInSessionsAsync(request),
                XDRAction.ResetUserPassword => await _entraService.ResetUserPasswordAsync(request),
                _ => CreateUnsupportedResponse(request)
            };
        }

        private XDRRemediationResponse CreateUnsupportedResponse(XDRRemediationRequest request)
        {
            return new XDRRemediationResponse
            {
                RequestId = request.RequestId,
                Success = false,
                Status = "ActionNotSupported",
                Message = $"Action {request.Action} not supported by Entra ID Worker"
            };
        }
    }

    /// <summary>
    /// Microsoft Intune Worker
    /// </summary>
    public class IntuneWorker
    {
        private readonly ILogger<IntuneWorker> _logger;
        private readonly IIntuneWorkerService _intuneService;

        public IntuneWorker(ILogger<IntuneWorker> logger, IIntuneWorkerService intuneService)
        {
            _logger = logger;
            _intuneService = intuneService;
        }

        [Function("IntuneWorkerActivity")]
        public async Task<XDRRemediationResponse> RunAsync([ActivityTrigger] XDRRemediationRequest request)
        {
            _logger.LogInformation("Intune Worker processing - Action: {Action}", request.Action);
            
            return request.Action switch
            {
                XDRAction.WipeDevice => await _intuneService.WipeDeviceAsync(request),
                XDRAction.RetireDevice => await _intuneService.RetireDeviceAsync(request),
                XDRAction.RemoteLockDevice => await _intuneService.RemoteLockDeviceAsync(request),
                _ => CreateUnsupportedResponse(request)
            };
        }

        private XDRRemediationResponse CreateUnsupportedResponse(XDRRemediationRequest request)
        {
            return new XDRRemediationResponse
            {
                RequestId = request.RequestId,
                Success = false,
                Status = "ActionNotSupported",
                Message = $"Action {request.Action} not supported by Intune Worker"
            };
        }
    }

    /// <summary>
    /// Azure Security Worker
    /// </summary>
    public class AzureWorker
    {
        private readonly ILogger<AzureWorker> _logger;
        private readonly IAzureWorkerService _azureService;

        public AzureWorker(ILogger<AzureWorker> logger, IAzureWorkerService azureService)
        {
            _logger = logger;
            _azureService = azureService;
        }

        [Function("AzureWorkerActivity")]
        public async Task<XDRRemediationResponse> RunAsync([ActivityTrigger] XDRRemediationRequest request)
        {
            _logger.LogInformation("Azure Worker processing - Action: {Action}", request.Action);
            
            return request.Action switch
            {
                XDRAction.IsolateVMNetwork => await _azureService.IsolateVMNetworkAsync(request),
                XDRAction.StopVM => await _azureService.StopVMAsync(request),
                XDRAction.RestartVM => await _azureService.RestartVMAsync(request),
                XDRAction.DeleteVM => await _azureService.DeleteVMAsync(request),
                XDRAction.SnapshotVM => await _azureService.SnapshotVMAsync(request),
                XDRAction.DetachDisk => await _azureService.DetachDiskAsync(request),
                XDRAction.RevokeVMAccess => await _azureService.RevokeVMAccessAsync(request),
                XDRAction.UpdateNSGRules => await _azureService.UpdateNSGRulesAsync(request),
                XDRAction.DisablePublicIP => await _azureService.DisablePublicIPAsync(request),
                XDRAction.BlockStorageAccount => await _azureService.BlockStorageAccountAsync(request),
                XDRAction.DisableServicePrincipal => await _azureService.DisableServicePrincipalAsync(request),
                XDRAction.RotateStorageKeys => await _azureService.RotateStorageKeysAsync(request),
                XDRAction.DeleteMaliciousResource => await _azureService.DeleteMaliciousResourceAsync(request),
                XDRAction.EnableDiagnosticLogs => await _azureService.EnableDiagnosticLogsAsync(request),
                XDRAction.TagResourceAsCompromised => await _azureService.TagResourceAsCompromisedAsync(request),
                _ => CreateUnsupportedResponse(request)
            };
        }

        private XDRRemediationResponse CreateUnsupportedResponse(XDRRemediationRequest request)
        {
            return new XDRRemediationResponse
            {
                RequestId = request.RequestId,
                Success = false,
                Status = "ActionNotSupported",
                Message = $"Action {request.Action} not supported by Azure Worker"
            };
        }
    }
}
