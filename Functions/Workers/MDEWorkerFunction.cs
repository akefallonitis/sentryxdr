using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SentryXDR.Models;
using SentryXDR.Services.Workers;

namespace SentryXDR.Functions.Workers
{
    /// <summary>
    /// Dedicated Microsoft Defender for Endpoint Worker
    /// Handles 52 endpoint security operations
    /// </summary>
    public class MDEWorkerFunction
    {
        private readonly ILogger<MDEWorkerFunction> _logger;
        private readonly IMDEApiService _mdeService;

        public MDEWorkerFunction(ILogger<MDEWorkerFunction> logger, IMDEApiService mdeService)
        {
            _logger = logger;
            _mdeService = mdeService;
        }

        [Function("MDEWorkerActivity")]
        public async Task<XDRRemediationResponse> RunAsync([ActivityTrigger] XDRRemediationRequest request)
        {
            _logger.LogInformation(
                "MDE Worker - Tenant: {TenantId}, Action: {Action}, Incident: {IncidentId}",
                request.TenantId,
                request.Action,
                request.IncidentId);

            try
            {
                return request.Action switch
                {
                    // Device Control Actions
                    XDRAction.IsolateDevice => await _mdeService.IsolateDeviceAsync(request),
                    XDRAction.ReleaseDeviceFromIsolation => await _mdeService.ReleaseDeviceAsync(request),
                    XDRAction.RestrictAppExecution => await _mdeService.RestrictAppExecutionAsync(request),
                    XDRAction.RemoveAppRestriction => await _mdeService.RemoveAppRestrictionAsync(request),
                    
                    // Antivirus Actions
                    XDRAction.RunAntivirusScan or XDRAction.RunQuickScan => 
                        await _mdeService.RunAntivirusScanAsync(request),
                    XDRAction.RunFullScan => await ExecuteFullScanAsync(request),
                    
                    // Investigation Actions
                    XDRAction.CollectInvestigationPackage => 
                        await _mdeService.CollectInvestigationPackageAsync(request),
                    XDRAction.StartAutomatedInvestigation => 
                        await _mdeService.StartAutomatedInvestigationAsync(request),
                    
                    // File Actions
                    XDRAction.StopAndQuarantineFile => 
                        await _mdeService.StopAndQuarantineFileAsync(request),
                    
                    // Indicator Actions
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
