# Collect Process Memory Dump
# Usage: collect-process-memory.ps1 -ProcessName <name> -OutputPath <path>
# Purpose: Dump suspicious process memory for forensic analysis

param(
    [Parameter(Mandatory=$true)]
    [string]$ProcessName,
    
    [Parameter(Mandatory=$false)]
    [string]$OutputPath = "C:\Windows\Temp\memory_dump"
)

try {
    Write-Host "=== Process Memory Collection ===" -ForegroundColor Cyan
    Write-Host "Target Process: $ProcessName" -ForegroundColor Yellow
    
    # Create output directory
    if (!(Test-Path $OutputPath)) {
        New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
    }
    
    # Get process
    $process = Get-Process -Name $ProcessName -ErrorAction Stop
    Write-Host "Found process: $($process.Name) (PID: $($process.Id))" -ForegroundColor Green
    
    # Create dump file
    $dumpFile = Join-Path $OutputPath "$ProcessName`_$($process.Id)_$(Get-Date -Format 'yyyyMMdd_HHmmss').dmp"
    
    # Use procdump if available, otherwise use Windows Error Reporting
    if (Test-Path "C:\Windows\System32\procdump.exe") {
        & procdump.exe -ma $process.Id $dumpFile -accepteula
    } else {
        # Use rundll32 with comsvcs.dll as fallback
        $command = "rundll32.exe C:\Windows\System32\comsvcs.dll, MiniDump $($process.Id) $dumpFile full"
        Invoke-Expression $command
    }
    
    if (Test-Path $dumpFile) {
        $fileSize = (Get-Item $dumpFile).Length / 1MB
        Write-Host "Memory dump created successfully: $dumpFile" -ForegroundColor Green
        Write-Host "File size: $([math]::Round($fileSize, 2)) MB" -ForegroundColor Green
        
        # Return result
        @{
            Success = $true
            ProcessName = $ProcessName
            ProcessId = $process.Id
            DumpFile = $dumpFile
            FileSizeMB = [math]::Round($fileSize, 2)
        } | ConvertTo-Json
    } else {
        throw "Failed to create memory dump"
    }
    
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
    @{
        Success = $false
        Error = $_.Exception.Message
    } | ConvertTo-Json
    exit 1
}
