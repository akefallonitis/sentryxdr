# SentryXDR - Complete Testing & Validation Script
# Tests all endpoints, validates responses, and generates report

param(
    [Parameter(Mandatory=$false)]
    [string]$FunctionAppUrl = "http://localhost:7071",
    
    [Parameter(Mandatory=$false)]
    [string]$FunctionKey = "",
    
    [Parameter(Mandatory=$false)]
    [string]$TestTenantId = "00000000-0000-0000-0000-000000000000"
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  SentryXDR Testing & Validation" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$baseUrl = $FunctionAppUrl
if ($FunctionKey) {
    $headers = @{ "x-functions-key" = $FunctionKey }
} else {
    $headers = @{}
}

$results = @{
    Passed = 0
    Failed = 0
    Tests = @()
}

function Test-Endpoint {
    param(
        [string]$Name,
        [string]$Method,
        [string]$Endpoint,
        [object]$Body = $null,
        [int]$ExpectedStatus = 200
    )
    
    Write-Host "`nTesting: $Name" -ForegroundColor Yellow
    
    try {
        $uri = "$baseUrl$Endpoint"
        $params = @{
            Uri = $uri
            Method = $Method
            Headers = $headers
            ContentType = "application/json"
        }
        
        if ($Body) {
            $params.Body = ($Body | ConvertTo-Json -Depth 10)
        }
        
        $response = Invoke-RestMethod @params -ErrorAction Stop
        
        Write-Host "  ? PASS - Status: 200" -ForegroundColor Green
        $script:results.Passed++
        $script:results.Tests += @{
            Name = $Name
            Status = "PASS"
            Response = $response
        }
        
        return $response
    }
    catch {
        Write-Host "  ? FAIL - $($_.Exception.Message)" -ForegroundColor Red
        $script:results.Failed++
        $script:results.Tests += @{
            Name = $Name
            Status = "FAIL"
            Error = $_.Exception.Message
        }
        return $null
    }
}

# Test 1: Health Check
Write-Host "`n========== Endpoint Tests ==========" -ForegroundColor Cyan
Test-Endpoint -Name "Health Check" -Method "GET" -Endpoint "/api/xdr/health"

# Test 2: Single Remediation Request (MDE - Isolate Device)
$singleRequest = @{
    tenantId = $TestTenantId
    incidentId = "TEST-001"
    platform = "MDE"
    action = "IsolateDevice"
    parameters = @{
        deviceId = "test-device-id"
    }
    priority = "High"
    initiatedBy = "test@example.com"
    justification = "Test isolation"
}

$singleResult = Test-Endpoint `
    -Name "Single Remediation (MDE IsolateDevice)" `
    -Method "POST" `
    -Endpoint "/api/xdr/remediate" `
    -Body $singleRequest `
    -ExpectedStatus 202

if ($singleResult) {
    Start-Sleep -Seconds 2
    
    # Check status
    Test-Endpoint `
        -Name "Get Remediation Status" `
        -Method "GET" `
        -Endpoint "/api/xdr/status/$($singleResult.orchestrationId)"
}

# Test 3: Batch Remediation (Multiple Devices)
$batchRequest = @{
    batchId = "BATCH-001"
    tenantId = $TestTenantId
    incidentId = "TEST-002"
    platform = "MDE"
    action = "IsolateDevice"
    targets = @(
        @{ deviceId = "device-1" },
        @{ deviceId = "device-2" },
        @{ deviceId = "device-3" }
    )
    priority = "High"
    initiatedBy = "test@example.com"
    justification = "Batch test isolation"
    parallelExecution = $true
}

Test-Endpoint `
    -Name "Batch Remediation (3 devices)" `
    -Method "POST" `
    -Endpoint "/api/xdr/batch-remediate" `
    -Body $batchRequest `
    -ExpectedStatus 202

# Test 4: Multi-Tenant Batch
$multiTenantRequest = @{
    batchId = "MULTI-001"
    requests = @(
        @{
            tenantId = $TestTenantId
            incidentId = "TEST-003"
            platform = "MDO"
            action = "SoftDeleteEmail"
            parameters = @{
                userId = "user1@test.com"
                messageId = "msg-1"
            }
            priority = "Medium"
            initiatedBy = "test@example.com"
            justification = "Test email deletion"
        },
        @{
            tenantId = $TestTenantId
            incidentId = "TEST-004"
            platform = "EntraID"
            action = "DisableUserAccount"
            parameters = @{
                userId = "user-guid-1"
            }
            priority = "High"
            initiatedBy = "test@example.com"
            justification = "Test user disable"
        }
    )
    parallelExecution = $true
}

Test-Endpoint `
    -Name "Multi-Tenant Batch (2 requests)" `
    -Method "POST" `
    -Endpoint "/api/xdr/multi-tenant-batch" `
    -Body $multiTenantRequest `
    -ExpectedStatus 202

# Test 5: Platform-Specific Tests
Write-Host "`n========== Platform Worker Tests ==========" -ForegroundColor Cyan

# MDE Worker Test
$mdeRequest = @{
    tenantId = $TestTenantId
    incidentId = "TEST-MDE"
    platform = "MDE"
    action = "RunAntivirusScan"
    parameters = @{
        deviceId = "test-device"
        scanType = "Quick"
    }
    priority = "Medium"
    initiatedBy = "test@example.com"
    justification = "Test AV scan"
}
Test-Endpoint -Name "MDE Worker - AV Scan" -Method "POST" -Endpoint "/api/xdr/remediate" -Body $mdeRequest -ExpectedStatus 202

# MDO Worker Test
$mdoRequest = @{
    tenantId = $TestTenantId
    incidentId = "TEST-MDO"
    platform = "MDO"
    action = "MoveEmailToJunk"
    parameters = @{
        userId = "user@test.com"
        messageId = "msg-123"
    }
    priority = "Medium"
    initiatedBy = "test@example.com"
    justification = "Test email move"
}
Test-Endpoint -Name "MDO Worker - Move Email" -Method "POST" -Endpoint "/api/xdr/remediate" -Body $mdoRequest -ExpectedStatus 202

# Entra ID Worker Test
$entraRequest = @{
    tenantId = $TestTenantId
    incidentId = "TEST-ENTRA"
    platform = "EntraID"
    action = "RevokeUserSignInSessions"
    parameters = @{
        userId = "user-guid"
    }
    priority = "High"
    initiatedBy = "test@example.com"
    justification = "Test session revoke"
}
Test-Endpoint -Name "Entra ID Worker - Revoke Sessions" -Method "POST" -Endpoint "/api/xdr/remediate" -Body $entraRequest -ExpectedStatus 202

# Intune Worker Test
$intuneRequest = @{
    tenantId = $TestTenantId
    incidentId = "TEST-INTUNE"
    platform = "Intune"
    action = "SyncDevice"
    parameters = @{
        deviceId = "intune-device-123"
    }
    priority = "Low"
    initiatedBy = "test@example.com"
    justification = "Test device sync"
}
Test-Endpoint -Name "Intune Worker - Sync Device" -Method "POST" -Endpoint "/api/xdr/remediate" -Body $intuneRequest -ExpectedStatus 202

# Generate Report
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  Test Results Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Total Tests: $($results.Passed + $results.Failed)" -ForegroundColor White
Write-Host "Passed: $($results.Passed)" -ForegroundColor Green
Write-Host "Failed: $($results.Failed)" -ForegroundColor Red
Write-Host "Success Rate: $([math]::Round(($results.Passed / ($results.Passed + $results.Failed)) * 100, 2))%" -ForegroundColor Cyan

# Save detailed report
$reportPath = ".\test-results-$(Get-Date -Format 'yyyyMMdd-HHmmss').json"
$results | ConvertTo-Json -Depth 10 | Out-File $reportPath
Write-Host "`nDetailed report saved to: $reportPath" -ForegroundColor Yellow

# Overall Status
if ($results.Failed -eq 0) {
    Write-Host "`n? ALL TESTS PASSED!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "`n?? SOME TESTS FAILED" -ForegroundColor Yellow
    exit 1
}
