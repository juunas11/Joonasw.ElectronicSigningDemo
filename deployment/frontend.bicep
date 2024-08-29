param location string = resourceGroup().location
param appInsightsName string
param storageAccountName string
param durableSigningContainerName string
param functionAppName string
@secure()
param functionAppKey string
param sqlServerName string
param sqlDbName string

var appName = 'durablesigning'
var namingSuffix = uniqueString(resourceGroup().id)
var naming = {
  frontendAppServicePlan: 'fe-plan-${appName}-${namingSuffix}'
  frontendAppService: 'fe-${appName}-${namingSuffix}'
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: appInsightsName
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageAccountName
}

resource frontendAppServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: naming.frontendAppServicePlan
  location: location
  sku: {
    name: 'B1'
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
          value: 'Data Source=${sqlServerName}${environment().suffixes.sqlServerHostname}; Authentication=Active Directory Managed Identity; Encrypt=True; Database=${sqlDbName}'
        }
        {
          name: 'Workflow__StartUrl'
          value: 'https://${functionAppName}.azurewebsites.net/api/StartSigningWorkflow?code=${functionAppKey}'
        }
        {
          name: 'Workflow__AddSignEventUrl'
          value: 'https://${functionAppName}.azurewebsites.net/api/AddSignEvent?code=${functionAppKey}'
        }
        {
          name: 'Storage__ConnectionString'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=core.windows.net'
        }
        {
          name: 'Storage__ContainerName'
          value: durableSigningContainerName
        }
      ]
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      netFrameworkVersion: 'v8.0'
      scmMinTlsVersion: '1.2'
    }
  }
}

output frontendAppServiceName string = frontendAppService.name
