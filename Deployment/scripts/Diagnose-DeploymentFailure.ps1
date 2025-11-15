# Diagnose Azure Policy Deployment Failure
# Run this script to find out WHY deployment is failing

param(
    [Parameter(Mandatory=$false)]
    [string]$ResourceGroupName = "alex-testing-rg"
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "SentryXDR Deployment Failure Diagnosis" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Get Azure Context
$context = Get-AzContext
if (!$context) {
    Write-Host "ERROR: Not logged into Azure. Run: Connect-AzAccount" -ForegroundColor Red
    exit 1
}

Write-Host "Subscription: $($context.Subscription.Name)" -ForegroundColor Green
Write-Host "Subscription ID: $($context.Subscription.Id)" -ForegroundColor Green
Write-Host "Resource Group: $ResourceGroupName`n" -ForegroundColor Green

# Step 1: Get Latest Failed Deployment
Write-Host "[Step 1] Getting latest failed deployment..." -ForegroundColor Yellow
$deployment = Get-AzResourceGroupDeployment -ResourceGroupName $ResourceGroupName -ErrorAction SilentlyContinue |
              Where-Object {$_.ProvisioningState -eq 'Failed'} |
              Sort-Object Timestamp -Descending |
              Select-Object -First 1

if (!$deployment) {
    Write-Host "No failed deployments found. Try deploying first!" -ForegroundColor Red
    exit 1
}

Write-Host "Failed Deployment: $($deployment.DeploymentName)" -ForegroundColor Red
Write-Host "Timestamp: $($deployment.Timestamp)" -ForegroundColor Gray
Write-Host "Duration: $($deployment.Duration)" -ForegroundColor Gray

# Step 2: Get Detailed Error
Write-Host "`n[Step 2] Analyzing deployment errors..." -ForegroundColor Yellow

$operations = Get-AzResourceGroupDeploymentOperation -ResourceGroupName $ResourceGroupName -DeploymentName $deployment.DeploymentName

$failedOps = $operations | Where-Object {$_.ProvisioningState -eq 'Failed'}

Write-Host "Found $($failedOps.Count) failed operations`n" -ForegroundColor Red

foreach ($op in $failedOps) {
    Write-Host "----------------------------------------" -ForegroundColor DarkGray
    Write-Host "Resource: $($op.Properties.TargetResource.ResourceName)" -ForegroundColor Yellow
    Write-Host "Type: $($op.Properties.TargetResource.ResourceType)" -ForegroundColor Gray
    
    $error = $op.Properties.StatusMessage.error
    if ($error) {
        Write-Host "Error Code: $($error.code)" -ForegroundColor Red
        Write-Host "Error Message: $($error.message)" -ForegroundColor Red
        
        # Check if it's a policy error
        if ($error.code -eq "RequestDisallowedByPolicy") {
            Write-Host "`nPOLICY VIOLATION DETECTED!" -ForegroundColor Red -BackgroundColor Yellow
            
            # Try to get policy details
            if ($error.details) {
                foreach ($detail in $error.details) {
                    Write-Host "  Policy: $($detail.code)" -ForegroundColor Magenta
                    Write-Host "  Message: $($detail.message)" -ForegroundColor White
                }
            }
            
            # Get policy assignment details
            if ($error.additionalInfo) {
                foreach ($info in $error.additionalInfo) {
                    Write-Host "  Additional Info: $($info.type)" -ForegroundColor Cyan
                    if ($info.info) {
                        $info.info | ConvertTo-Json -Depth 5 | Write-Host -ForegroundColor White
                    }
                }
            }
        }
    }
    Write-Host ""
}

# Step 3: Check Azure Policies
Write-Host "`n[Step 3] Checking Azure Policy Assignments..." -ForegroundColor Yellow

$subscriptionScope = "/subscriptions/$($context.Subscription.Id)"
$rgScope = "/subscriptions/$($context.Subscription.Id)/resourceGroups/$ResourceGroupName"

Write-Host "Subscription-level policies:" -ForegroundColor Cyan
$subPolicies = Get-AzPolicyAssignment -Scope $subscriptionScope -ErrorAction SilentlyContinue
if ($subPolicies) {
    $subPolicies | Select-Object Name, DisplayName, @{Name='Enforcement';Expression={$_.Properties.EnforcementMode}} | Format-Table -AutoSize
} else {
    Write-Host "  None found" -ForegroundColor Gray
}

Write-Host "Resource Group-level policies:" -ForegroundColor Cyan
$rgPolicies = Get-AzPolicyAssignment -Scope $rgScope -ErrorAction SilentlyContinue
if ($rgPolicies) {
    $rgPolicies | Select-Object Name, DisplayName, @{Name='Enforcement';Expression={$_.Properties.EnforcementMode}} | Format-Table -AutoSize
} else {
    Write-Host "  None found" -ForegroundColor Gray
}

# Step 4: Check Resource Group Location
Write-Host "`n[Step 4] Checking Resource Group configuration..." -ForegroundColor Yellow
$rg = Get-AzResourceGroup -Name $ResourceGroupName -ErrorAction SilentlyContinue
if ($rg) {
    Write-Host "Location: $($rg.Location)" -ForegroundColor Green
    Write-Host "Tags:" -ForegroundColor Green
    if ($rg.Tags) {
        $rg.Tags | Format-Table -AutoSize
    } else {
        Write-Host "  No tags" -ForegroundColor Gray
    }
}

# Step 5: Recommendations
Write-Host "`n[Step 5] RECOMMENDATIONS:" -ForegroundColor Green -BackgroundColor Black
Write-Host "========================================`n" -ForegroundColor Green

Write-Host "Common fixes for 'RequestDisallowedByPolicy':" -ForegroundColor Yellow
Write-Host ""
Write-Host "1. CHECK TAGS:" -ForegroundColor Cyan
Write-Host "   - ARM template has 13 tags, but policy might require different tag names/values"
Write-Host "   - Solution: Find required tags in policy details above"
Write-Host ""
Write-Host "2. CHECK PREMIUM SKU:" -ForegroundColor Cyan
Write-Host "   - EP1 (Premium) might not be allowed in your subscription"
Write-Host "   - Solution: Try Function App SKU = 'Y1' (Consumption) instead"
Write-Host ""
Write-Host "3. CHECK AUTOMATION ACCOUNT:" -ForegroundColor Cyan
Write-Host "   - Set 'Deploy Hybrid Worker' = FALSE"
Write-Host "   - Automation Accounts might be blocked by policy"
Write-Host ""
Write-Host "4. CHECK LOCATION:" -ForegroundColor Cyan
Write-Host "   - Your RG is in: $($rg.Location)"
Write-Host "   - Policy might restrict to specific regions"
Write-Host ""
Write-Host "5. CONTACT AZURE ADMIN:" -ForegroundColor Cyan
Write-Host "   - Ask them to exempt this RG from restrictive policies"
Write-Host "   - Or get list of allowed resource types/SKUs/locations"
Write-Host ""

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Diagnosis Complete!" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# Export detailed error to file
$errorFile = "deployment-error-$(Get-Date -Format 'yyyyMMdd-HHmmss').json"
$deployment | ConvertTo-Json -Depth 10 | Out-File $errorFile
Write-Host "`nDetailed error saved to: $errorFile" -ForegroundColor Gray
