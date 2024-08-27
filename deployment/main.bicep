param location string = resourceGroup().location
@secure()
param sendGridApiKey string
param emailSenderAddress string

var naming = {
  appInsights: ''
  durableFunctionsTaskHubContainer: 'esigning'
  frontendAppServicePlan: ''
  frontendAppService: ''
  functionsAppServicePlan: ''
  functionsStorageAccount: ''
  functionsApp: ''
  logAnalytics: ''
  sqlServer: ''
  sqlDb: ''
}

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: naming.logAnalytics
  location: location
  properties: {
    retentionInDays: 30
    sku: {
      name: 'PerGB2018'
    }
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: naming.appInsights
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    Flow_Type: 'Bluefield'
    Request_Source: 'rest'
    WorkspaceResourceId: logAnalytics.id
  }
}

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
          value: 'DefaultEndpointsProtocol=https;AccountName=${functionsStorageAccount.name};AccountKey=${functionsStorageAccount.listKeys().keys[0].value};EndpointSuffix=core.windows.net'
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsights.properties.ConnectionString
        }
        {
          name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
          value: 'DefaultEndpointsProtocol=https;AccountName=${functionsStorageAccount.name};AccountKey=${functionsStorageAccount.listKeys().keys[0].value};EndpointSuffix=core.windows.net'
        }
        {
          name: 'WEBSITE_CONTENTSHARE'
          value: toLower(naming.functionsApp)
        }
        {
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: '1'
        }
        {
          name: 'ConnectionStrings__Sql'
          value: 'Data Source=${naming.sqlServer}${environment().suffixes.sqlServerHostname}; Authentication=Active Directory Managed Identity; Encrypt=True; Database=${naming.sqlDb}'
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
          name: 'Storage__ConnectionString'
          value: 'DefaultEndpointsProtocol=https;AccountName=${functionsStorageAccount.name};AccountKey=${functionsStorageAccount.listKeys().keys[0].value};EndpointSuffix=core.windows.net'
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
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsights.properties.ConnectionString
        }
        {
          name: 'ConnectionStrings__Sql'
          value: 'TODO'
        }
        {
          name: 'Workflow__StartUrl'
          value: 'https://${naming.functionsApp}.azurewebsites.net/api/TODO?code=TODO'
        }
        {
          name: 'Workflow__AddSignEventUrl'
          value: 'https://${naming.functionsApp}.azurewebsites.net/api/TODO?code=TODO'
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
