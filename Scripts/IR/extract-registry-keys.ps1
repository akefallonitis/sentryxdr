# Extract Registry Keys
# Usage: extract-registry-keys.ps1 -OutputPath <path>
# Purpose: Export critical registry keys for forensic analysis

param(
    [Parameter(Mandatory=$false)]
    [string]$OutputPath = "C:\Windows\Temp\registry_export"
)

try {
    Write-Host "=== Registry Key Extraction ===" -ForegroundColor Cyan
    
    # Create output directory
    if (!(Test-Path $OutputPath)) {
        New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
    }
    
    $timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
    $exportedKeys = @()
    
    # Define critical registry keys for forensics
    $criticalKeys = @(
        @{
            Name = "Autorun_CurrentUser"
            Path = "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run"
        },
        @{
            Name = "Autorun_LocalMachine"
            Path = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run"
        },
        @{
            Name = "RunOnce_CurrentUser"
            Path = "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce"
        },
        @{
            Name = "RunOnce_LocalMachine"
            Path = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce"
        },
        @{
            Name = "Services"
            Path = "HKLM:\SYSTEM\CurrentControlSet\Services"
        },
        @{
            Name = "Winlogon"
            Path = "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon"
        },
        @{
            Name = "ShellFolders"
            Path = "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Shell Folders"
        },
        @{
            Name = "ImageFileExecutionOptions"
            Path = "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options"
        }
    )
    
    Write-Host "Exporting $($criticalKeys.Count) registry locations..." -ForegroundColor Yellow
    
    foreach ($key in $criticalKeys) {
        try {
            if (Test-Path $key.Path) {
                $outputFile = Join-Path $OutputPath "$($key.Name)_$timestamp.reg"
                
                # Export to .reg file
                $regPath = $key.Path -replace "HKCU:", "HKEY_CURRENT_USER" -replace "HKLM:", "HKEY_LOCAL_MACHINE"
                reg export $regPath $outputFile /y | Out-Null
                
                # Also export to text for easy reading
                $textFile = "$outputFile.txt"
                Get-ItemProperty -Path $key.Path -ErrorAction SilentlyContinue | Format-List | Out-File $textFile
                
                $exportedKeys += @{
                    Name = $key.Name
                    Path = $key.Path
                    RegFile = $outputFile
                    TextFile = $textFile
                }
                
                Write-Host "Exported: $($key.Name)" -ForegroundColor Green
            } else {
                Write-Host "Not found: $($key.Name)" -ForegroundColor Yellow
            }
        } catch {
            Write-Host "Failed to export $($key.Name): $_" -ForegroundColor Red
        }
    }
    
    # Create summary report
    $summaryFile = Join-Path $OutputPath "extraction_summary_$timestamp.txt"
    @"
Registry Key Extraction Summary
================================
Extraction Time: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
Computer: $env:COMPUTERNAME
User: $env:USERNAME
Total Keys Exported: $($exportedKeys.Count)

Exported Keys:
$($exportedKeys | ForEach-Object { "- $($_.Name): $($_.RegFile)" } | Out-String)
"@ | Out-File $summaryFile
    
    Write-Host "`nRegistry extraction complete!" -ForegroundColor Green
    Write-Host "Exported $($exportedKeys.Count) registry keys" -ForegroundColor Green
    Write-Host "Summary: $summaryFile" -ForegroundColor Green
    
    @{
        Success = $true
        ExportedCount = $exportedKeys.Count
        ExportedKeys = $exportedKeys
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
