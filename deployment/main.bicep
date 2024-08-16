param location string = resourceGroup().location
@secure()
param sendGridApiKey string
param emailSenderAddress string

var naming = {
  durableFunctionsTaskHubContainer: 'esigning'
  frontendAppServicePlan: ''
  frontendAppService: ''
  functionsAppServicePlan: ''
  functionsStorageAccount: ''
  functionsApp: ''
  sqlServer: ''
  sqlDb: ''
}

// TODO: Log Analytics workspace
// TODO: App Insights

resource functionsAppServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: naming.functionsAppServicePlan
  location: location
  sku: {
    name: 'Y1'
    capacity: 1
  }
}

resource functionsStorageAccount 'Microsoft.Storage/storageAccounts@2021-04-01' = {
  name: naming.functionsStorageAccount
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    accessTier: 'Hot'
    allowBlobPublicAccess: false
    allowSharedKeyAccess: true
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
  }

  resource blobService 'blobServices' = {
    name: 'default'
    properties: {}

    resource durableFunctionsTaskHubContainer 'containers' = {
      name: naming.durableFunctionsTaskHubContainer
      properties: {
        publicAccess: 'None'
      }
    }
  }
}

resource functionsApp 'Microsoft.Web/sites@2023-12-01' = {
  name: naming.functionsApp
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  kind: 'functionapp'
  properties: {
    serverFarmId: functionsAppServicePlan.id
    httpsOnly: true
    clientAffinityEnabled: false
    siteConfig: {
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: 'TODO'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: 'TODO'
        }
        {
          name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
          value: 'TODO'
        }
        {
          name: 'WEBSITE_CONTENTSHARE'
          value: 'TODO'
        }
        {
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: '1'
        }
        {
          name: 'ConnectionStrings__Sql'
          value: 'TODO'
        }
        {
          name: 'AppBaseUrl'
          value: 'https://${naming.frontendAppService}.azurewebsites.net'
        }
        {
          name: 'SendGridKey'
          value: sendGridApiKey
        }
        {
          name: 'FromEmail'
          value: emailSenderAddress
        }
        {
          name: 'Storage__ContainerName'
          value: naming.durableFunctionsTaskHubContainer
        }
      ]
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      netFrameworkVersion: 'v8.0'
      scmMinTlsVersion: '1.2'
      cors: {
        allowedOrigins: [
          'https://portal.azure.com'
        ]
      }
    }
  }
}

resource frontendAppServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: naming.frontendAppServicePlan
  location: location
  sku: {
    name: 'TODO'
    capacity: 1
  }
}

resource frontendAppService 'Microsoft.Web/sites@2023-12-01' = {
  name: naming.frontendAppService
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: frontendAppServicePlan.id
    httpsOnly: true
    clientAffinityEnabled: false
    siteConfig: {
      alwaysOn: true
      appSettings: [
        {
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: '1'
        }
        {
          name: 'ConnectionStrings__Sql'
          value: 'TODO'
        }
        {
          name: 'Workflow__StartUrl'
          value: 'TODO'
        }
        {
          name: 'Workflow__AddSignEventUrl'
          value: 'TODO'
        }
        {
          name: 'Storage__ContainerName'
          value: naming.durableFunctionsTaskHubContainer
        }
      ]
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      netFrameworkVersion: 'v8.0'
      scmMinTlsVersion: '1.2'
    }
  }
}
