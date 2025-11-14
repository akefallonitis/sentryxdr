# Collect Event Logs
# Usage: collect-event-logs.ps1 -LogNames <names> -Hours <hours> -OutputPath <path>
# Purpose: Export security and system event logs for analysis

param(
    [Parameter(Mandatory=$false)]
    [string[]]$LogNames = @("Security", "System", "Application"),
    
    [Parameter(Mandatory=$false)]
    [int]$Hours = 24,
    
    [Parameter(Mandatory=$false)]
    [string]$OutputPath = "C:\Windows\Temp\event_logs"
)

try {
    Write-Host "=== Event Log Collection ===" -ForegroundColor Cyan
    Write-Host "Collecting logs from last $Hours hours" -ForegroundColor Yellow
    
    # Create output directory
    if (!(Test-Path $OutputPath)) {
        New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
    }
    
    $timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
    $startTime = (Get-Date).AddHours(-$Hours)
    $collectedLogs = @()
    
    foreach ($logName in $LogNames) {
        try {
            Write-Host "Collecting $logName log..." -ForegroundColor Yellow
            
            # Export to EVTX format
            $evtxFile = Join-Path $OutputPath "$logName`_$timestamp.evtx"
            wevtutil epl $logName $evtxFile
            
            # Also export to CSV for easy analysis
            $csvFile = Join-Path $OutputPath "$logName`_$timestamp.csv"
            Get-WinEvent -LogName $logName -MaxEvents 10000 -ErrorAction SilentlyContinue |
                Where-Object { $_.TimeCreated -gt $startTime } |
                Select-Object TimeCreated, Id, LevelDisplayName, Message, ProviderName |
                Export-Csv -Path $csvFile -NoTypeInformation
            
            $eventCount = (Import-Csv $csvFile).Count
            
            $collectedLogs += @{
                LogName = $logName
                EvtxFile = $evtxFile
                CsvFile = $csvFile
                EventCount = $eventCount
                SizeMB = [math]::Round((Get-Item $evtxFile).Length / 1MB, 2)
            }
            
            Write-Host "$logName: $eventCount events collected" -ForegroundColor Green
            
        } catch {
            Write-Host "Failed to collect $logName`: $_" -ForegroundColor Red
        }
    }
    
    # Create summary
    $summaryFile = Join-Path $OutputPath "collection_summary_$timestamp.txt"
    @"
Event Log Collection Summary
=============================
Collection Time: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
Time Range: Last $Hours hours
Computer: $env:COMPUTERNAME
Total Logs Collected: $($collectedLogs.Count)

Collected Logs:
$($collectedLogs | ForEach-Object { "- $($_.LogName): $($_.EventCount) events ($($_.SizeMB) MB)" } | Out-String)
"@ | Out-File $summaryFile
    
    Write-Host "`nEvent log collection complete!" -ForegroundColor Green
    Write-Host "Summary: $summaryFile" -ForegroundColor Green
    
    @{
        Success = $true
        CollectedCount = $collectedLogs.Count
        CollectedLogs = $collectedLogs
        SummaryFile = $summaryFile
    } | ConvertTo-Json -Depth 3
    
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
    @{
        Success = $false
        Error = $_.Exception.Message
    } | ConvertTo-Json
    exit 1
}
