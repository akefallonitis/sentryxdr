# Start-DeltaSync.ps1
# Azure Automation Runbook for triggering Azure AD Connect delta sync
# Requires: ADSync PowerShell module
# Runs on: Hybrid Worker on Azure AD Connect server

param(
    [Parameter(Mandatory=$false)]
    [string]$IncidentId = "Unknown"
)

try {
    Write-Output "=== Start-DeltaSync Runbook ==="
    Write-Output "Incident ID: $IncidentId"
    Write-Output "Timestamp: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    
    # Import AD Sync module
    Import-Module ADSync -ErrorAction Stop
    Write-Output "ADSync module loaded successfully"
    
    # Start delta sync
    Start-ADSyncSyncCycle -PolicyType Delta -ErrorAction Stop
    Write-Output "Delta sync initiated successfully"
    
    # Wait a moment to get initial status
    Start-Sleep -Seconds 2
    
    # Get sync status
    $syncStatus = Get-ADSyncScheduler -ErrorAction SilentlyContinue
    
    $result = @{
        Success = $true
        Message = "Delta sync initiated successfully"
        SyncType = "Delta"
        IncidentId = $IncidentId
        SyncSchedulerEnabled = $syncStatus.SyncCycleEnabled
        NextSyncCycle = $syncStatus.NextSyncCyclePolicyType
        Timestamp = Get-Date -Format 'o'
        Note = "Changes will sync to Azure AD within 2-5 minutes"
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
        IncidentId = $IncidentId
        Timestamp = Get-Date -Format 'o'
        Note = "Ensure ADSync module is installed and runbook is running on Azure AD Connect server"
    }
    
    Write-Output "=== FAILURE ==="
    Write-Output ($errorResult | ConvertTo-Json -Depth 3)
    
    throw
}
