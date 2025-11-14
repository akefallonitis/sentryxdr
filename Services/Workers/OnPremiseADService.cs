using Microsoft.Extensions.Logging;
using SentryXDR.Models;
using System.Text.Json;

namespace SentryXDR.Services.Workers
{
    public interface IOnPremiseADService
    {
        Task<XDRRemediationResponse> DisableOnPremUserAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> ResetOnPremPasswordAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> MoveToQuarantineOUAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> DisableOnPremComputerAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> ForceDeltaSyncAsync(XDRRemediationRequest request);
    }

    /// <summary>
    /// On-Premise Active Directory Service
    /// Executes on-premise AD actions via Azure Automation Hybrid Worker
    /// Requires: Azure Automation Account with Hybrid Worker configured
    /// </summary>
    public class OnPremiseADService : BaseWorkerService, IOnPremiseADService
    {
        private readonly IAzureAutomationService _automationService;
        
        public OnPremiseADService(
            ILogger<OnPremiseADService> logger,
            IAzureAutomationService automationService,
            HttpClient httpClient) : base(logger, httpClient)
        {
            _automationService = automationService;
        }

        /// <summary>
        /// Disable on-premise user account
        /// Triggers: Disable-OnPremUser.ps1 runbook
        /// </summary>
        public async Task<XDRRemediationResponse> DisableOnPremUserAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "DisableOnPremUser");

                var userPrincipalName = GetRequiredParameter(request, "userPrincipalName", out var failureResponse);
                if (userPrincipalName == null) return failureResponse!;

                Logger.LogCritical("DISABLE ON-PREM USER: Triggering runbook to disable {UserPrincipalName}", userPrincipalName);

                var runbookParameters = new Dictionary<string, object>
                {
                    { "UserPrincipalName", userPrincipalName },
                    { "IncidentId", request.IncidentId ?? "Unknown" },
                    { "Justification", request.Justification ?? "SentryXDR automated remediation" }
                };

                var jobId = await _automationService.StartRunbookAsync(
                    "Disable-OnPremUser",
                    runbookParameters);

                LogOperationComplete(request, "DisableOnPremUser", DateTime.UtcNow - startTime, true);
                
                return CreateSuccessResponse(request, $"On-premise user disable initiated", new Dictionary<string, object>
                {
                    { "userPrincipalName", userPrincipalName },
                    { "jobId", jobId },
                    { "status", "Running" },
                    { "action", "DisableOnPremUser" },
                    { "note", "Check job status for completion. Typically completes in 1-2 minutes." },
                    { "checkStatusUrl", $"/api/automation/jobs/{jobId}/status" }
                }, startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        /// <summary>
        /// Reset on-premise user password
        /// Triggers: Reset-OnPremPassword.ps1 runbook
        /// </summary>
        public async Task<XDRRemediationResponse> ResetOnPremPasswordAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "ResetOnPremPassword");

                if (!ValidateRequiredParameters(request, out var failureResponse, "userPrincipalName", "newPassword"))
                {
                    return failureResponse!;
                }

                var userPrincipalName = request.Parameters["userPrincipalName"]!.ToString()!;
                var newPassword = request.Parameters["newPassword"]!.ToString()!;

                Logger.LogCritical("RESET ON-PREM PASSWORD: Triggering runbook for {UserPrincipalName}", userPrincipalName);

                var runbookParameters = new Dictionary<string, object>
                {
                    { "UserPrincipalName", userPrincipalName },
                    { "NewPassword", newPassword },
                    { "IncidentId", request.IncidentId ?? "Unknown" },
                    { "Justification", request.Justification ?? "SentryXDR automated remediation" }
                };

                var jobId = await _automationService.StartRunbookAsync(
                    "Reset-OnPremPassword",
                    runbookParameters);

                LogOperationComplete(request, "ResetOnPremPassword", DateTime.UtcNow - startTime, true);
                
                return CreateSuccessResponse(request, $"On-premise password reset initiated", new Dictionary<string, object>
                {
                    { "userPrincipalName", userPrincipalName },
                    { "jobId", jobId },
                    { "status", "Running" },
                    { "action", "ResetOnPremPassword" },
                    { "warning", "Password is in transit - ensure secure communication channel" }
                }, startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        /// <summary>
        /// Move user to quarantine OU
        /// Triggers: Move-UserToQuarantineOU.ps1 runbook
        /// </summary>
        public async Task<XDRRemediationResponse> MoveToQuarantineOUAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "MoveToQuarantineOU");

                var userPrincipalName = GetRequiredParameter(request, "userPrincipalName", out var failureResponse);
                if (userPrincipalName == null) return failureResponse!;

                var quarantineOU = GetOptionalParameter(request, "quarantineOU", "OU=Quarantine,DC=domain,DC=com");

                Logger.LogWarning("MOVE TO QUARANTINE: Moving {UserPrincipalName} to {QuarantineOU}", userPrincipalName, quarantineOU);

                var runbookParameters = new Dictionary<string, object>
                {
                    { "UserPrincipalName", userPrincipalName },
                    { "QuarantineOU", quarantineOU },
                    { "IncidentId", request.IncidentId ?? "Unknown" }
                };

                var jobId = await _automationService.StartRunbookAsync(
                    "Move-UserToQuarantineOU",
                    runbookParameters);

                LogOperationComplete(request, "MoveToQuarantineOU", DateTime.UtcNow - startTime, true);
                
                return CreateSuccessResponse(request, $"User move to quarantine initiated", new Dictionary<string, object>
                {
                    { "userPrincipalName", userPrincipalName },
                    { "quarantineOU", quarantineOU },
                    { "jobId", jobId },
                    { "status", "Running" },
                    { "action", "MoveToQuarantineOU" }
                }, startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        /// <summary>
        /// Disable on-premise computer account
        /// Triggers: Disable-OnPremComputer.ps1 runbook
        /// </summary>
        public async Task<XDRRemediationResponse> DisableOnPremComputerAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "DisableOnPremComputer");

                var computerName = GetRequiredParameter(request, "computerName", out var failureResponse);
                if (computerName == null) return failureResponse!;

                Logger.LogCritical("DISABLE ON-PREM COMPUTER: Triggering runbook to disable {ComputerName}", computerName);

                var runbookParameters = new Dictionary<string, object>
                {
                    { "ComputerName", computerName },
                    { "IncidentId", request.IncidentId ?? "Unknown" },
                    { "Justification", request.Justification ?? "SentryXDR automated remediation" }
                };

                var jobId = await _automationService.StartRunbookAsync(
                    "Disable-OnPremComputer",
                    runbookParameters);

                LogOperationComplete(request, "DisableOnPremComputer", DateTime.UtcNow - startTime, true);
                
                return CreateSuccessResponse(request, $"On-premise computer disable initiated", new Dictionary<string, object>
                {
                    { "computerName", computerName },
                    { "jobId", jobId },
                    { "status", "Running" },
                    { "action", "DisableOnPremComputer" },
                    { "effect", "Computer account will be disabled in Active Directory" }
                }, startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        /// <summary>
        /// Force AD Connect delta sync
        /// Triggers: Start-DeltaSync.ps1 runbook
        /// </summary>
        public async Task<XDRRemediationResponse> ForceDeltaSyncAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "ForceDeltaSync");

                Logger.LogInformation("FORCE DELTA SYNC: Triggering Azure AD Connect delta synchronization");

                var runbookParameters = new Dictionary<string, object>
                {
                    { "IncidentId", request.IncidentId ?? "Unknown" }
                };

                var jobId = await _automationService.StartRunbookAsync(
                    "Start-DeltaSync",
                    runbookParameters);

                LogOperationComplete(request, "ForceDeltaSync", DateTime.UtcNow - startTime, true);
                
                return CreateSuccessResponse(request, $"AD Connect delta sync initiated", new Dictionary<string, object>
                {
                    { "jobId", jobId },
                    { "status", "Running" },
                    { "action", "ForceDeltaSync" },
                    { "note", "Changes will sync to Azure AD within 2-5 minutes" }
                }, startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }
    }
}
