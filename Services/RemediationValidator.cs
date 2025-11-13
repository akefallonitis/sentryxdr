using SentryXDR.Models;
using Microsoft.Extensions.Logging;

namespace SentryXDR.Services
{
    public interface IRemediationValidator
    {
        Task<ValidationResult> ValidateRequestAsync(XDRRemediationRequest request);
        bool ValidateTenantId(string tenantId);
        bool ValidateParameters(XDRAction action, Dictionary<string, object> parameters);
    }

    public class RemediationValidator : IRemediationValidator
    {
        private readonly ILogger<RemediationValidator> _logger;
        private readonly ITenantConfigService _tenantConfigService;

        public RemediationValidator(
            ILogger<RemediationValidator> logger,
            ITenantConfigService tenantConfigService)
        {
            _logger = logger;
            _tenantConfigService = tenantConfigService;
        }

        public async Task<ValidationResult> ValidateRequestAsync(XDRRemediationRequest request)
        {
            // Validate tenant
            if (string.IsNullOrEmpty(request.TenantId))
            {
                return ValidationResult.Failure("TenantId is required");
            }

            if (!ValidateTenantId(request.TenantId))
            {
                return ValidationResult.Failure("Invalid TenantId format");
            }

            var tenantConfig = await _tenantConfigService.GetTenantConfigAsync(request.TenantId);
            if (tenantConfig == null || !tenantConfig.IsActive)
            {
                return ValidationResult.Failure($"Tenant {request.TenantId} is not active or configured");
            }

            // Validate incident ID
            if (string.IsNullOrEmpty(request.IncidentId))
            {
                return ValidationResult.Failure("IncidentId is required");
            }

            // Validate platform is enabled for tenant
            if (!tenantConfig.EnabledPlatforms.GetValueOrDefault(request.Platform, false))
            {
                return ValidationResult.Failure($"Platform {request.Platform} is not enabled for this tenant");
            }

            // Validate parameters
            if (!ValidateParameters(request.Action, request.Parameters))
            {
                return ValidationResult.Failure($"Invalid parameters for action {request.Action}");
            }

            // Validate initiator
            if (string.IsNullOrEmpty(request.InitiatedBy))
            {
                return ValidationResult.Failure("InitiatedBy is required");
            }

            // Validate justification for high/critical priority
            if (request.Priority >= RemediationPriority.High && string.IsNullOrEmpty(request.Justification))
            {
                return ValidationResult.Failure("Justification is required for High or Critical priority actions");
            }

            return ValidationResult.Success();
        }

        public bool ValidateTenantId(string tenantId)
        {
            return Guid.TryParse(tenantId, out _);
        }

        public bool ValidateParameters(XDRAction action, Dictionary<string, object> parameters)
        {
            if (parameters == null || parameters.Count == 0)
            {
                _logger.LogWarning($"No parameters provided for action {action}");
                return false;
            }

            // Validate based on action type
            return action switch
            {
                // MDE validations
                XDRAction.IsolateDevice or 
                XDRAction.ReleaseDeviceFromIsolation or
                XDRAction.RunAntivirusScan or
                XDRAction.CollectInvestigationPackage or
                XDRAction.RestrictAppExecution or
                XDRAction.RemoveAppRestriction => 
                    parameters.ContainsKey("deviceId") || parameters.ContainsKey("machineId"),

                XDRAction.StopAndQuarantineFile => 
                    parameters.ContainsKey("sha1") || parameters.ContainsKey("sha256"),

                XDRAction.StartAutomatedInvestigation => 
                    parameters.ContainsKey("alertId"),

                // MDO validations
                XDRAction.SoftDeleteEmail or 
                XDRAction.HardDeleteEmail or
                XDRAction.MoveEmailToJunk => 
                    parameters.ContainsKey("messageId") && parameters.ContainsKey("userId"),

                // Entra ID validations
                XDRAction.DisableUserAccount or
                XDRAction.EnableUserAccount or
                XDRAction.RevokeUserSignInSessions or
                XDRAction.ResetUserPassword => 
                    parameters.ContainsKey("userId") || parameters.ContainsKey("userPrincipalName"),

                // Intune validations
                XDRAction.WipeDevice or
                XDRAction.RetireDevice or
                XDRAction.RemoteLockDevice => 
                    parameters.ContainsKey("deviceId"),

                // Azure validations
                XDRAction.StopVM or
                XDRAction.StartVM or
                XDRAction.RestartVM => 
                    parameters.ContainsKey("vmId") || 
                    (parameters.ContainsKey("resourceGroup") && parameters.ContainsKey("vmName")),

                _ => true // Default: assume valid if parameters exist
            };
        }
    }

    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> Errors { get; set; } = new();

        public static ValidationResult Success() => new() { IsValid = true };
        
        public static ValidationResult Failure(string message) => new() 
        { 
            IsValid = false, 
            Message = message,
            Errors = new List<string> { message }
        };
    }
}
