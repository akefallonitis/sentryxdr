# Quarantine Suspicious File
# Usage: quarantine-suspicious-file.ps1 -FilePath <path>
# Purpose: Move suspicious file to quarantine for analysis

param(
    [Parameter(Mandatory=$true)]
    [string]$FilePath,
    
    [Parameter(Mandatory=$false)]
    [string]$QuarantinePath = "C:\Windows\Temp\Quarantine"
)

try {
    Write-Host "=== File Quarantine Operation ===" -ForegroundColor Cyan
    Write-Host "Target File: $FilePath" -ForegroundColor Yellow
    
    # Verify file exists
    if (!(Test-Path $FilePath)) {
        throw "File not found: $FilePath"
    }
    
    # Create quarantine directory
    if (!(Test-Path $QuarantinePath)) {
        New-Item -ItemType Directory -Path $QuarantinePath -Force | Out-Null
    }
    
    # Get file info
    $file = Get-Item $FilePath
    $fileName = $file.Name
    $fileHash = (Get-FileHash -Path $FilePath -Algorithm SHA256).Hash
    
    Write-Host "File: $fileName" -ForegroundColor Yellow
    Write-Host "SHA256: $fileHash" -ForegroundColor Yellow
    
    # Create quarantine metadata
    $timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
    $quarantineFileName = "$timestamp`_$fileName"
    $quarantineFilePath = Join-Path $QuarantinePath $quarantineFileName
    $metadataFile = "$quarantineFilePath.metadata.txt"
    
    # Save metadata
    @"
Quarantine Metadata
==================
Original Path: $FilePath
Original Name: $fileName
File Size: $($file.Length) bytes
SHA256 Hash: $fileHash
Quarantine Time: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
Quarantined By: $env:USERNAME on $env:COMPUTERNAME
"@ | Out-File $metadataFile -Force
    
    # Move file to quarantine (secure move)
    Move-Item -Path $FilePath -Destination $quarantineFilePath -Force
    
    # Set quarantine file as read-only
    Set-ItemProperty -Path $quarantineFilePath -Name IsReadOnly -Value $true
    
    # Remove write permissions for all users
    $acl = Get-Acl $quarantineFilePath
    $acl.SetAccessRuleProtection($true, $false)
    Set-Acl $quarantineFilePath $acl
    
    Write-Host "File quarantined successfully!" -ForegroundColor Green
    Write-Host "Quarantine Location: $quarantineFilePath" -ForegroundColor Green
    
    @{
        Success = $true
        OriginalPath = $FilePath
        QuarantinePath = $quarantineFilePath
        MetadataFile = $metadataFile
        FileHash = $fileHash
    } | ConvertTo-Json
    
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
    @{
        Success = $false
        Error = $_.Exception.Message
    } | ConvertTo-Json
    exit 1
}
