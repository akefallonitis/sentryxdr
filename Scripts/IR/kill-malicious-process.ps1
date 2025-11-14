# Kill Malicious Process
# Usage: kill-malicious-process.ps1 -ProcessName <name> -Force
# Purpose: Terminate malicious process and prevent restart

param(
    [Parameter(Mandatory=$true)]
    [string]$ProcessName,
    
    [Parameter(Mandatory=$false)]
    [switch]$Force
)

try {
    Write-Host "=== Process Termination ===" -ForegroundColor Cyan
    Write-Host "Target Process: $ProcessName" -ForegroundColor Yellow
    
    # Get all instances of the process
    $processes = Get-Process -Name $ProcessName -ErrorAction SilentlyContinue
    
    if ($processes.Count -eq 0) {
        Write-Host "Process not found: $ProcessName" -ForegroundColor Yellow
        @{
            Success = $true
            Message = "Process not running"
            ProcessName = $ProcessName
        } | ConvertTo-Json
        exit 0
    }
    
    $killedProcesses = @()
    
    foreach ($process in $processes) {
        Write-Host "Found process: $($process.Name) (PID: $($process.Id))" -ForegroundColor Yellow
        
        # Get process details before killing
        $processInfo = @{
            Name = $process.Name
            Id = $process.Id
            Path = $process.Path
            CommandLine = (Get-WmiObject Win32_Process -Filter "ProcessId = $($process.Id)").CommandLine
            StartTime = $process.StartTime
            CPU = $process.CPU
            Memory = $process.WorkingSet64
        }
        
        try {
            # Try graceful stop first
            if (!$Force) {
                $process.CloseMainWindow() | Out-Null
                Start-Sleep -Seconds 2
            }
            
            # Force kill if still running
            if (!$process.HasExited) {
                Stop-Process -Id $process.Id -Force
                Write-Host "Process terminated: PID $($process.Id)" -ForegroundColor Green
            }
            
            $killedProcesses += $processInfo
            
        } catch {
            Write-Host "Failed to kill PID $($process.Id): $_" -ForegroundColor Red
        }
    }
    
    # Disable automatic restart via registry (common malware technique)
    $runKeys = @(
        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce",
        "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
        "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce"
    )
    
    $removedEntries = @()
    foreach ($key in $runKeys) {
        if (Test-Path $key) {
            $entries = Get-ItemProperty -Path $key -ErrorAction SilentlyContinue
            foreach ($entry in $entries.PSObject.Properties) {
                if ($entry.Value -like "*$ProcessName*") {
                    Remove-ItemProperty -Path $key -Name $entry.Name -Force -ErrorAction SilentlyContinue
                    $removedEntries += "$key\$($entry.Name)"
                    Write-Host "Removed autostart entry: $key\$($entry.Name)" -ForegroundColor Green
                }
            }
        }
    }
    
    Write-Host "Process termination complete!" -ForegroundColor Green
    Write-Host "Killed $($killedProcesses.Count) process(es)" -ForegroundColor Green
    
    @{
        Success = $true
        ProcessName = $ProcessName
        KilledCount = $killedProcesses.Count
        KilledProcesses = $killedProcesses
        RemovedAutostartEntries = $removedEntries
    } | ConvertTo-Json -Depth 3
    
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
    @{
        Success = $false
        Error = $_.Exception.Message
    } | ConvertTo-Json
    exit 1
}
