# Deploy SentryXDR to Azure
# This script deploys the entire SentryXDR infrastructure

param(
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroupName,
    
    [Parameter(Mandatory=$true)]
    [string]$Location,
    
    [Parameter(Mandatory=$true)]
    [string]$MultiTenantClientId,
    
    [Parameter(Mandatory=$true)]
    [string]$MultiTenantClientSecret,
    
    [Parameter(Mandatory=$false)]
    [string]$FunctionAppName = "sentryxdr-$((Get-Random -Maximum 9999))"
)

Write-Host "Starting SentryXDR deployment..." -ForegroundColor Green

# Login to Azure (if not already logged in)
$context = Get-AzContext
if (!$context) {
    Write-Host "Please login to Azure..." -ForegroundColor Yellow
    Connect-AzAccount
}

# Create Resource Group if it doesn't exist
Write-Host "Creating Resource Group: $ResourceGroupName" -ForegroundColor Cyan
$rg = Get-AzResourceGroup -Name $ResourceGroupName -ErrorAction SilentlyContinue
if (!$rg) {
    New-AzResourceGroup -Name $ResourceGroupName -Location $Location
    Write-Host "Resource Group created successfully" -ForegroundColor Green
} else {
    Write-Host "Resource Group already exists" -ForegroundColor Yellow
}

# Deploy ARM Template
Write-Host "Deploying ARM template..." -ForegroundColor Cyan
$deploymentParams = @{
    ResourceGroupName = $ResourceGroupName
    TemplateFile = ".\Deployment\azuredeploy.json"
    functionAppName = $FunctionAppName
    location = $Location
    multiTenantClientId = $MultiTenantClientId
    multiTenantClientSecret = (ConvertTo-SecureString $MultiTenantClientSecret -AsPlainText -Force)
}

$deployment = New-AzResourceGroupDeployment @deploymentParams

if ($deployment.ProvisioningState -eq "Succeeded") {
    Write-Host "ARM deployment completed successfully!" -ForegroundColor Green
    
    # Output deployment details
    Write-Host "`nDeployment Details:" -ForegroundColor Cyan
    Write-Host "Function App Name: $($deployment.Outputs.functionAppName.Value)" -ForegroundColor White
    Write-Host "Function App URL: $($deployment.Outputs.functionAppUrl.Value)" -ForegroundColor White
    Write-Host "Storage Account: $($deployment.Outputs.storageAccountName.Value)" -ForegroundColor White
    Write-Host "Application Insights: $($deployment.Outputs.applicationInsightsName.Value)" -ForegroundColor White
    Write-Host "Managed Identity Principal ID: $($deployment.Outputs.managedIdentityPrincipalId.Value)" -ForegroundColor White
    
    # Build and publish the Function App
    Write-Host "`nBuilding Function App..." -ForegroundColor Cyan
    dotnet build --configuration Release
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Build successful!" -ForegroundColor Green
        
        Write-Host "Publishing Function App..." -ForegroundColor Cyan
        dotnet publish --configuration Release --output ".\publish"
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Creating deployment package..." -ForegroundColor Cyan
            Compress-Archive -Path ".\publish\*" -DestinationPath ".\deploy.zip" -Force
            
            Write-Host "Deploying to Azure..." -ForegroundColor Cyan
            Publish-AzWebApp -ResourceGroupName $ResourceGroupName `
                -Name $deployment.Outputs.functionAppName.Value `
                -ArchivePath ".\deploy.zip" `
                -Force
            
            Write-Host "`nDeployment completed successfully!" -ForegroundColor Green
            Write-Host "`nAPI Endpoint: $($deployment.Outputs.functionAppUrl.Value)/api/xdr/remediate" -ForegroundColor Cyan
            Write-Host "Health Check: $($deployment.Outputs.functionAppUrl.Value)/api/xdr/health" -ForegroundColor Cyan
            
            # Clean up
            Remove-Item ".\deploy.zip" -Force
            Remove-Item ".\publish" -Recurse -Force
        }
    }
} else {
    Write-Host "Deployment failed: $($deployment.ProvisioningState)" -ForegroundColor Red
}
