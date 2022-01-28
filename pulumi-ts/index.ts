import * as az from "@pulumi/azure-native"

const imageInDockerHub = "appsvcsample/python-helloworld:latest"
const resourceGroup = new az.resources.ResourceGroup("pulik-rg")

const plan = new az.web.AppServicePlan("pulik-plan", {
    resourceGroupName: resourceGroup.name,
    kind: "Linux",
    reserved: true,
    sku: {
        name: "B1",
        tier: "Basic",
    },
})

const userAssignedIdentity = new az.managedidentity.UserAssignedIdentity("pulik-identity", {
    location: "westeurope",
    resourceGroupName: resourceGroup.name,
    resourceName: "pulik-identity"
})

const vault = new az.keyvault.Vault("vault", {
    location: "westeurope",
    properties: {
        accessPolicies: [{
            objectId: userAssignedIdentity.principalId,
            permissions: {
                secrets: [
                    "get",
                    "list",
                    "set",
                    "backup",
                    "restore",
                    "recover"
                ],
            },
            tenantId: userAssignedIdentity.tenantId,
        }],
        enabledForDeployment: true,
        sku: {
            family: "A",
            name: "standard",
        },
        tenantId: userAssignedIdentity.tenantId,
    },
    resourceGroupName: resourceGroup.name,
    vaultName: "pulik-vault",
})

const secret = new az.keyvault.Secret("pulik-secret", {
    properties: {
        value: "secret-value",
    },
    resourceGroupName: resourceGroup.name,
    secretName: "pulik-secret-name",
    vaultName: vault.name,
});

const pulikAi = new az.insights.Component("pulik-ai", {
    resourceGroupName: resourceGroup.name,
    kind: "web",
    applicationType: az.insights.ApplicationType.Web,
});

const pulikApp = new az.web.WebApp("pulik-app", {
    resourceGroupName: resourceGroup.name,
    serverFarmId: plan.id,
    siteConfig: {
        appSettings: [{
            name: "WEBSITES_ENABLE_APP_SERVICE_STORAGE",
            value: "false",
        }],
        alwaysOn: true,
        linuxFxVersion: `DOCKER|${imageInDockerHub}`,
    },
    httpsOnly: true,
})

const server = new az.dbforpostgresql.Server("pulik-server", {
    location: "westeurope",
    properties: {
        administratorLogin: "pulik-admin",
        administratorLoginPassword: "SztrongPa$$sword",
        createMode: "Default",
        minimalTlsVersion: "TLS1_2",
        sslEnforcement: "Enabled",
        storageProfile: {
            backupRetentionDays: 7,
            geoRedundantBackup: "Disabled",
            storageMB: 128000,
        },
    },
    resourceGroupName: resourceGroup.name,
    serverName: "pulik-server",
    sku: {
        capacity: 2,
        family: "Gen5",
        name: "B_Gen5_2",
        tier: "Basic",
    }
});

const database = new az.dbforpostgresql.Database("pulik-database", {
    charset: "UTF8",
    collation: "English_United States.1252",
    databaseName: "pulik-db",
    resourceGroupName: resourceGroup.name,
    serverName: server.name,
})