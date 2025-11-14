# Capture Network Traffic
# Usage: capture-network-traffic.ps1 -Duration <seconds> -OutputPath <path>
# Purpose: Capture network packets for analysis

param(
    [Parameter(Mandatory=$false)]
    [int]$Duration = 60,
    
    [Parameter(Mandatory=$false)]
    [string]$OutputPath = "C:\Windows\Temp\network_capture"
)

try {
    Write-Host "=== Network Traffic Capture ===" -ForegroundColor Cyan
    Write-Host "Capture Duration: $Duration seconds" -ForegroundColor Yellow
    
    # Create output directory
    if (!(Test-Path $OutputPath)) {
        New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
    }
    
    $timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
    $captureFile = Join-Path $OutputPath "network_capture_$timestamp.etl"
    $txtFile = Join-Path $OutputPath "network_capture_$timestamp.txt"
    
    # Check if netsh is available
    if (!(Get-Command netsh -ErrorAction SilentlyContinue)) {
        throw "netsh command not found"
    }
    
    Write-Host "Starting packet capture..." -ForegroundColor Yellow
    Write-Host "This will capture for $Duration seconds" -ForegroundColor Yellow
    
    # Start network trace
    $startTime = Get-Date
    netsh trace start capture=yes tracefile=$captureFile maxsize=500 overwrite=yes | Out-Null
    
    Write-Host "Capture started at $(Get-Date -Format 'HH:mm:ss')" -ForegroundColor Green
    Write-Host "Waiting for $Duration seconds..." -ForegroundColor Yellow
    
    # Wait for specified duration
    Start-Sleep -Seconds $Duration
    
    # Stop capture
    Write-Host "Stopping capture..." -ForegroundColor Yellow
    netsh trace stop | Out-Null
    
    $endTime = Get-Date
    $actualDuration = ($endTime - $startTime).TotalSeconds
    
    # Get current network connections for context
    Write-Host "Capturing current network state..." -ForegroundColor Yellow
    $connections = Get-NetTCPConnection | Select-Object LocalAddress, LocalPort, RemoteAddress, RemotePort, State, OwningProcess, @{
        Name='ProcessName';
        Expression={(Get-Process -Id $_.OwningProcess -ErrorAction SilentlyContinue).Name}
    }
    
    # Create summary
    $summaryFile = Join-Path $OutputPath "capture_summary_$timestamp.txt"
    @"
Network Traffic Capture Summary
================================
Capture Time: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
Computer: $env:COMPUTERNAME
Duration: $([math]::Round($actualDuration, 2)) seconds
Capture File: $captureFile
File Size: $([math]::Round((Get-Item $captureFile).Length / 1MB, 2)) MB

Active Connections at Capture Time:
$($ | Format-Table -AutoSize | Out-String)

Instructions:
1. Open capture file with Microsoft Message Analyzer or Wireshark
2. Convert ETL to PCAP: etl2pcapng $captureFile output.pcapng
3. Analyze with network forensics tools

NOTE: ETL format requires conversion for Wireshark analysis.
"@ | Out-File $summaryFile
    
    # Export connections to CSV
    $connectionsFile = Join-Path $OutputPath "connections_$timestamp.csv"
    $connections | Export-Csv -Path $connectionsFile -NoTypeInformation
    
    Write-Host "`nNetwork capture complete!" -ForegroundColor Green
    Write-Host "Capture File: $captureFile" -ForegroundColor Green
    Write-Host "File Size: $([math]::Round((Get-Item $captureFile).Length / 1MB, 2)) MB" -ForegroundColor Green
    Write-Host "Summary: $summaryFile" -ForegroundColor Green
    
    @{
        Success = $true
        CaptureFile = $captureFile
        FileSizeMB = [math]::Round((Get-Item $captureFile).Length / 1MB, 2)
        Duration = [math]::Round($actualDuration, 2)
        ConnectionsFile = $connectionsFile
        SummaryFile = $summaryFile
        ActiveConnections = $connections.Count
    } | ConvertTo-Json -Depth 3
    
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
    Write-Host "Note: This operation requires administrative privileges" -ForegroundColor Yellow
    @{
        Success = $false
        Error = $_.Exception.Message
    } | ConvertTo-Json
    exit 1
}
