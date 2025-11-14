# Move-UserToQuarantineOU.ps1
# Azure Automation Runbook for moving user to quarantine OU
# Requires: ActiveDirectory PowerShell module
# Runs on: Hybrid Worker with domain access

param(
    [Parameter(Mandatory=$true)]
    [string]$UserPrincipalName,
    
    [Parameter(Mandatory=$false)]
    [string]$QuarantineOU = "OU=Quarantine,DC=domain,DC=com",
    
    [Parameter(Mandatory=$false)]
    [string]$IncidentId = "Unknown"
)

try {
    Write-Output "=== Move-UserToQuarantineOU Runbook ==="
    Write-Output "User: $UserPrincipalName"
    Write-Output "Target OU: $QuarantineOU"
    Write-Output "Incident ID: $IncidentId"
    
    Import-Module ActiveDirectory -ErrorAction Stop
    
    # Get user
    $user = Get-ADUser -Filter "UserPrincipalName -eq '$UserPrincipalName'" -ErrorAction Stop
    
    if ($null -eq $user) {
        throw "User not found: $UserPrincipalName"
    }
    
    $originalOU = $user.DistinguishedName
    Write-Output "Current location: $originalOU"
    
    # Move user to quarantine OU
    Move-ADObject -Identity $user.DistinguishedName -TargetPath $QuarantineOU -ErrorAction Stop
    Write-Output "User moved to quarantine OU successfully"
    
    # Add description
    $description = "QUARANTINED by SentryXDR - Incident: $IncidentId - Date: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') - Original OU: $originalOU"
    Set-ADUser -Filter "UserPrincipalName -eq '$UserPrincipalName'" -Description $description -ErrorAction Stop
    
    $result = @{
        Success = $true
        Message = "User moved to quarantine OU"
        UserPrincipalName = $UserPrincipalName
        OriginalOU = $originalOU
        QuarantineOU = $QuarantineOU
        IncidentId = $IncidentId
        Timestamp = Get-Date -Format 'o'
    }
    
    Write-Output "=== SUCCESS ==="
    Write-Output ($result | ConvertTo-Json -Depth 3)
    
    return $result
}
catch {
    Write-Error "ERROR: $($_.Exception.Message)"
    
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
