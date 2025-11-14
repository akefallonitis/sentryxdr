using Microsoft.Extensions.Logging;
using SentryXDR.Models;
using SentryXDR.Services.Authentication;
using System.Text.Json;

namespace SentryXDR.Services.Workers
{
    public interface IIntuneDeviceService
    {
        // Critical Device Actions
        Task<XDRRemediationResponse> WipeDeviceAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> RetireDeviceAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> RemoteLockDeviceAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> ResetPasscodeAsync(XDRRemediationRequest request);
        
        // Device Management
        Task<XDRRemediationResponse> RebootDeviceAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> ShutdownDeviceAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> SyncDeviceAsync(XDRRemediationRequest request);
        
        // Lost Device Mode
        Task<XDRRemediationResponse> EnableLostModeAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> DisableLostModeAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> LocateDeviceAsync(XDRRemediationRequest request);
        
        // Compliance
        Task<XDRRemediationResponse> TriggerComplianceEvaluationAsync(XDRRemediationRequest request);
        
        // Windows Defender Actions
        Task<XDRRemediationResponse> UpdateDefenderSignaturesAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> RunDefenderScanAsync(XDRRemediationRequest request);
        
        // App Management
        Task<XDRRemediationResponse> ForceRemoveAppAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> BlockAppExecutionAsync(XDRRemediationRequest request);
    }

    /// <summary>
    /// Intune Device Management Service
    /// Manages mobile device management actions via Microsoft Graph
    /// API Reference: https://learn.microsoft.com/en-us/graph/api/intune-devices-manageddevice-retire
    /// </summary>
    public class IntuneDeviceService : BaseWorkerService, IIntuneDeviceService
    {
        private readonly IMultiTenantAuthService _authService;
        private const string GraphBaseUrl = "https://graph.microsoft.com/v1.0";
        private const string GraphBetaUrl = "https://graph.microsoft.com/beta";

        public IntuneDeviceService(
            ILogger<IntuneDeviceService> logger,
            IMultiTenantAuthService authService,
            HttpClient httpClient) : base(logger, httpClient)
        {
            _authService = authService;
        }

        private async Task SetAuthHeaderAsync(string tenantId)
        {
            var token = await _authService.GetGraphTokenAsync(tenantId);
            SetBearerToken(token);
        }

        // ==================== Critical Device Actions ====================

        /// <summary>
        /// Wipe device (factory reset) - removes all data
        /// POST /deviceManagement/managedDevices/{id}/wipe
        /// Permission: DeviceManagementManagedDevices.PrivilegedOperations.All
        /// </summary>
        public async Task<XDRRemediationResponse> WipeDeviceAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "WipeDevice");
                await SetAuthHeaderAsync(request.TenantId);

                var deviceId = GetRequiredParameter(request, "deviceId", out var failureResponse);
                if (deviceId == null) return failureResponse!;

                var keepEnrollmentData = bool.Parse(GetOptionalParameter(request, "keepEnrollmentData", "false"));
                var keepUserData = bool.Parse(GetOptionalParameter(request, "keepUserData", "false"));
                var macOsUnlockCode = GetOptionalParameter(request, "macOsUnlockCode", "");

                Logger.LogCritical("WIPE DEVICE: Initiating factory reset for device {DeviceId} - THIS WILL ERASE ALL DATA", deviceId);

                var wipeRequest = new
                {
                    keepEnrollmentData = keepEnrollmentData,
                    keepUserData = keepUserData,
                    macOsUnlockCode = macOsUnlockCode
                };

                var result = await PostJsonAsync<JsonElement>(
                    $"{GraphBaseUrl}/deviceManagement/managedDevices/{deviceId}/wipe",
                    wipeRequest);

                if (result.Success)
                {
                    LogOperationComplete(request, "WipeDevice", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"Device wipe initiated successfully", new Dictionary<string, object>
                    {
                        { "deviceId", deviceId },
                        { "action", "FactoryReset" },
                        { "keepEnrollmentData", keepEnrollmentData },
                        { "keepUserData", keepUserData },
                        { "status", "WipeInProgress" },
                        { "warning", "This operation will erase all data on the device" }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error wiping device", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        /// <summary>
        /// Retire device (selective wipe) - removes company data only
        /// POST /deviceManagement/managedDevices/{id}/retire
        /// Permission: DeviceManagementManagedDevices.PrivilegedOperations.All
        /// </summary>
        public async Task<XDRRemediationResponse> RetireDeviceAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "RetireDevice");
                await SetAuthHeaderAsync(request.TenantId);

                var deviceId = GetRequiredParameter(request, "deviceId", out var failureResponse);
                if (deviceId == null) return failureResponse!;

                Logger.LogWarning("RETIRE DEVICE: Initiating selective wipe for device {DeviceId} - removes company data only", deviceId);

                var result = await PostJsonAsync<JsonElement>(
                    $"{GraphBaseUrl}/deviceManagement/managedDevices/{deviceId}/retire",
                    new { });

                if (result.Success)
                {
                    LogOperationComplete(request, "RetireDevice", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"Device retirement initiated successfully", new Dictionary<string, object>
                    {
                        { "deviceId", deviceId },
                        { "action", "SelectiveWipe" },
                        { "status", "RetireInProgress" },
                        { "effect", "Removes company data, apps, and profiles. Personal data is retained." }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error retiring device", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        /// <summary>
        /// Remote lock device
        /// POST /deviceManagement/managedDevices/{id}/remoteLock
        /// Permission: DeviceManagementManagedDevices.PrivilegedOperations.All
        /// </summary>
        public async Task<XDRRemediationResponse> RemoteLockDeviceAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "RemoteLockDevice");
                await SetAuthHeaderAsync(request.TenantId);

                var deviceId = GetRequiredParameter(request, "deviceId", out var failureResponse);
                if (deviceId == null) return failureResponse!;

                Logger.LogWarning("REMOTE LOCK: Locking device {DeviceId}", deviceId);

                var result = await PostJsonAsync<JsonElement>(
                    $"{GraphBaseUrl}/deviceManagement/managedDevices/{deviceId}/remoteLock",
                    new { });

                if (result.Success)
                {
                    LogOperationComplete(request, "RemoteLockDevice", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"Device locked successfully", new Dictionary<string, object>
                    {
                        { "deviceId", deviceId },
                        { "action", "RemoteLock" },
                        { "status", "Locked" },
                        { "note", "User will need to enter passcode to unlock" }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error locking device", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        /// <summary>
        /// Reset device passcode
        /// POST /deviceManagement/managedDevices/{id}/resetPasscode
        /// Permission: DeviceManagementManagedDevices.PrivilegedOperations.All
        /// </summary>
        public async Task<XDRRemediationResponse> ResetPasscodeAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "ResetPasscode");
                await SetAuthHeaderAsync(request.TenantId);

                var deviceId = GetRequiredParameter(request, "deviceId", out var failureResponse);
                if (deviceId == null) return failureResponse!;

                Logger.LogWarning("RESET PASSCODE: Resetting passcode for device {DeviceId}", deviceId);

                var result = await PostJsonAsync<JsonElement>(
                    $"{GraphBaseUrl}/deviceManagement/managedDevices/{deviceId}/resetPasscode",
                    new { });

                if (result.Success)
                {
                    LogOperationComplete(request, "ResetPasscode", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"Device passcode reset successfully", new Dictionary<string, object>
                    {
                        { "deviceId", deviceId },
                        { "action", "PasscodeReset" },
                        { "status", "ResetInProgress" },
                        { "note", "New temporary passcode will be generated and displayed in Intune portal" }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error resetting passcode", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        // ==================== Device Management ====================

        /// <summary>
        /// Reboot device immediately
        /// POST /deviceManagement/managedDevices/{id}/rebootNow
        /// Permission: DeviceManagementManagedDevices.PrivilegedOperations.All
        /// </summary>
        public async Task<XDRRemediationResponse> RebootDeviceAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "RebootDevice");
                await SetAuthHeaderAsync(request.TenantId);

                var deviceId = GetRequiredParameter(request, "deviceId", out var failureResponse);
                if (deviceId == null) return failureResponse!;

                var result = await PostJsonAsync<JsonElement>(
                    $"{GraphBaseUrl}/deviceManagement/managedDevices/{deviceId}/rebootNow",
                    new { });

                if (result.Success)
                {
                    LogOperationComplete(request, "RebootDevice", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"Device reboot initiated successfully", new Dictionary<string, object>
                    {
                        { "deviceId", deviceId },
                        { "action", "Reboot" },
                        { "status", "RebootInProgress" }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error rebooting device", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        /// <summary>
        /// Shutdown device
        /// POST /deviceManagement/managedDevices/{id}/shutDown
        /// Permission: DeviceManagementManagedDevices.PrivilegedOperations.All
        /// </summary>
        public async Task<XDRRemediationResponse> ShutdownDeviceAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "ShutdownDevice");
                await SetAuthHeaderAsync(request.TenantId);

                var deviceId = GetRequiredParameter(request, "deviceId", out var failureResponse);
                if (deviceId == null) return failureResponse!;

                var result = await PostJsonAsync<JsonElement>(
                    $"{GraphBaseUrl}/deviceManagement/managedDevices/{deviceId}/shutDown",
                    new { });

                if (result.Success)
                {
                    LogOperationComplete(request, "ShutdownDevice", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"Device shutdown initiated successfully", new Dictionary<string, object>
                    {
                        { "deviceId", deviceId },
                        { "action", "Shutdown" },
                        { "status", "ShutdownInProgress" }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error shutting down device", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        /// <summary>
        /// Sync device with Intune
        /// POST /deviceManagement/managedDevices/{id}/syncDevice
        /// Permission: DeviceManagementManagedDevices.PrivilegedOperations.All
        /// </summary>
        public async Task<XDRRemediationResponse> SyncDeviceAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "SyncDevice");
                await SetAuthHeaderAsync(request.TenantId);

                var deviceId = GetRequiredParameter(request, "deviceId", out var failureResponse);
                if (deviceId == null) return failureResponse!;

                var result = await PostJsonAsync<JsonElement>(
                    $"{GraphBaseUrl}/deviceManagement/managedDevices/{deviceId}/syncDevice",
                    new { });

                if (result.Success)
                {
                    LogOperationComplete(request, "SyncDevice", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"Device sync initiated successfully", new Dictionary<string, object>
                    {
                        { "deviceId", deviceId },
                        { "action", "Sync" },
                        { "status", "SyncInProgress" },
                        { "effect", "Device will check in with Intune and apply latest policies" }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error syncing device", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        // ==================== Lost Device Mode ====================

        /// <summary>
        /// Enable lost mode on iOS device
        /// POST /deviceManagement/managedDevices/{id}/enableLostMode
        /// Permission: DeviceManagementManagedDevices.PrivilegedOperations.All
        /// </summary>
        public async Task<XDRRemediationResponse> EnableLostModeAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "EnableLostMode");
                await SetAuthHeaderAsync(request.TenantId);

                var deviceId = GetRequiredParameter(request, "deviceId", out var failureResponse);
                if (deviceId == null) return failureResponse!;

                var message = GetOptionalParameter(request, "message", "This device has been lost. Please contact IT.");
                var phoneNumber = GetOptionalParameter(request, "phoneNumber", "");
                var footnote = GetOptionalParameter(request, "footnote", "Managed by SentryXDR");

                var lostModeRequest = new
                {
                    message = message,
                    phoneNumber = phoneNumber,
                    footnote = footnote
                };

                var result = await PostJsonAsync<JsonElement>(
                    $"{GraphBaseUrl}/deviceManagement/managedDevices/{deviceId}/enableLostMode",
                    lostModeRequest);

                if (result.Success)
                {
                    LogOperationComplete(request, "EnableLostMode", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"Lost mode enabled successfully", new Dictionary<string, object>
                    {
                        { "deviceId", deviceId },
                        { "action", "EnableLostMode" },
                        { "status", "LostModeEnabled" },
                        { "message", message },
                        { "phoneNumber", phoneNumber }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error enabling lost mode", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        /// <summary>
        /// Disable lost mode on iOS device
        /// POST /deviceManagement/managedDevices/{id}/disableLostMode
        /// Permission: DeviceManagementManagedDevices.PrivilegedOperations.All
        /// </summary>
        public async Task<XDRRemediationResponse> DisableLostModeAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "DisableLostMode");
                await SetAuthHeaderAsync(request.TenantId);

                var deviceId = GetRequiredParameter(request, "deviceId", out var failureResponse);
                if (deviceId == null) return failureResponse!;

                var result = await PostJsonAsync<JsonElement>(
                    $"{GraphBaseUrl}/deviceManagement/managedDevices/{deviceId}/disableLostMode",
                    new { });

                if (result.Success)
                {
                    LogOperationComplete(request, "DisableLostMode", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"Lost mode disabled successfully", new Dictionary<string, object>
                    {
                        { "deviceId", deviceId },
                        { "action", "DisableLostMode" },
                        { "status", "LostModeDisabled" }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error disabling lost mode", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        /// <summary>
        /// Locate lost or stolen device
        /// POST /deviceManagement/managedDevices/{id}/locateDevice
        /// Permission: DeviceManagementManagedDevices.PrivilegedOperations.All
        /// </summary>
        public async Task<XDRRemediationResponse> LocateDeviceAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "LocateDevice");
                await SetAuthHeaderAsync(request.TenantId);

                var deviceId = GetRequiredParameter(request, "deviceId", out var failureResponse);
                if (deviceId == null) return failureResponse!;

                var result = await PostJsonAsync<JsonElement>(
                    $"{GraphBaseUrl}/deviceManagement/managedDevices/{deviceId}/locateDevice",
                    new { });

                if (result.Success)
                {
                    LogOperationComplete(request, "LocateDevice", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"Device location request sent successfully", new Dictionary<string, object>
                    {
                        { "deviceId", deviceId },
                        { "action", "LocateDevice" },
                        { "status", "LocationRequestSent" },
                        { "note", "Location will be available in Intune portal shortly" }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error locating device", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        // ==================== Compliance ====================

        /// <summary>
        /// Trigger compliance evaluation
        /// POST /deviceManagement/managedDevices/{id}/reevaluateCompliance (Graph Beta)
        /// Permission: DeviceManagementManagedDevices.ReadWrite.All
        /// </summary>
        public async Task<XDRRemediationResponse> TriggerComplianceEvaluationAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "TriggerComplianceEvaluation");
                await SetAuthHeaderAsync(request.TenantId);

                var deviceId = GetRequiredParameter(request, "deviceId", out var failureResponse);
                if (deviceId == null) return failureResponse!;

                var result = await PostJsonAsync<JsonElement>(
                    $"{GraphBetaUrl}/deviceManagement/managedDevices/{deviceId}/reevaluateCompliance",
                    new { });

                if (result.Success)
                {
                    LogOperationComplete(request, "TriggerComplianceEvaluation", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"Compliance evaluation triggered successfully", new Dictionary<string, object>
                    {
                        { "deviceId", deviceId },
                        { "action", "ComplianceEvaluation" },
                        { "status", "EvaluationTriggered" },
                        { "note", "Compliance status will be updated within 24 hours" }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error triggering compliance evaluation", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        // ==================== Windows Defender Actions ====================

        /// <summary>
        /// Update Windows Defender signatures
        /// POST /deviceManagement/managedDevices/{id}/windowsDefenderUpdateSignatures
        /// Permission: DeviceManagementManagedDevices.PrivilegedOperations.All
        /// </summary>
        public async Task<XDRRemediationResponse> UpdateDefenderSignaturesAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "UpdateDefenderSignatures");
                await SetAuthHeaderAsync(request.TenantId);

                var deviceId = GetRequiredParameter(request, "deviceId", out var failureResponse);
                if (deviceId == null) return failureResponse!;

                var result = await PostJsonAsync<JsonElement>(
                    $"{GraphBaseUrl}/deviceManagement/managedDevices/{deviceId}/windowsDefenderUpdateSignatures",
                    new { });

                if (result.Success)
                {
                    LogOperationComplete(request, "UpdateDefenderSignatures", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"Defender signature update initiated successfully", new Dictionary<string, object>
                    {
                        { "deviceId", deviceId },
                        { "action", "UpdateDefenderSignatures" },
                        { "status", "UpdateInProgress" }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error updating Defender signatures", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        /// <summary>
        /// Run Windows Defender scan
        /// POST /deviceManagement/managedDevices/{id}/windowsDefenderScan
        /// Permission: DeviceManagementManagedDevices.PrivilegedOperations.All
        /// </summary>
        public async Task<XDRRemediationResponse> RunDefenderScanAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "RunDefenderScan");
                await SetAuthHeaderAsync(request.TenantId);

                var deviceId = GetRequiredParameter(request, "deviceId", out var failureResponse);
                if (deviceId == null) return failureResponse!;

                var quickScan = bool.Parse(GetOptionalParameter(request, "quickScan", "true"));

                var scanRequest = new
                {
                    quickScan = quickScan
                };

                var result = await PostJsonAsync<JsonElement>(
                    $"{GraphBaseUrl}/deviceManagement/managedDevices/{deviceId}/windowsDefenderScan",
                    scanRequest);

                if (result.Success)
                {
                    LogOperationComplete(request, "RunDefenderScan", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"Defender scan initiated successfully", new Dictionary<string, object>
                    {
                        { "deviceId", deviceId },
                        { "action", "DefenderScan" },
                        { "scanType", quickScan ? "Quick" : "Full" },
                        { "status", "ScanInProgress" }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error running Defender scan", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        // ==================== App Management ====================

        /// <summary>
        /// Force remove app from managed device
        /// POST /deviceManagement/managedDevices/{id}/removeApp (Graph Beta)
        /// Permission: DeviceManagementManagedDevices.PrivilegedOperations.All
        /// </summary>
        public async Task<XDRRemediationResponse> ForceRemoveAppAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "ForceRemoveApp");
                await SetAuthHeaderAsync(request.TenantId);

                if (!ValidateRequiredParameters(request, out var failureResponse, "deviceId", "appId"))
                {
                    return failureResponse!;
                }

                var deviceId = request.Parameters["deviceId"]!.ToString()!;
                var appId = request.Parameters["appId"]!.ToString()!;

                Logger.LogCritical("FORCE REMOVE APP: Removing app {AppId} from device {DeviceId}", appId, deviceId);

                var removeRequest = new
                {
                    applicationId = appId
                };

                var result = await PostJsonAsync<JsonElement>(
                    $"{GraphBetaUrl}/deviceManagement/managedDevices/{deviceId}/removeApp",
                    removeRequest);

                if (result.Success)
                {
                    LogOperationComplete(request, "ForceRemoveApp", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"App removal initiated successfully", new Dictionary<string, object>
                    {
                        { "deviceId", deviceId },
                        { "appId", appId },
                        { "action", "ForceRemoveApp" },
                        { "status", "RemovalInProgress" }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error removing app", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        /// <summary>
        /// Block app execution on managed device
        /// POST /deviceManagement/managedDevices/{id}/blockApp (Graph Beta)
        /// Permission: DeviceManagementManagedDevices.PrivilegedOperations.All
        /// </summary>
        public async Task<XDRRemediationResponse> BlockAppExecutionAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "BlockAppExecution");
                await SetAuthHeaderAsync(request.TenantId);

                if (!ValidateRequiredParameters(request, out var failureResponse, "deviceId", "appId"))
                {
                    return failureResponse!;
                }

                var deviceId = request.Parameters["deviceId"]!.ToString()!;
                var appId = request.Parameters["appId"]!.ToString()!;

                Logger.LogCritical("BLOCK APP: Blocking app {AppId} execution on device {DeviceId}", appId, deviceId);

                var blockRequest = new
                {
                    applicationId = appId,
                    action = "block"
                };

                var result = await PostJsonAsync<JsonElement>(
                    $"{GraphBetaUrl}/deviceManagement/managedDevices/{deviceId}/blockApp",
                    blockRequest);

                if (result.Success)
                {
                    LogOperationComplete(request, "BlockAppExecution", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"App execution blocked successfully", new Dictionary<string, object>
                    {
                        { "deviceId", deviceId },
                        { "appId", appId },
                        { "action", "BlockAppExecution" },
                        { "effect", "App can no longer be launched on this device" }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error blocking app", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }
    }
}
