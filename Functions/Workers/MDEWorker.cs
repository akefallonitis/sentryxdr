using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SentryXDR.Models;
using SentryXDR.Services.Workers;

namespace SentryXDR.Functions.Workers
{
    /// <summary>
    /// Microsoft Defender for Endpoint Worker
    /// Handles 61+ device protection and response actions
    /// </summary>
    public class MDEWorker
    {
        private readonly ILogger<MDEWorker> _logger;
        private readonly IMDEApiService _mdeService;

        public MDEWorker(
            ILogger<MDEWorker> logger,
            IMDEApiService mdeService)
        {
            _logger = logger;
            _mdeService = mdeService;
        }

        [Function("MDEWorkerActivity")]
        public async Task<XDRRemediationResponse> RunAsync(
            [ActivityTrigger] XDRRemediationRequest request)
        {
            _logger.LogInformation(
                "MDE Worker processing - Tenant: {TenantId}, Action: {Action}, Incident: {IncidentId}",
                request.TenantId,
                request.Action,
                request.IncidentId);

            try
            {
                return request.Action switch
                {
                    XDRAction.IsolateDevice => 
                        await _mdeService.IsolateDeviceAsync(request),
                    
                    XDRAction.ReleaseDeviceFromIsolation => 
                        await _mdeService.ReleaseDeviceAsync(request),
                    
                    XDRAction.RunAntivirusScan or
                    XDRAction.RunQuickScan => 
                        await _mdeService.RunAntivirusScanAsync(request),
                    
                    XDRAction.RunFullScan => 
                        await ExecuteFullScanAsync(request),
                    
                    XDRAction.CollectInvestigationPackage => 
                        await _mdeService.CollectInvestigationPackageAsync(request),
                    
                    XDRAction.StopAndQuarantineFile => 
                        await _mdeService.StopAndQuarantineFileAsync(request),
                    
                    XDRAction.RestrictAppExecution => 
                        await _mdeService.RestrictAppExecutionAsync(request),
                    
                    XDRAction.RemoveAppRestriction => 
                        await _mdeService.RemoveAppRestrictionAsync(request),
                    
                    XDRAction.StartAutomatedInvestigation => 
                        await _mdeService.StartAutomatedInvestigationAsync(request),
                    
                    XDRAction.SubmitIndicator => 
                        await _mdeService.SubmitIndicatorAsync(request),
                    
                    _ => CreateUnsupportedActionResponse(request)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MDE Worker failed for action: {Action}", request.Action);
                return CreateErrorResponse(request, ex);
            }
        }

        private async Task<XDRRemediationResponse> ExecuteFullScanAsync(XDRRemediationRequest request)
        {
            request.Parameters["scanType"] = "Full";
            return await _mdeService.RunAntivirusScanAsync(request);
        }

        private XDRRemediationResponse CreateUnsupportedActionResponse(XDRRemediationRequest request)
        {
            return new XDRRemediationResponse
            {
                RequestId = request.RequestId,
                TenantId = request.TenantId,
                IncidentId = request.IncidentId,
                Success = false,
                Status = "ActionNotSupported",
                Message = $"Action {request.Action} is not supported by MDE Worker",
                CompletedAt = DateTime.UtcNow
            };
        }

        private XDRRemediationResponse CreateErrorResponse(XDRRemediationRequest request, Exception ex)
        {
            return new XDRRemediationResponse
            {
                RequestId = request.RequestId,
                TenantId = request.TenantId,
                IncidentId = request.IncidentId,
                Success = false,
                Status = "Error",
                Message = $"MDE Worker error: {ex.Message}",
                Errors = new List<string> { ex.ToString() },
                CompletedAt = DateTime.UtcNow
            };
        }
    }
}
