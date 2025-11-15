using Microsoft.Extensions.Logging;
using SentryXDR.Models;
using SentryXDR.Services.Authentication;
using System.Text;
using System.Text.Json;

namespace SentryXDR.Services.Workers
{
    public interface IMailForwardingService
    {
        Task<XDRRemediationResponse> DisableExternalMailForwardingAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> EnableExternalMailForwardingAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> GetMailForwardingStatusAsync(XDRRemediationRequest request);
    }

    /// <summary>
    /// Mail Forwarding Control Service
    /// Uses Graph Beta API to control external mail forwarding
    /// API Reference: https://learn.microsoft.com/en-us/graph/api/user-update
    /// Permission Required: MailboxSettings.ReadWrite
    /// </summary>
    public class MailForwardingService : BaseWorkerService, IMailForwardingService
    {
        private readonly IMultiTenantAuthService _authService;
        private const string GraphBetaUrl = "https://graph.microsoft.com/beta";

        public MailForwardingService(
            ILogger<MailForwardingService> logger,
            IMultiTenantAuthService authService,
            HttpClient httpClient) : base(logger, httpClient)
        {
            _authService = authService;
        }

        private async Task SetAuthHeaderAsync(string tenantId)
        {
            var token = await _authService.GetGraphBetaTokenAsync(tenantId);
            SetBearerToken(token);
        }

        /// <summary>
        /// Disable external mail forwarding for a user
        /// PATCH /beta/users/{userId}/mailboxSettings
        /// </summary>
        public async Task<XDRRemediationResponse> DisableExternalMailForwardingAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                LogOperationStart(request, "DisableExternalMailForwarding");
                await SetAuthHeaderAsync(request.TenantId);

                if (!ValidateRequiredParameters(request, out var failureResponse, "userId"))
                {
                    return failureResponse!;
                }

                var userId = request.Parameters["userId"]!.ToString()!;

                Logger.LogWarning("DISABLING external mail forwarding for user: {UserId}", userId);

                // Disable forwarding by setting forwardingSmtpAddress to null
                var updatePayload = new
                {
                    automaticRepliesSetting = new
                    {
                        status = "disabled"
                    },
                    forwardingSmtpAddress = (string?)null
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(updatePayload),
                    Encoding.UTF8,
                    "application/json");

                var response = await HttpClient.PatchAsync(
                    $"{GraphBetaUrl}/users/{userId}/mailboxSettings",
                    content);

                if (response.IsSuccessStatusCode)
                {
                    return CreateSuccessResponse(
                        request,
                        $"External mail forwarding disabled for user {userId}",
                        new Dictionary<string, object>
                        {
                            ["userId"] = userId,
                            ["forwardingDisabled"] = true,
                            ["timestamp"] = DateTime.UtcNow
                        },
                        startTime);
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                return CreateFailureResponse(
                    request,
                    $"Failed to disable mail forwarding: {response.ReasonPhrase} - {errorContent}",
                    startTime);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error disabling external mail forwarding");
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        /// <summary>
        /// Enable external mail forwarding for a user
        /// PATCH /beta/users/{userId}/mailboxSettings
        /// </summary>
        public async Task<XDRRemediationResponse> EnableExternalMailForwardingAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                LogOperationStart(request, "EnableExternalMailForwarding");
                await SetAuthHeaderAsync(request.TenantId);

                if (!ValidateRequiredParameters(request, out var failureResponse, "userId", "forwardingAddress"))
                {
                    return failureResponse!;
                }

                var userId = request.Parameters["userId"]!.ToString()!;
                var forwardingAddress = request.Parameters["forwardingAddress"]!.ToString()!;

                Logger.LogWarning("ENABLING external mail forwarding for user: {UserId} to {Address}", 
                    userId, forwardingAddress);

                var updatePayload = new
                {
                    forwardingSmtpAddress = forwardingAddress
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(updatePayload),
                    Encoding.UTF8,
                    "application/json");

                var response = await HttpClient.PatchAsync(
                    $"{GraphBetaUrl}/users/{userId}/mailboxSettings",
                    content);

                if (response.IsSuccessStatusCode)
                {
                    return CreateSuccessResponse(
                        request,
                        $"External mail forwarding enabled for user {userId} to {forwardingAddress}",
                        new Dictionary<string, object>
                        {
                            ["userId"] = userId,
                            ["forwardingAddress"] = forwardingAddress,
                            ["forwardingEnabled"] = true,
                            ["timestamp"] = DateTime.UtcNow
                        },
                        startTime);
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                return CreateFailureResponse(
                    request,
                    $"Failed to enable mail forwarding: {response.ReasonPhrase} - {errorContent}",
                    startTime);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error enabling external mail forwarding");
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        /// <summary>
        /// Get mail forwarding status for a user
        /// GET /beta/users/{userId}/mailboxSettings
        /// </summary>
        public async Task<XDRRemediationResponse> GetMailForwardingStatusAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                LogOperationStart(request, "GetMailForwardingStatus");
                await SetAuthHeaderAsync(request.TenantId);

                if (!ValidateRequiredParameters(request, out var failureResponse, "userId"))
                {
                    return failureResponse!;
                }

                var userId = request.Parameters["userId"]!.ToString()!;

                var response = await HttpClient.GetAsync(
                    $"{GraphBetaUrl}/users/{userId}/mailboxSettings");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var settings = JsonSerializer.Deserialize<JsonElement>(content);

                    var forwardingAddress = settings.TryGetProperty("forwardingSmtpAddress", out var addr)
                        ? addr.GetString()
                        : null;

                    var isForwardingEnabled = !string.IsNullOrEmpty(forwardingAddress);

                    return CreateSuccessResponse(
                        request,
                        $"Retrieved mail forwarding status for user {userId}",
                        new Dictionary<string, object>
                        {
                            ["userId"] = userId,
                            ["forwardingEnabled"] = isForwardingEnabled,
                            ["forwardingAddress"] = forwardingAddress ?? "None",
                            ["mailboxSettings"] = settings
                        },
                        startTime);
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                return CreateFailureResponse(
                    request,
                    $"Failed to get mail forwarding status: {response.ReasonPhrase} - {errorContent}",
                    startTime);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error getting mail forwarding status");
                return CreateExceptionResponse(request, ex, startTime);
            }
        }
    }
}
