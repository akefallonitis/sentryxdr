# Disable-OnPremUser.ps1
# Azure Automation Runbook for disabling on-premise Active Directory user
# Requires: ActiveDirectory PowerShell module
# Runs on: Hybrid Worker with domain access

param(
    [Parameter(Mandatory=$true)]
    [string]$UserPrincipalName,
    
    [Parameter(Mandatory=$false)]
    [string]$IncidentId = "Unknown",
    
    [Parameter(Mandatory=$false)]
    [string]$Justification = "SentryXDR automated remediation"
)

try {
    Write-Output "=== Disable-OnPremUser Runbook ==="
    Write-Output "User: $UserPrincipalName"
    Write-Output "Incident ID: $IncidentId"
    Write-Output "Justification: $Justification"
    Write-Output "Timestamp: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    
    # Import Active Directory module
    Import-Module ActiveDirectory -ErrorAction Stop
    Write-Output "Active Directory module loaded successfully"
    
    # Get user object
    $user = Get-ADUser -Filter "UserPrincipalName -eq '$UserPrincipalName'" -ErrorAction Stop
    
    if ($null -eq $user) {
        throw "User not found: $UserPrincipalName"
    }
    
    Write-Output "Found user: $($user.DistinguishedName)"
    
    # Disable the user account
    Disable-ADAccount -Identity $user.DistinguishedName -ErrorAction Stop
    Write-Output "User account disabled successfully"
    
    # Add description with incident info
    $description = "DISABLED by SentryXDR - Incident: $IncidentId - Date: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') - Reason: $Justification"
    Set-ADUser -Identity $user.DistinguishedName -Description $description -ErrorAction Stop
    Write-Output "Added incident information to user description"
    
    # Log to event log (optional)
    Write-EventLog -LogName Application -Source "SentryXDR" -EventId 1001 -EntryType Warning -Message "User disabled: $UserPrincipalName - Incident: $IncidentId" -ErrorAction SilentlyContinue
    
    # Return success
    $result = @{
        Success = $true
        Message = "User disabled successfully"
        UserPrincipalName = $UserPrincipalName
        DistinguishedName = $user.DistinguishedName
        IncidentId = $IncidentId
        Timestamp = Get-Date -Format 'o'
    }
    
    Write-Output "=== SUCCESS ==="
    Write-Output ($result | ConvertTo-Json -Depth 3)
    
    return $result
}
catch {
    Write-Error "ERROR: $($_.Exception.Message)"
    Write-Error "Stack Trace: $($_.ScriptStackTrace)"
    
    $errorResult = @{
        Success = $false
        Error = $_.Exception.Message
        UserPrincipalName = $UserPrincipalName
        IncidentId = $IncidentId
        Timestamp = Get-Date -Format 'o'
    }
    
    Write-Output "=== FAILURE ==="
    Write-Output ($errorResult | ConvertTo-Json -Depth 3)
    
    throw
}
