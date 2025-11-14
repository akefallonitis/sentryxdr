# Enumerate Drivers
# Usage: enumerate-drivers.ps1 -OutputPath <path>
# Purpose: List all loaded drivers for rootkit/malware detection

param(
    [Parameter(Mandatory=$false)]
    [string]$OutputPath = "C:\Windows\Temp\driver_enum"
)

try {
    Write-Host "=== Driver Enumeration ===" -ForegroundColor Cyan
    
    # Create output directory
    if (!(Test-Path $OutputPath)) {
        New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
    }
    
    $timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
    $drivers = @()
    
    Write-Host "Enumerating loaded drivers..." -ForegroundColor Yellow
    
    # Get all loaded drivers
    $loadedDrivers = Get-WmiObject Win32_SystemDriver | Sort-Object Name
    
    foreach ($driver in $loadedDrivers) {
        $driverInfo = @{
            Name = $driver.Name
            DisplayName = $driver.DisplayName
            Description = $driver.Description
            State = $driver.State
            Status = $driver.Status
            PathName = $driver.PathName
            StartMode = $driver.StartMode
            Started = $driver.Started
        }
        
        # Try to get file info and hash if path exists
        if ($driver.PathName -and (Test-Path $driver.PathName)) {
            try {
                $fileInfo = Get-Item $driver.PathName
                $hash = (Get-FileHash -Path $driver.PathName -Algorithm SHA256 -ErrorAction SilentlyContinue).Hash
                
                $driverInfo.FileSize = $fileInfo.Length
                $driverInfo.FileVersion = $fileInfo.VersionInfo.FileVersion
                $driverInfo.Company = $fileInfo.VersionInfo.CompanyName
                $driverInfo.SHA256 = $hash
                $driverInfo.IsSigned = $fileInfo.VersionInfo.LegalCopyright -ne $null
            } catch {
                $driverInfo.Error = "Could not read file info"
            }
        }
        
        $drivers += $driverInfo
    }
    
    # Identify suspicious drivers (unsigned, unknown companies, unusual paths)
    $suspiciousDrivers = $drivers | Where-Object {
        ($_.Company -notlike "Microsoft*" -and $_.Company -notlike "*Intel*" -and $_.Company -notlike "*AMD*") -or
        ($_.PathName -notlike "*\Windows\*" -and $_.PathName -notlike "*\Program Files*") -or
        ($_.IsSigned -eq $false)
    }
    
    # Export to CSV
    $csvFile = Join-Path $OutputPath "drivers_$timestamp.csv"
    $drivers | Export-Csv -Path $csvFile -NoTypeInformation
    
    # Export suspicious drivers
    $suspiciousCsv = Join-Path $OutputPath "suspicious_drivers_$timestamp.csv"
    $suspiciousDrivers | Export-Csv -Path $suspiciousCsv -NoTypeInformation
    
    # Create summary
    $summaryFile = Join-Path $OutputPath "driver_summary_$timestamp.txt"
    @"
Driver Enumeration Summary
==========================
Enumeration Time: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
Computer: $env:COMPUTERNAME
Total Drivers: $($drivers.Count)
Running Drivers: $($drivers | Where-Object { $_.State -eq 'Running' } | Measure-Object).Count
Suspicious Drivers: $($suspiciousDrivers.Count)

Suspicious Drivers:
$(if ($suspiciousDrivers.Count -gt 0) {
    $suspiciousDrivers | ForEach-Object {
        "- $($_.Name): $($_.PathName) (Company: $($_.Company))"
    } | Out-String
} else {
    "None detected"
})

Output Files:
- All Drivers: $csvFile
- Suspicious Drivers: $suspiciousCsv
"@ | Out-File $summaryFile
    
    Write-Host "`nDriver enumeration complete!" -ForegroundColor Green
    Write-Host "Total drivers: $($drivers.Count)" -ForegroundColor Green
    Write-Host "Suspicious drivers: $($suspiciousDrivers.Count)" -ForegroundColor $(if ($suspiciousDrivers.Count -gt 0) { 'Red' } else { 'Green' })
    Write-Host "Summary: $summaryFile" -ForegroundColor Green
    
    @{
        Success = $true
        TotalDrivers = $drivers.Count
        SuspiciousCount = $suspiciousDrivers.Count
        SuspiciousDrivers = $suspiciousDrivers
        CsvFile = $csvFile
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
