# Collect Network Connections
# Usage: collect-network-connections.ps1 -OutputPath <path>
# Purpose: Capture active network connections for forensic analysis

param(
    [Parameter(Mandatory=$false)]
    [string]$OutputPath = "C:\Windows\Temp\network_collection"
)

try {
    Write-Host "=== Network Connections Collection ===" -ForegroundColor Cyan
    
    # Create output directory
    if (!(Test-Path $OutputPath)) {
        New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
    }
    
    $timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
    $outputFile = Join-Path $OutputPath "network_connections_$timestamp.txt"
    
    Write-Host "Collecting network data..." -ForegroundColor Yellow
    
    # Collect netstat data
    "=== Active Network Connections (netstat) ===" | Out-File $outputFile
    netstat -ano | Out-File $outputFile -Append
    
    # Collect TCP connections with process info
    "`n`n=== TCP Connections with Process Info ===" | Out-File $outputFile -Append
    Get-NetTCPConnection | Select-Object LocalAddress, LocalPort, RemoteAddress, RemotePort, State, OwningProcess, @{
        Name='ProcessName';
        Expression={(Get-Process -Id $_.OwningProcess -ErrorAction SilentlyContinue).Name}
    } | Format-Table -AutoSize | Out-File $outputFile -Append
    
    # Collect UDP endpoints
    "`n`n=== UDP Endpoints ===" | Out-File $outputFile -Append
    Get-NetUDPEndpoint | Select-Object LocalAddress, LocalPort, OwningProcess, @{
        Name='ProcessName';
        Expression={(Get-Process -Id $_.OwningProcess -ErrorAction SilentlyContinue).Name}
    } | Format-Table -AutoSize | Out-File $outputFile -Append
    
    # Collect DNS cache
    "`n`n=== DNS Cache ===" | Out-File $outputFile -Append
    Get-DnsClientCache | Select-Object Entry, Name, Type, TimeToLive, DataLength, Data | Format-Table -AutoSize | Out-File $outputFile -Append
    
    # Collect routing table
    "`n`n=== Routing Table ===" | Out-File $outputFile -Append
    route print | Out-File $outputFile -Append
    
    # Collect ARP cache
    "`n`n=== ARP Cache ===" | Out-File $outputFile -Append
    arp -a | Out-File $outputFile -Append
    
    Write-Host "Network connections collected successfully: $outputFile" -ForegroundColor Green
    
    @{
        Success = $true
        OutputFile = $outputFile
        FileSizeMB = [math]::Round((Get-Item $outputFile).Length / 1MB, 2)
    } | ConvertTo-Json
    
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
    @{
        Success = $false
        Error = $_.Exception.Message
    } | ConvertTo-Json
    exit 1
}
