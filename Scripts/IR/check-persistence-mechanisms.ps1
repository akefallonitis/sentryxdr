# Check Persistence Mechanisms
# Usage: check-persistence-mechanisms.ps1 -OutputPath <path>
# Purpose: Scan for common malware persistence techniques

param(
    [Parameter(Mandatory=$false)]
    [string]$OutputPath = "C:\Windows\Temp\persistence_scan"
)

try {
    Write-Host "=== Persistence Mechanism Scan ===" -ForegroundColor Cyan
    
    # Create output directory
    if (!(Test-Path $OutputPath)) {
        New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
    }
    
    $timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
    $findings = @()
    
    Write-Host "Scanning for persistence mechanisms..." -ForegroundColor Yellow
    
    # 1. Registry Run Keys
    Write-Host "Checking registry run keys..." -ForegroundColor Cyan
    $runKeys = @(
        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce",
        "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
        "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce",
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run"
    )
    
    foreach ($key in $runKeys) {
        if (Test-Path $key) {
            $entries = Get-ItemProperty -Path $key -ErrorAction SilentlyContinue
            foreach ($prop in $entries.PSObject.Properties) {
                if ($prop.Name -notin @('PSPath', 'PSParentPath', 'PSChildName', 'PSDrive', 'PSProvider')) {
                    $findings += @{
                        Type = "Registry Run Key"
                        Location = $key
                        Name = $prop.Name
                        Value = $prop.Value
                        Risk = "Medium"
                    }
                }
            }
        }
    }
    
    # 2. Startup Folder
    Write-Host "Checking startup folders..." -ForegroundColor Cyan
    $startupFolders = @(
        "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\Startup",
        "C:\ProgramData\Microsoft\Windows\Start Menu\Programs\Startup"
    )
    
    foreach ($folder in $startupFolders) {
        if (Test-Path $folder) {
            $items = Get-ChildItem -Path $folder -File
            foreach ($item in $items) {
                $findings += @{
                    Type = "Startup Folder"
                    Location = $folder
                    Name = $item.Name
                    Value = $item.FullName
                    Risk = "Low"
                }
            }
        }
    }
    
    # 3. Scheduled Tasks
    Write-Host "Checking scheduled tasks..." -ForegroundColor Cyan
    $suspiciousTasks = Get-ScheduledTask | Where-Object {
        $_.State -eq "Ready" -and 
        $_.TaskPath -notlike "\Microsoft\*"
    } | Select-Object -First 20
    
    foreach ($task in $suspiciousTasks) {
        $findings += @{
            Type = "Scheduled Task"
            Location = $task.TaskPath
            Name = $task.TaskName
            Value = $task.Actions.Execute
            Risk = "Medium"
        }
    }
    
    # 4. Services
    Write-Host "Checking suspicious services..." -ForegroundColor Cyan
    $suspiciousServices = Get-Service | Where-Object {
        $_.Status -eq "Running" -and
        $_.StartType -eq "Automatic" -and
        $_.DisplayName -notlike "Microsoft*" -and
        $_.DisplayName -notlike "Windows*"
    } | Select-Object -First 20
    
    foreach ($service in $suspiciousServices) {
        $servicePath = (Get-WmiObject Win32_Service -Filter "Name='$($service.Name)'").PathName
        $findings += @{
            Type = "Service"
            Location = "Services"
            Name = $service.Name
            Value = $servicePath
            Risk = "Medium"
        }
    }
    
    # 5. WMI Event Subscriptions
    Write-Host "Checking WMI event subscriptions..." -ForegroundColor Cyan
    try {
        $wmiSubs = Get-WmiObject -Namespace root\subscription -Class __EventFilter
        foreach ($sub in $wmiSubs) {
            $findings += @{
                Type = "WMI Event Subscription"
                Location = "root\subscription"
                Name = $sub.Name
                Value = $sub.Query
                Risk = "High"
            }
        }
    } catch {
        Write-Host "Could not check WMI subscriptions: $_" -ForegroundColor Yellow
    }
    
    # Generate report
    $reportFile = Join-Path $OutputPath "persistence_scan_$timestamp.html"
    
    $html = @"
<!DOCTYPE html>
<html>
<head>
    <title>Persistence Mechanism Scan Report</title>
    <style>
        body { font-family: Arial; margin: 20px; }
        h1 { color: #d9534f; }
        table { border-collapse: collapse; width: 100%; margin-top: 20px; }
        th { background-color: #5bc0de; color: white; padding: 10px; text-align: left; }
        td { border: 1px solid #ddd; padding: 8px; }
        tr:nth-child(even) { background-color: #f2f2f2; }
        .high { background-color: #d9534f; color: white; }
        .medium { background-color: #f0ad4e; color: white; }
        .low { background-color: #5cb85c; color: white; }
    </style>
</head>
<body>
    <h1>Persistence Mechanism Scan Report</h1>
    <p><strong>Scan Time:</strong> $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')</p>
    <p><strong>Computer:</strong> $env:COMPUTERNAME</p>
    <p><strong>Total Findings:</strong> $($findings.Count)</p>
    
    <table>
        <tr>
            <th>Type</th>
            <th>Location</th>
            <th>Name</th>
            <th>Value</th>
            <th>Risk</th>
        </tr>
        $(foreach ($finding in $findings) {
            "<tr>
                <td>$($finding.Type)</td>
                <td>$($finding.Location)</td>
                <td>$($finding.Name)</td>
                <td>$($finding.Value)</td>
                <td class='$($finding.Risk.ToLower())'>$($finding.Risk)</td>
            </tr>"
        })
    </table>
</body>
</html>
"@
    
    $html | Out-File $reportFile -Encoding UTF8
    
    Write-Host "`nPersistence scan complete!" -ForegroundColor Green
    Write-Host "Found $($findings.Count) potential persistence mechanisms" -ForegroundColor Green
    Write-Host "Report: $reportFile" -ForegroundColor Green
    
    @{
        Success = $true
        FindingsCount = $findings.Count
        Findings = $findings
        ReportFile = $reportFile
    } | ConvertTo-Json -Depth 3
    
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
    @{
        Success = $false
        Error = $_.Exception.Message
    } | ConvertTo-Json
    exit 1
}
