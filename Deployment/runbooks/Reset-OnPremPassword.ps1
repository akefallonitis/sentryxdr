# Reset-OnPremPassword.ps1
# Azure Automation Runbook for resetting on-premise Active Directory user password
# Requires: ActiveDirectory PowerShell module
# Runs on: Hybrid Worker with domain access

param(
    [Parameter(Mandatory=$true)]
    [string]$UserPrincipalName,
    
    [Parameter(Mandatory=$true)]
    [string]$NewPassword,
    
    [Parameter(Mandatory=$false)]
    [string]$IncidentId = "Unknown",
    
    [Parameter(Mandatory=$false)]
    [string]$Justification = "SentryXDR automated remediation"
)

try {
    Write-Output "=== Reset-OnPremPassword Runbook ==="
    Write-Output "User: $UserPrincipalName"
    Write-Output "Incident ID: $IncidentId"
    Write-Output "Timestamp: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    
    # Import Active Directory module
    Import-Module ActiveDirectory -ErrorAction Stop
    
    # Get user object
    $user = Get-ADUser -Filter "UserPrincipalName -eq '$UserPrincipalName'" -ErrorAction Stop
    
    if ($null -eq $user) {
        throw "User not found: $UserPrincipalName"
    }
    
    Write-Output "Found user: $($user.DistinguishedName)"
    
    # Convert password to secure string
    $securePassword = ConvertTo-SecureString -String $NewPassword -AsPlainText -Force
    
    # Reset password
    Set-ADAccountPassword -Identity $user.DistinguishedName -NewPassword $securePassword -Reset -ErrorAction Stop
    Write-Output "Password reset successfully"
    
    # Force password change at next logon
    Set-ADUser -Identity $user.DistinguishedName -ChangePasswordAtLogon $true -ErrorAction Stop
    Write-Output "User must change password at next logon"
    
    # Add description
    $description = "PASSWORD RESET by SentryXDR - Incident: $IncidentId - Date: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    Set-ADUser -Identity $user.DistinguishedName -Description $description -ErrorAction Stop
    
    $result = @{
        Success = $true
        Message = "Password reset successfully"
        UserPrincipalName = $UserPrincipalName
        DistinguishedName = $user.DistinguishedName
        IncidentId = $IncidentId
        ChangePasswordRequired = $true
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
