$ErrorActionPreference = 'Stop'

$configPath = Join-Path $PSScriptRoot config.json
$config = Get-Content $configPath -Raw | ConvertFrom-Json

$tenantId = $config.tenantId
$subscriptionId = $config.subscriptionId

# Check subscription is available
az account show -s "$subscriptionId" | Out-Null
if ($LASTEXITCODE -ne 0) {
    az login -t "$tenantId"
}

$deploymentNamePrefix = Get-Date -Format "yyyy-MM-dd-HH-mm-ss"

Write-Host "Starting deployment..."

$resourceGroup = $config.resourceGroup
$location = $config.location

$rgExists = az group exists --subscription "$subscriptionId" -g "$resourceGroup"
if ($LASTEXITCODE -ne 0) {
    throw "Failed to check if resource group exists."
}

if ($rgExists -eq "false") {
    Write-Host "Resource group does not exist. Creating resource group..."
    az group create --subscription "$subscriptionId" -g "$resourceGroup" -l "$location"
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to create resource group."
    }
}

Push-Location $PSScriptRoot

Write-Host "Running functionApp.bicep deployment..."
$sendGridApiKey = $config.sendGridApiKey
$emailSenderAddress = $config.emailSenderAddress
$developerUserId = $config.developerUserId
$developerUsername = $config.developerUsername
$developerIpAddress = $config.developerIpAddress

$functionAppBicepResult = az deployment group create --subscription "$subscriptionId" -g "$resourceGroup" -f "functionApp.bicep" -n "$deploymentNamePrefix-FunctionApp" --mode "Incremental" `
    -p sendGridApiKey=$sendGridApiKey emailSenderAddress=$emailSenderAddress `
    -p developerUserId=$developerUserId developerUsername=$developerUsername `
    -p developerIpAddress=$developerIpAddress | ConvertFrom-Json
if ($LASTEXITCODE -ne 0) {
    Pop-Location
    throw "Failed to deploy functionApp.bicep."
}

$functionAppBicepOutputs = $functionAppBicepResult.properties.outputs
$appInsightsName = $functionAppBicepOutputs.appInsightsName.value
$storageAccountName = $functionAppBicepOutputs.storageAccountName.value
$durableSigningContainerName = $functionAppBicepOutputs.durableSigningContainerName.value
$functionAppName = $functionAppBicepOutputs.functionAppName.value
$sqlServerName = $functionAppBicepOutputs.sqlServerName.value
$sqlDbName = $functionAppBicepOutputs.sqlDbName.value

Pop-Location

Push-Location (Join-Path $PSScriptRoot ..\Joonasw.ElectronicSigningDemo.Workflows -Resolve)

Write-Host "Creating Function App deployment package..."
dotnet publish --configuration Release --output publish_output
if ($LASTEXITCODE -ne 0) {
    throw "Failed to create Function App deployment package."
}

Compress-Archive -Path .\publish_output\* -DestinationPath .\FunctionAppPublish.zip -CompressionLevel Fastest -Force

Write-Host "Deploying Function App..."
az functionapp deployment source config-zip --subscription "$subscriptionId" -g "$resourceGroup" -n "$functionAppName" --src .\FunctionAppPublish.zip | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw "Failed to deploy Function App."
}

Remove-Item -R .\publish_output
Remove-Item .\FunctionAppPublish.zip

Pop-Location

$functionAppKey = az functionapp keys list --subscription "$subscriptionId" -g "$resourceGroup" -n "$functionAppName" --query "masterKey" -o tsv
if ($LASTEXITCODE -ne 0) {
    throw "Failed to get Function App key."
}

Push-Location $PSScriptRoot

Write-Host "Running frontend.bicep deployment..."
$frontendBicepResult = az deployment group create --subscription "$subscriptionId" -g "$resourceGroup" -f "frontend.bicep" -n "$deploymentNamePrefix-Frontend" --mode "Incremental" `
    -p appInsightsName=$appInsightsName storageAccountName=$storageAccountName `
    -p durableSigningContainerName=$durableSigningContainerName `
    -p functionAppName=$functionAppName functionAppKey=$functionAppKey `
    -p sqlServerName=$sqlServerName sqlDbName=$sqlDbName | ConvertFrom-Json
if ($LASTEXITCODE -ne 0) {
    Pop-Location
    throw "Failed to deploy frontend.bicep."
}

$frontendBicepOutputs = $frontendBicepResult.properties.outputs