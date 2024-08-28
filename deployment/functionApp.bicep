param location string = resourceGroup().location
@secure()
param sendGridApiKey string
param emailSenderAddress string
param developerUserId string
param developerUsername string
param developerIpAddress string

var appName = 'durablesigning'
var namingSuffix = uniqueString(resourceGroup().id)
var naming = {
  appInsights: 'ai-${appName}-${namingSuffix}'
  durableSigningContainer: 'esigning'
  frontendAppService: 'fe-${appName}-${namingSuffix}'
  functionAppServicePlan: 'func-plan-${appName}-${namingSuffix}'
  functionApp: 'func-${appName}-${namingSuffix}'
  logAnalytics: 'la-${appName}-${namingSuffix}'
  sqlServer: 'sql-${appName}-${namingSuffix}'
  sqlDb: 'DurableSigningDb'
  storageAccount: 'stodursig${namingSuffix}'
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

resource functionAppServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: naming.functionAppServicePlan
  location: location
  sku: {
    name: 'Y1'
    capacity: 1
  }
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2021-04-01' = {
  name: naming.storageAccount
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

    resource durableSigningContainer 'containers' = {
      name: naming.durableSigningContainer
      properties: {
        publicAccess: 'None'
      }
    }
  }
}

resource functionApp 'Microsoft.Web/sites@2023-12-01' = {
  name: naming.functionApp
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  kind: 'functionapp'
  properties: {
    serverFarmId: functionAppServicePlan.id
    httpsOnly: true
    clientAffinityEnabled: false
    siteConfig: {
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=core.windows.net'
        }
        {
          name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=core.windows.net'
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
          name: 'WEBSITE_CONTENTSHARE'
          value: toLower(replace(naming.functionApp, '-', ''))
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
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=core.windows.net'
        }
        {
          name: 'Storage__ContainerName'
          value: naming.durableSigningContainer
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

resource sqlServer 'Microsoft.Sql/servers@2021-11-01' = {
  name: naming.sqlServer
  location: location
  properties: {
    minimalTlsVersion: '1.2'
    administrators: {
      administratorType: 'ActiveDirectory'
      azureADOnlyAuthentication: true
      principalType: 'User'
      login: developerUsername
      sid: developerUserId
      tenantId: tenant().tenantId
    }
  }

  resource azureFirewallRule 'firewallRules@2022-05-01-preview' = {
    name: 'Allow Azure'
    properties: {
      startIpAddress: '0.0.0.0'
      endIpAddress: '0.0.0.0'
    }
  }

  resource devFirewallRule 'firewallRules@2022-05-01-preview' = {
    name: 'Allow developer'
    properties: {
      startIpAddress: developerIpAddress
      endIpAddress: developerIpAddress
    }
  }
}

resource sqlDb 'Microsoft.Sql/servers/databases@2021-11-01' = {
  parent: sqlServer
  name: naming.sqlDb
  location: location
  properties: {
    requestedBackupStorageRedundancy: 'Local'
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: 2147483648 // 2 GB
  }
  sku: {
    name: 'Basic'
    capacity: 5
  }
}

output appInsightsName string = appInsights.name
output storageAccountName string = storageAccount.name
output durableSigningContainerName string = naming.durableSigningContainer
output functionAppName string = functionApp.name
output sqlServerName string = sqlServer.name
output sqlDbName string = sqlDb.name
