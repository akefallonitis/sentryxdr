# Disable-OnPremComputer.ps1
# Azure Automation Runbook for disabling on-premise computer account
# Requires: ActiveDirectory PowerShell module
# Runs on: Hybrid Worker with domain access

param(
    [Parameter(Mandatory=$true)]
    [string]$ComputerName,
    
    [Parameter(Mandatory=$false)]
    [string]$IncidentId = "Unknown",
    
    [Parameter(Mandatory=$false)]
    [string]$Justification = "SentryXDR automated remediation"
)

try {
    Write-Output "=== Disable-OnPremComputer Runbook ==="
    Write-Output "Computer: $ComputerName"
    Write-Output "Incident ID: $IncidentId"
    Write-Output "Timestamp: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    
    Import-Module ActiveDirectory -ErrorAction Stop
    
    # Get computer object
    $computer = Get-ADComputer -Filter "Name -eq '$ComputerName'" -ErrorAction Stop
    
    if ($null -eq $computer) {
        throw "Computer not found: $ComputerName"
    }
    
    Write-Output "Found computer: $($computer.DistinguishedName)"
    
    # Disable computer account
    Disable-ADAccount -Identity $computer.DistinguishedName -ErrorAction Stop
    Write-Output "Computer account disabled successfully"
    
    # Add description
    $description = "DISABLED by SentryXDR - Incident: $IncidentId - Date: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') - Reason: $Justification"
    Set-ADComputer -Identity $computer.DistinguishedName -Description $description -ErrorAction Stop
    
    $result = @{
        Success = $true
        Message = "Computer disabled successfully"
        ComputerName = $ComputerName
        DistinguishedName = $computer.DistinguishedName
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
        ComputerName = $ComputerName
        IncidentId = $IncidentId
        Timestamp = Get-Date -Format 'o'
    }
    
    Write-Output "=== FAILURE ==="
    Write-Output ($errorResult | ConvertTo-Json -Depth 3)
    
    throw
}
