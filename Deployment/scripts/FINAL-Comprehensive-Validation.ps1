# FINAL-Comprehensive-Validation.ps1
# Complete end-to-end validation of SentryXDR production readiness

param(
    [switch]$ValidateARM,
    [switch]$ValidateBuild,
    [switch]$ValidatePackage,
    [switch]$ValidateDocs,
    [switch]$All
)

$ErrorActionPreference = "Continue"

Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "SentryXDR - FINAL Comprehensive Validation" -ForegroundColor Cyan
Write-Host "Version 1.0.0 - Production Readiness Check" -ForegroundColor Cyan
Write-Host "======================================================`n" -ForegroundColor Cyan

$validationResults = @{
    Build = $null
    ARMTemplate = $null
    Package = $null
    Documentation = $null
    Tags = $null
    Permissions = $null
    Overall = $null
}

# ==================== BUILD VALIDATION ====================
if ($All -or $ValidateBuild) {
    Write-Host "[1/6] Build Validation..." -ForegroundColor Yellow
    
    try {
        $buildResult = dotnet build --configuration Release --no-restore 2>&1 | Out-String
        
        if ($buildResult -match "Build succeeded") {
            $validationResults.Build = @{
                Status = "PASS"
                Errors = 0
                Details = "Build succeeded with 0 errors"
            }
            Write-Host "  ? Build: PASS (0 errors)" -ForegroundColor Green
        }
        else {
            $errorCount = ([regex]::Matches($buildResult, "error CS")).Count
            $validationResults.Build = @{
                Status = "FAIL"
                Errors = $errorCount
                Details = "Build failed with $errorCount errors"
            }
            Write-Host "  ? Build: FAIL ($errorCount errors)" -ForegroundColor Red
        }
    }
    catch {
        $validationResults.Build = @{
            Status = "ERROR"
            Details = $_.Exception.Message
        }
        Write-Host "  ? Build: ERROR - $_" -ForegroundColor Red
    }
}

# ==================== ARM TEMPLATE VALIDATION ====================
if ($All -or $ValidateARM) {
    Write-Host "[2/6] ARM Template Validation..." -ForegroundColor Yellow
    
    $armPath = "Deployment\azuredeploy.json"
    
    if (Test-Path $armPath) {
        try {
            $armContent = Get-Content $armPath -Raw | ConvertFrom-Json
            
            $checks = @{
                "Has Parameters" = ($null -ne $armContent.parameters)
                "Has Resources" = ($null -ne $armContent.resources)
                "Has Outputs" = ($null -ne $armContent.outputs)
                "Has Variables" = ($null -ne $armContent.variables)
                "Has Common Tags" = ($null -ne $armContent.variables.commonTags)
            }
            
            $passCount = ($checks.Values | Where-Object { $_ -eq $true }).Count
            $totalChecks = $checks.Count
            
            if ($passCount -eq $totalChecks) {
                $validationResults.ARMTemplate = @{
                    Status = "PASS"
                    Details = "All $totalChecks ARM template checks passed"
                    Checks = $checks
                }
                Write-Host "  ? ARM Template: PASS ($passCount/$totalChecks checks)" -ForegroundColor Green
            }
            else {
                $validationResults.ARMTemplate = @{
                    Status = "PARTIAL"
                    Details = "$passCount/$totalChecks checks passed"
                    Checks = $checks
                }
                Write-Host "  ??  ARM Template: PARTIAL ($passCount/$totalChecks checks)" -ForegroundColor Yellow
            }
            
            # Tag validation
            $requiredTags = @("Project", "Environment", "CreatedBy", "ManagedBy", "Version", 
                            "ApplicationName", "BusinessUnit", "CostCenter", "DataClassification", 
                            "Criticality", "DisasterRecovery", "MaintenanceWindow")
            
            $presentTags = $armContent.variables.commonTags.PSObject.Properties.Name
            $missingTags = $requiredTags | Where-Object { $_ -notin $presentTags }
            
            if ($missingTags.Count -eq 0) {
                Write-Host "  ? Tags: All $($requiredTags.Count) compliance tags present" -ForegroundColor Green
                $validationResults.Tags = @{ Status = "PASS"; Details = "All compliance tags present" }
            }
            else {
                Write-Host "  ??  Tags: Missing $($missingTags.Count) tags: $($missingTags -join ', ')" -ForegroundColor Yellow
                $validationResults.Tags = @{ Status = "PARTIAL"; MissingTags = $missingTags }
            }
        }
        catch {
            $validationResults.ARMTemplate = @{
                Status = "ERROR"
                Details = $_.Exception.Message
            }
            Write-Host "  ? ARM Template: ERROR - $_" -ForegroundColor Red
        }
    }
    else {
        Write-Host "  ? ARM Template: NOT FOUND" -ForegroundColor Red
        $validationResults.ARMTemplate = @{ Status = "FAIL"; Details = "File not found" }
    }
}

# ==================== PACKAGE VALIDATION ====================
if ($All -or $ValidatePackage) {
    Write-Host "[3/6] Deployment Package Validation..." -ForegroundColor Yellow
    
    $packagePath = "sentryxdr-deploy.zip"
    
    if (Test-Path $packagePath) {
        $packageSize = (Get-Item $packagePath).Length / 1MB
        
        # Extract and check structure
        $tempExtract = "temp-validation-extract"
        if (Test-Path $tempExtract) { Remove-Item $tempExtract -Recurse -Force }
        
        try {
            Expand-Archive -Path $packagePath -DestinationPath $tempExtract -Force
            
            $checks = @{
                "host.json present" = (Test-Path "$tempExtract\host.json")
                "SentryXDR.dll present" = (Test-Path "$tempExtract\SentryXDR.dll")
                "Size < 100MB" = ($packageSize -lt 100)
                "Files at root" = ((Get-ChildItem $tempExtract).Count -gt 10)
            }
            
            $passCount = ($checks.Values | Where-Object { $_ -eq $true }).Count
            
            if ($passCount -eq $checks.Count) {
                $validationResults.Package = @{
                    Status = "PASS"
                    Size = [math]::Round($packageSize, 2)
                    Details = "Package valid, $([math]::Round($packageSize, 2)) MB"
                }
                Write-Host "  ? Package: PASS ($([math]::Round($packageSize, 2)) MB, $passCount/$($checks.Count) checks)" -ForegroundColor Green
            }
            else {
                Write-Host "  ??  Package: PARTIAL ($passCount/$($checks.Count) checks passed)" -ForegroundColor Yellow
                $validationResults.Package = @{ Status = "PARTIAL"; Checks = $checks }
            }
            
            Remove-Item $tempExtract -Recurse -Force
        }
        catch {
            Write-Host "  ? Package: ERROR - $_" -ForegroundColor Red
            $validationResults.Package = @{ Status = "ERROR"; Details = $_.Exception.Message }
        }
    }
    else {
        Write-Host "  ? Package: NOT FOUND" -ForegroundColor Red
        $validationResults.Package = @{ Status = "FAIL"; Details = "Package not found" }
    }
}

# ==================== DOCUMENTATION VALIDATION ====================
if ($All -or $ValidateDocs) {
    Write-Host "[4/6] Documentation Validation..." -ForegroundColor Yellow
    
    $essentialDocs = @(
        "README.md",
        "LICENSE",
        "CONTRIBUTING.md",
        "DEPLOYMENT_GUIDE.md",
        "FINAL_PROJECT_STATUS.md",
        "PRODUCTION_READINESS_CHECKLIST.md",
        "FINAL_STATUS.md"
    )
    
    $presentDocs = $essentialDocs | Where-Object { Test-Path $_ }
    $missingDocs = $essentialDocs | Where-Object { -not (Test-Path $_) }
    
    if ($missingDocs.Count -eq 0) {
        $validationResults.Documentation = @{
            Status = "PASS"
            Details = "All $($essentialDocs.Count) essential docs present"
        }
        Write-Host "  ? Documentation: PASS ($($essentialDocs.Count)/$($essentialDocs.Count) files)" -ForegroundColor Green
        
        # Check README for Deploy button
        $readme = Get-Content "README.md" -Raw
        if ($readme -match "Deploy to Azure") {
            Write-Host "  ? Deploy Button: Present in README.md" -ForegroundColor Green
        }
        else {
            Write-Host "  ??  Deploy Button: Not found in README.md" -ForegroundColor Yellow
        }
    }
    else {
        Write-Host "  ??  Documentation: PARTIAL ($($presentDocs.Count)/$($essentialDocs.Count) files)" -ForegroundColor Yellow
        Write-Host "     Missing: $($missingDocs -join ', ')" -ForegroundColor Yellow
        $validationResults.Documentation = @{
            Status = "PARTIAL"
            Present = $presentDocs.Count
            Missing = $missingDocs
        }
    }
}

# ==================== PERMISSIONS VALIDATION ====================
Write-Host "[5/6] Permissions Script Validation..." -ForegroundColor Yellow

$permScript = "Deployment\scripts\Setup-SentryXDR-Permissions-COMPLETE.ps1"
if (Test-Path $permScript) {
    $permContent = Get-Content $permScript -Raw
    
    $permissionCount = ([regex]::Matches($permContent, '"[^"]*\.Read[^"]*"')).Count
    $permissionCount += ([regex]::Matches($permContent, '"[^"]*\.ReadWrite[^"]*"')).Count
    
    if ($permissionCount -gt 50) {
        Write-Host "  ? Permissions: $permissionCount API permissions configured" -ForegroundColor Green
        $validationResults.Permissions = @{ Status = "PASS"; Count = $permissionCount }
    }
    else {
        Write-Host "  ??  Permissions: Only $permissionCount permissions found (expected 60+)" -ForegroundColor Yellow
        $validationResults.Permissions = @{ Status = "PARTIAL"; Count = $permissionCount }
    }
}
else {
    Write-Host "  ? Permissions: Script not found" -ForegroundColor Red
    $validationResults.Permissions = @{ Status = "FAIL" }
}

# ==================== GIT STATUS ====================
Write-Host "[6/6] Git Repository Validation..." -ForegroundColor Yellow

try {
    $gitStatus = git status --porcelain 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        if ([string]::IsNullOrWhiteSpace($gitStatus)) {
            Write-Host "  ? Git: Clean working directory" -ForegroundColor Green
        }
        else {
            $changedFiles = ($gitStatus -split "`n").Count
            Write-Host "  ??  Git: $changedFiles uncommitted changes" -ForegroundColor Yellow
        }
        
        $branch = git branch --show-current
        Write-Host "  ??  Branch: $branch" -ForegroundColor Cyan
    }
}
catch {
    Write-Host "  ??  Git: Unable to check status" -ForegroundColor Yellow
}

# ==================== OVERALL ASSESSMENT ====================
Write-Host "`n======================================================" -ForegroundColor Cyan
Write-Host "OVERALL ASSESSMENT" -ForegroundColor Cyan
Write-Host "======================================================" -ForegroundColor Cyan

$passCount = ($validationResults.Values | Where-Object { $_.Status -eq "PASS" }).Count
$totalChecks = ($validationResults.Values | Where-Object { $null -ne $_ }).Count

$overallStatus = if ($passCount -eq $totalChecks) {
    "? PRODUCTION READY"
}
elseif ($passCount / $totalChecks -gt 0.8) {
    "??  MOSTLY READY (minor issues)"
}
else {
    "? NOT READY (significant issues)"
}

Write-Host "`nStatus: $overallStatus" -ForegroundColor $(
    if ($passCount -eq $totalChecks) { "Green" }
    elseif ($passCount / $totalChecks -gt 0.8) { "Yellow" }
    else { "Red" }
)
Write-Host "Checks Passed: $passCount / $totalChecks" -ForegroundColor Cyan

# Summary
Write-Host "`nComponent Status:" -ForegroundColor Cyan
foreach ($key in $validationResults.Keys) {
    if ($null -ne $validationResults[$key]) {
        $status = $validationResults[$key].Status
        $color = switch ($status) {
            "PASS" { "Green" }
            "PARTIAL" { "Yellow" }
            "FAIL" { "Red" }
            "ERROR" { "Red" }
            default { "Gray" }
        }
        Write-Host "  $key`: $status" -ForegroundColor $color
    }
}

Write-Host "`n======================================================" -ForegroundColor Cyan
Write-Host "Validation Complete!" -ForegroundColor Cyan
Write-Host "======================================================`n" -ForegroundColor Cyan

# Return results for scripting
return $validationResults
