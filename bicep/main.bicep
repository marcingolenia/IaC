var location= 'northeurope'
var pgAdminPassword='pgAdminP@ssword4321'

var imageInDockerHub = 'microsoft/azure-appservices-go-quickstart'

resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2018-11-30' = {
  name: 'BicepIaCPocUMI'
  location: location
}

resource keyVault 'Microsoft.KeyVault/vaults@2019-09-01' = {
  name: 'BicepIaCPocKV'
  location: location
  properties: {
    enabledForDeployment: true
    enabledForTemplateDeployment: true
    enabledForDiskEncryption: true
    tenantId: managedIdentity.properties.tenantId
    accessPolicies: [
      {
        tenantId: managedIdentity.properties.tenantId
        objectId: managedIdentity.properties.principalId
        permissions: {
          secrets: [
            'list'
            'get'
          ]
        }
      }
    ]
    sku: {
      name: 'standard'
      family: 'A'
    }
  }
}

resource postgreSQL 'Microsoft.DBForPostgreSQL/servers@2017-12-01' = {
  name: 'BicepIaCPocpgdb'
  location: location
  properties: {
      administratorLogin: 'pulumiPgAdmin'
      administratorLoginPassword: pgAdminPassword
      createMode: 'Default'
      sslEnforcement: 'Enabled'
      minimalTlsVersion: 'TLS1_2'
      storageProfile: {
        backupRetentionDays: 7
        geoRedundantBackup: 'Disabled'
        storageMB: 5120
      }
  }
  sku: {
      name: 'B_Gen5_2'
      tier: 'Basic' 
      family: 'Gen5'
      capacity: 2
  }
}

resource keyVaultSecret 'Microsoft.KeyVault/vaults/secrets@2019-09-01' = {
  name: 'BicepIaCPocKV/pgAdminPassword'
  properties: {
    value: 'pgAdminPassword'
  }
}

resource appInsightsComponents 'Microsoft.Insights/components@2020-02-02-preview' = {
  name: 'BicepIaCPocAI'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    Flow_Type: 'Bluefield'
    Request_Source: 'rest'
  }
}

resource appServicePlan 'Microsoft.Web/serverfarms@2020-12-01' = {
  name: 'BicepIaCPocAppPlan2'
  location: location
  kind: 'linux'
  properties: {
    reserved: true
  }
  sku: {
    name: 'B1'
    tier: 'Basic'
  }
}

resource webApplication 'Microsoft.Web/sites@2018-11-01' = {
  name: 'BicepIaCPocWebApp'
  location: location
  kind: 'app,linux,container'
  tags: {
    'hidden-related:${resourceGroup().id}/providers/Microsoft.Web/serverfarms/appServicePlan': 'Resource'
  }
  properties: {
    serverFarmId: appServicePlan.id
    siteConfig: {
      alwaysOn:true
      appSettings:[
        {
          name: 'DOCKER_ENABLE_CI'
          value: 'true'
        }
        {
          name: 'DOCKER_CUSTOM_IMAGE_NAME'
          value: imageInDockerHub
        }    
      ]
      linuxFxVersion: 'DOCKER|${imageInDockerHub}'
    }
    httpsOnly: true
  }
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentity.id}': {}
    }
  }

}
