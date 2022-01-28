import * as az from "@pulumi/azure-native"

const imageInDockerHub = "appsvcsample/python-helloworld:latest"
const resourceGroup = new az.resources.ResourceGroup("appservice-docker-rg")

const plan = new az.web.AppServicePlan("pulik-plan", {
    resourceGroupName: resourceGroup.name,
    kind: "Linux",
    reserved: true,
    sku: {
        name: "B1",
        tier: "Basic",
    },
})

const vault = new az.keyvault.Vault("vault", {
    location: "westeurope",
    properties: {
        accessPolicies: [{
            objectId: "00000000-0000-0000-0000-000000000000",
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
            tenantId: "00000000-0000-0000-0000-000000000000",
        }],
        enabledForDeployment: true,
        enabledForDiskEncryption: true,
        enabledForTemplateDeployment: true,
        sku: {
            family: "A",
            name: "standard",
        },
        tenantId: "00000000-0000-0000-0000-000000000000",
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
        createMode: "PointInTimeRestore",
        restorePointInTime: "2017-12-14T00:00:37.467Z",
        sourceServerId: "/subscriptions/ffffffff-ffff-ffff-ffff-ffffffffffff/resourceGroups/SourceResourceGroup/providers/Microsoft.DBforPostgreSQL/servers/sourceserver",
    },
    resourceGroupName: resourceGroup.name,
    serverName: "pulik-server",
    sku: {
        capacity: 2,
        family: "Gen5",
        name: "B_Gen5_2",
        tier: "Basic",
    }
})

const database = new az.dbforpostgresql.Database("pulik-database", {
    charset: "UTF8",
    collation: "English_United States.1252",
    databaseName: "pulik-db",
    resourceGroupName: resourceGroup.name,
    serverName: server.name,
})

const userAssignedIdentity = new az.managedidentity.UserAssignedIdentity("pulik-identity", {
    location: "westeurope",
    resourceGroupName: resourceGroup.name,
    resourceName: "pulik-identity"
})