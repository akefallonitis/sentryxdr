using Microsoft.Extensions.Logging;
using SentryXDR.Models;
using SentryXDR.Services.Authentication;
using System.Text.Json;

namespace SentryXDR.Services.Workers
{
    public interface IEntraIDConditionalAccessService
    {
        // Emergency Block Policies
        Task<XDRRemediationResponse> CreateEmergencyBlockPolicyAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> BlockGeographicLocationAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> BlockIPRangeAsync(XDRRemediationRequest request);
        
        // Break-Glass Policies
        Task<XDRRemediationResponse> EnableBreakGlassPolicyAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> DisableRiskyPolicyAsync(XDRRemediationRequest request);
        
        // Named Locations Management
        Task<XDRRemediationResponse> CreateBlockedNamedLocationAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> UpdateNamedLocationAsync(XDRRemediationRequest request);
        Task<XDRRemediationResponse> DeleteNamedLocationAsync(XDRRemediationRequest request);
    }

    /// <summary>
    /// Entra ID Conditional Access Service
    /// Manages emergency blocking and named location policies
    /// API Reference: https://learn.microsoft.com/en-us/graph/api/resources/conditionalaccesspolicy
    /// </summary>
    public class EntraIDConditionalAccessService : BaseWorkerService, IEntraIDConditionalAccessService
    {
        private readonly IMultiTenantAuthService _authService;
        private const string GraphBaseUrl = "https://graph.microsoft.com/v1.0";
        private const string GraphBetaUrl = "https://graph.microsoft.com/beta";

        public EntraIDConditionalAccessService(
            ILogger<EntraIDConditionalAccessService> logger,
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

        // ==================== Emergency Block Policies ====================

        /// <summary>
        /// Create emergency block policy for compromised users/apps
        /// POST /identity/conditionalAccess/policies
        /// Permission: Policy.ReadWrite.ConditionalAccess
        /// </summary>
        public async Task<XDRRemediationResponse> CreateEmergencyBlockPolicyAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "CreateEmergencyBlockPolicy");
                await SetAuthHeaderAsync(request.TenantId);

                if (!ValidateRequiredParameters(request, out var failureResponse, "policyName", "targetType"))
                {
                    return failureResponse!;
                }

                var policyName = request.Parameters["policyName"]!.ToString()!;
                var targetType = request.Parameters["targetType"]!.ToString()!; // users, groups, applications
                var targetIds = request.Parameters.GetValueOrDefault("targetIds")?.ToString();

                Logger.LogCritical("CREATE EMERGENCY BLOCK: Creating emergency CA policy {PolicyName} for {TargetType}", policyName, targetType);

                var policy = new
                {
                    displayName = policyName,
                    state = "enabled",
                    conditions = new
                    {
                        users = targetType == "users" ? new
                        {
                            includeUsers = targetIds?.Split(',') ?? new[] { "All" }
                        } : null,
                        applications = new
                        {
                            includeApplications = new[] { "All" }
                        },
                        clientAppTypes = new[] { "all" }
                    },
                    grantControls = new
                    {
                        operator = "OR",
                        builtInControls = new[] { "block" }
                    },
                    sessionControls = null
                };

                var result = await PostJsonAsync<JsonElement>(
                    $"{GraphBaseUrl}/identity/conditionalAccess/policies",
                    policy);

                if (result.Success && result.Data.HasValue)
                {
                    var policyId = result.Data.Value.GetProperty("id").GetString();
                    
                    LogOperationComplete(request, "CreateEmergencyBlockPolicy", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"Emergency block policy created successfully", new Dictionary<string, object>
                    {
                        { "policyId", policyId! },
                        { "policyName", policyName },
                        { "targetType", targetType },
                        { "state", "enabled" },
                        { "effect", "All access is now blocked for specified targets" },
                        { "warning", "This is a critical security policy - review immediately" }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error creating policy", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        /// <summary>
        /// Block specific geographic location via named location
        /// POST /identity/conditionalAccess/namedLocations + policy
        /// Permission: Policy.ReadWrite.ConditionalAccess
        /// </summary>
        public async Task<XDRRemediationResponse> BlockGeographicLocationAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "BlockGeographicLocation");
                await SetAuthHeaderAsync(request.TenantId);

                if (!ValidateRequiredParameters(request, out var failureResponse, "countryCode"))
                {
                    return failureResponse!;
                }

                var countryCode = request.Parameters["countryCode"]!.ToString()!; // ISO 3166-1 alpha-2 (e.g., "RU", "CN")
                var locationName = GetOptionalParameter(request, "locationName", $"Blocked-{countryCode}-{DateTime.UtcNow:yyyyMMdd}");

                Logger.LogCritical("BLOCK LOCATION: Blocking geographic location {CountryCode}", countryCode);

                // Step 1: Create named location
                var namedLocation = new
                {
                    odataType = "#microsoft.graph.countryNamedLocation",
                    displayName = locationName,
                    countriesAndRegions = new[] { countryCode },
                    includeUnknownCountriesAndRegions = false
                };

                var locationResult = await PostJsonAsync<JsonElement>(
                    $"{GraphBaseUrl}/identity/conditionalAccess/namedLocations",
                    namedLocation);

                if (!locationResult.Success || !locationResult.Data.HasValue)
                {
                    return CreateFailureResponse(request, locationResult.Error ?? "Failed to create named location", startTime);
                }

                var locationId = locationResult.Data.Value.GetProperty("id").GetString();

                // Step 2: Create blocking policy
                var policy = new
                {
                    displayName = $"Block {countryCode} - Emergency",
                    state = "enabled",
                    conditions = new
                    {
                        users = new
                        {
                            includeUsers = new[] { "All" }
                        },
                        applications = new
                        {
                            includeApplications = new[] { "All" }
                        },
                        locations = new
                        {
                            includeLocations = new[] { locationId }
                        }
                    },
                    grantControls = new
                    {
                        operator = "OR",
                        builtInControls = new[] { "block" }
                    }
                };

                var policyResult = await PostJsonAsync<JsonElement>(
                    $"{GraphBaseUrl}/identity/conditionalAccess/policies",
                    policy);

                if (policyResult.Success && policyResult.Data.HasValue)
                {
                    var policyId = policyResult.Data.Value.GetProperty("id").GetString();
                    
                    LogOperationComplete(request, "BlockGeographicLocation", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"Geographic location {countryCode} blocked successfully", new Dictionary<string, object>
                    {
                        { "countryCode", countryCode },
                        { "locationId", locationId! },
                        { "policyId", policyId! },
                        { "locationName", locationName },
                        { "effect", $"All sign-ins from {countryCode} are now blocked" }
                    }, startTime);
                }

                return CreateFailureResponse(request, policyResult.Error ?? "Failed to create blocking policy", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        /// <summary>
        /// Block specific IP range via named location
        /// POST /identity/conditionalAccess/namedLocations + policy
        /// Permission: Policy.ReadWrite.ConditionalAccess
        /// </summary>
        public async Task<XDRRemediationResponse> BlockIPRangeAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "BlockIPRange");
                await SetAuthHeaderAsync(request.TenantId);

                if (!ValidateRequiredParameters(request, out var failureResponse, "ipRanges"))
                {
                    return failureResponse!;
                }

                var ipRangesParam = request.Parameters["ipRanges"]!.ToString()!; // CIDR notation: "192.168.1.0/24" or comma-separated
                var ipRanges = ipRangesParam.Split(',').Select(ip => ip.Trim()).ToArray();
                var locationName = GetOptionalParameter(request, "locationName", $"Blocked-IPs-{DateTime.UtcNow:yyyyMMdd}");

                Logger.LogCritical("BLOCK IP RANGE: Blocking IP ranges {IpRanges}", string.Join(", ", ipRanges));

                // Step 1: Create named location for IPs
                var namedLocation = new
                {
                    odataType = "#microsoft.graph.ipNamedLocation",
                    displayName = locationName,
                    isTrusted = false,
                    ipRanges = ipRanges.Select(ip => new
                    {
                        odataType = "#microsoft.graph.iPv4CidrRange",
                        cidrAddress = ip.Contains('/') ? ip : $"{ip}/32" // Add /32 if single IP
                    }).ToArray()
                };

                var locationResult = await PostJsonAsync<JsonElement>(
                    $"{GraphBaseUrl}/identity/conditionalAccess/namedLocations",
                    namedLocation);

                if (!locationResult.Success || !locationResult.Data.HasValue)
                {
                    return CreateFailureResponse(request, locationResult.Error ?? "Failed to create named location", startTime);
                }

                var locationId = locationResult.Data.Value.GetProperty("id").GetString();

                // Step 2: Create blocking policy
                var policy = new
                {
                    displayName = $"Block IPs - Emergency - {DateTime.UtcNow:yyyyMMdd}",
                    state = "enabled",
                    conditions = new
                    {
                        users = new
                        {
                            includeUsers = new[] { "All" }
                        },
                        applications = new
                        {
                            includeApplications = new[] { "All" }
                        },
                        locations = new
                        {
                            includeLocations = new[] { locationId }
                        }
                    },
                    grantControls = new
                    {
                        operator = "OR",
                        builtInControls = new[] { "block" }
                    }
                };

                var policyResult = await PostJsonAsync<JsonElement>(
                    $"{GraphBaseUrl}/identity/conditionalAccess/policies",
                    policy);

                if (policyResult.Success && policyResult.Data.HasValue)
                {
                    var policyId = policyResult.Data.Value.GetProperty("id").GetString();
                    
                    LogOperationComplete(request, "BlockIPRange", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"IP range(s) blocked successfully", new Dictionary<string, object>
                    {
                        { "ipRanges", ipRanges },
                        { "locationId", locationId! },
                        { "policyId", policyId! },
                        { "locationName", locationName },
                        { "effect", $"All sign-ins from specified IP ranges are now blocked" }
                    }, startTime);
                }

                return CreateFailureResponse(request, policyResult.Error ?? "Failed to create blocking policy", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        // ==================== Break-Glass Policies ====================

        /// <summary>
        /// Enable break-glass policy (MFA requirement bypass for emergencies)
        /// POST /identity/conditionalAccess/policies
        /// Permission: Policy.ReadWrite.ConditionalAccess
        /// </summary>
        public async Task<XDRRemediationResponse> EnableBreakGlassPolicyAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "EnableBreakGlassPolicy");
                await SetAuthHeaderAsync(request.TenantId);

                if (!ValidateRequiredParameters(request, out var failureResponse, "breakGlassUserIds"))
                {
                    return failureResponse!;
                }

                var breakGlassUserIds = request.Parameters["breakGlassUserIds"]!.ToString()!.Split(',').Select(id => id.Trim()).ToArray();

                Logger.LogCritical("ENABLE BREAK-GLASS: Enabling emergency access for {Count} break-glass accounts", breakGlassUserIds.Length);

                var policy = new
                {
                    displayName = "Break-Glass Emergency Access",
                    state = "enabled",
                    conditions = new
                    {
                        users = new
                        {
                            includeUsers = breakGlassUserIds
                        },
                        applications = new
                        {
                            includeApplications = new[] { "All" }
                        }
                    },
                    grantControls = new
                    {
                        operator = "OR",
                        builtInControls = new[] { "compliantDevice" } // Require compliant device only, no MFA
                    }
                };

                var result = await PostJsonAsync<JsonElement>(
                    $"{GraphBaseUrl}/identity/conditionalAccess/policies",
                    policy);

                if (result.Success && result.Data.HasValue)
                {
                    var policyId = result.Data.Value.GetProperty("id").GetString();
                    
                    LogOperationComplete(request, "EnableBreakGlassPolicy", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"Break-glass policy enabled successfully", new Dictionary<string, object>
                    {
                        { "policyId", policyId! },
                        { "breakGlassUsers", breakGlassUserIds },
                        { "state", "enabled" },
                        { "effect", "Emergency accounts can sign in without MFA" },
                        { "warning", "This should only be used during emergencies - review and disable ASAP" }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error enabling break-glass policy", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        /// <summary>
        /// Disable risky policy that's causing issues
        /// PATCH /identity/conditionalAccess/policies/{id}
        /// Permission: Policy.ReadWrite.ConditionalAccess
        /// </summary>
        public async Task<XDRRemediationResponse> DisableRiskyPolicyAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "DisableRiskyPolicy");
                await SetAuthHeaderAsync(request.TenantId);

                var policyId = GetRequiredParameter(request, "policyId", out var failureResponse);
                if (policyId == null) return failureResponse!;

                Logger.LogCritical("DISABLE POLICY: Disabling conditional access policy {PolicyId}", policyId);

                var updateBody = new
                {
                    state = "disabled"
                };

                var result = await PatchJsonAsync<JsonElement>(
                    $"{GraphBaseUrl}/identity/conditionalAccess/policies/{policyId}",
                    updateBody);

                if (result.Success)
                {
                    LogOperationComplete(request, "DisableRiskyPolicy", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"Policy disabled successfully", new Dictionary<string, object>
                    {
                        { "policyId", policyId },
                        { "state", "disabled" },
                        { "effect", "Policy is no longer enforced" },
                        { "note", "Review policy configuration before re-enabling" }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error disabling policy", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        // ==================== Named Locations Management ====================

        /// <summary>
        /// Create blocked named location (generic)
        /// POST /identity/conditionalAccess/namedLocations
        /// Permission: Policy.ReadWrite.ConditionalAccess
        /// </summary>
        public async Task<XDRRemediationResponse> CreateBlockedNamedLocationAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "CreateBlockedNamedLocation");
                await SetAuthHeaderAsync(request.TenantId);

                if (!ValidateRequiredParameters(request, out var failureResponse, "locationName", "locationType"))
                {
                    return failureResponse!;
                }

                var locationName = request.Parameters["locationName"]!.ToString()!;
                var locationType = request.Parameters["locationType"]!.ToString()!; // "ip" or "country"

                object namedLocation;

                if (locationType.ToLower() == "ip")
                {
                    var ipRanges = request.Parameters["ipRanges"]!.ToString()!.Split(',').Select(ip => ip.Trim()).ToArray();
                    
                    namedLocation = new
                    {
                        odataType = "#microsoft.graph.ipNamedLocation",
                        displayName = locationName,
                        isTrusted = false,
                        ipRanges = ipRanges.Select(ip => new
                        {
                            odataType = "#microsoft.graph.iPv4CidrRange",
                            cidrAddress = ip.Contains('/') ? ip : $"{ip}/32"
                        }).ToArray()
                    };
                }
                else
                {
                    var countries = request.Parameters["countries"]!.ToString()!.Split(',').Select(c => c.Trim()).ToArray();
                    
                    namedLocation = new
                    {
                        odataType = "#microsoft.graph.countryNamedLocation",
                        displayName = locationName,
                        countriesAndRegions = countries,
                        includeUnknownCountriesAndRegions = false
                    };
                }

                var result = await PostJsonAsync<JsonElement>(
                    $"{GraphBaseUrl}/identity/conditionalAccess/namedLocations",
                    namedLocation);

                if (result.Success && result.Data.HasValue)
                {
                    var locationId = result.Data.Value.GetProperty("id").GetString();
                    
                    LogOperationComplete(request, "CreateBlockedNamedLocation", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"Named location created successfully", new Dictionary<string, object>
                    {
                        { "locationId", locationId! },
                        { "locationName", locationName },
                        { "locationType", locationType },
                        { "note", "Create a policy to block this location" }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error creating named location", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        /// <summary>
        /// Update named location
        /// PATCH /identity/conditionalAccess/namedLocations/{id}
        /// Permission: Policy.ReadWrite.ConditionalAccess
        /// </summary>
        public async Task<XDRRemediationResponse> UpdateNamedLocationAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "UpdateNamedLocation");
                await SetAuthHeaderAsync(request.TenantId);

                var locationId = GetRequiredParameter(request, "locationId", out var failureResponse);
                if (locationId == null) return failureResponse!;

                var updateBody = new Dictionary<string, object>();
                
                if (request.Parameters.ContainsKey("displayName"))
                    updateBody["displayName"] = request.Parameters["displayName"]!;
                
                if (request.Parameters.ContainsKey("ipRanges"))
                {
                    var ipRanges = request.Parameters["ipRanges"]!.ToString()!.Split(',').Select(ip => ip.Trim()).ToArray();
                    updateBody["ipRanges"] = ipRanges.Select(ip => new
                    {
                        odataType = "#microsoft.graph.iPv4CidrRange",
                        cidrAddress = ip.Contains('/') ? ip : $"{ip}/32"
                    }).ToArray();
                }

                var result = await PatchJsonAsync<JsonElement>(
                    $"{GraphBaseUrl}/identity/conditionalAccess/namedLocations/{locationId}",
                    updateBody);

                if (result.Success)
                {
                    LogOperationComplete(request, "UpdateNamedLocation", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"Named location updated successfully", new Dictionary<string, object>
                    {
                        { "locationId", locationId },
                        { "updatedFields", updateBody.Keys.ToList() }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error updating named location", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }

        /// <summary>
        /// Delete named location
        /// DELETE /identity/conditionalAccess/namedLocations/{id}
        /// Permission: Policy.ReadWrite.ConditionalAccess
        /// </summary>
        public async Task<XDRRemediationResponse> DeleteNamedLocationAsync(XDRRemediationRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                LogOperationStart(request, "DeleteNamedLocation");
                await SetAuthHeaderAsync(request.TenantId);

                var locationId = GetRequiredParameter(request, "locationId", out var failureResponse);
                if (locationId == null) return failureResponse!;

                var result = await DeleteAsync($"{GraphBaseUrl}/identity/conditionalAccess/namedLocations/{locationId}");

                if (result.Success)
                {
                    LogOperationComplete(request, "DeleteNamedLocation", DateTime.UtcNow - startTime, true);
                    
                    return CreateSuccessResponse(request, $"Named location deleted successfully", new Dictionary<string, object>
                    {
                        { "locationId", locationId },
                        { "status", "Deleted" }
                    }, startTime);
                }

                return CreateFailureResponse(request, result.Error ?? "Unknown error deleting named location", startTime);
            }
            catch (Exception ex)
            {
                return CreateExceptionResponse(request, ex, startTime);
            }
        }
    }
}
