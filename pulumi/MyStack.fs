module pulumi_iac.MyStack

open System.Collections.Generic
open Pulumi
open Pulumi.AzureNative

[<Literal>]
let ResourceGroupName = "PulumiIaCPoc"

[<Literal>]
let Location = "northeurope"

let createResourceGroup () =
    let resourceGroupArgs =
        Resources.ResourceGroupArgs(Location = Location, ResourceGroupName = ResourceGroupName)

    Resources.ResourceGroup(ResourceGroupName, resourceGroupArgs)

let createUserAssignedIdentity (resourceGroupName: Output<string>) =
    let name = "PulumiIaCPocUMI"

    let userAssignedIdentityArgs =
        ManagedIdentity.UserAssignedIdentityArgs(Location = Location, ResourceName = name, ResourceGroupName = resourceGroupName)

    ManagedIdentity.UserAssignedIdentity(name, userAssignedIdentityArgs)

let createKeyVault (managedIdentityObjectId: Output<string>) (tenantId: Output<string>) (resourceGroupName: Output<string>) =
    let name = "PulumiIaCPocKV"

    let (secrets: Union<string, KeyVault.SecretPermissions> []) =
        [| Union.FromT0("list")
           Union.FromT0("get") |]

    let permissionArgs =
        KeyVault.Inputs.PermissionsArgs(Secrets = secrets)

    let accessPolicyEntryArgs =
        KeyVault.Inputs.AccessPolicyEntryArgs(ObjectId = managedIdentityObjectId, TenantId = tenantId, Permissions = permissionArgs)

    let skuArgs =
        KeyVault.Inputs.SkuArgs(Family = "A", Name = KeyVault.SkuName.Standard)

    let vaultPropertiesArgs =
        KeyVault.Inputs.VaultPropertiesArgs(
            EnabledForDeployment = true,
            EnabledForDiskEncryption = true,
            EnabledForTemplateDeployment = true,
            Sku = skuArgs,
            TenantId = tenantId,
            AccessPolicies = accessPolicyEntryArgs
        )

    let keyVaultArgs =
        KeyVault.VaultArgs(Location = Location, VaultName = name, ResourceGroupName = resourceGroupName, Properties = vaultPropertiesArgs)

    KeyVault.Vault(name, keyVaultArgs)

let createPgPassword () =
    let passwordArgs =
        Random.RandomPasswordArgs(Length = 32, Special = true)

    Random.RandomPassword("pgDbPassword", passwordArgs)

let createPostgreSqlServer (adminLogin: string) (adminPassword: Output<string>) (resourceGroupName: Output<string>) =
    let name = "PulumiIaCPocpgdb"

    let skuArgs =
        DBforPostgreSQL.Inputs.SkuArgs(Capacity = 2, Family = "Gen5", Name = "B_Gen5_2", Tier = "Basic")

    let storageProfileArgs =
        DBforPostgreSQL.Inputs.StorageProfileArgs(BackupRetentionDays = 7, GeoRedundantBackup = "Disabled", StorageMB = 5120)

    let propertiesArgs =
        DBforPostgreSQL.Inputs.ServerPropertiesForDefaultCreateArgs(
            AdministratorLogin = adminLogin,
            AdministratorLoginPassword = adminPassword,
            CreateMode = "Default",
            MinimalTlsVersion = "TLS1_2",
            SslEnforcement = DBforPostgreSQL.SslEnforcementEnum.Enabled,
            StorageProfile = storageProfileArgs
        )

    let serverArgs =
        DBforPostgreSQL.ServerArgs(Location = Location, Properties = propertiesArgs, ResourceGroupName = resourceGroupName, ServerName = name, Sku = skuArgs)

    DBforPostgreSQL.Server(name, serverArgs)

let addSecretToKeyVault (secretName: string) (secretValue: Output<string>) (keyVaultName: Output<string>) (resourceGroupName: Output<string>) =
    let secretProperties =
        KeyVault.Inputs.SecretPropertiesArgs(Value = secretValue)

    let secretArgs =
        KeyVault.SecretArgs(ResourceGroupName = resourceGroupName, SecretName = secretName, VaultName = keyVaultName, Properties = secretProperties)

    KeyVault.Secret(secretName, secretArgs)

let createApplicationInsights (resourceGroupName: Output<string>) =
    let name = "PulumiIaCPocAI"

    let insightsComponentArgs =
        Insights.ComponentArgs(
            ApplicationType = "web",
            FlowType = "Bluefield",
            Kind = "web",
            Location = Location,
            RequestSource = "rest",
            ResourceGroupName = resourceGroupName,
            ResourceName = name
        )

    Insights.Component(name, insightsComponentArgs)

let createAppPlan (resourceGroupName: Output<string>) =
    let name = "PulumiIaCPocAppPlan"

    let skuArgs =
        Web.Inputs.SkuDescriptionArgs(Tier = "Basic", Name = "B1")

    let webPlanArgs =
        Web.AppServicePlanArgs(ResourceGroupName = resourceGroupName, Kind = "Linux", Reserved = true, Sku = skuArgs, Location = Location)

    Web.AppServicePlan(name, webPlanArgs)

let createWebApp (resourceGroupName: Output<string>) (webPlanId: Output<string>) (managedIdentityObjectName: Output<string>) (subscriptionId: string) =
    let name = "PulumiIaCPocWebApp"

    let imageInDockerHub =
        "microsoft/azure-appservices-go-quickstart"

    let siteConfigArgs =
        Web.Inputs.SiteConfigArgs(AlwaysOn = true, LinuxFxVersion = $"DOCKER|{imageInDockerHub}")

    let umiName = Output.Format($"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.ManagedIdentity/userAssignedIdentities/{managedIdentityObjectName}")

    let x = umiName.Apply(fun x ->
        let emptyDict = Dictionary<string, obj>() :> obj
        Map [(x, emptyDict)] |> Map.toSeq |> dict)

    let managedServiceIdentityArgs =
        Web.Inputs.ManagedServiceIdentityArgs(Type = Web.ManagedServiceIdentityType.UserAssigned, UserAssignedIdentities = x)

    let webAppArgs =
        Web.WebAppArgs(
            Name = name,
            ResourceGroupName = resourceGroupName,
            ServerFarmId = webPlanId,
            SiteConfig = siteConfigArgs,
            HttpsOnly = true,
            Identity = managedServiceIdentityArgs
        )

    Web.WebApp(name, webAppArgs)

type MyStack() =
    inherit Stack()

    let config = Authorization.GetClientConfig.InvokeAsync() |> Async.AwaitTask |> Async.RunSynchronously

    let resourceGroup = createResourceGroup ()

    let userAssignedIdentity =
        createUserAssignedIdentity resourceGroup.Name

    let keyVault =
        createKeyVault userAssignedIdentity.PrincipalId userAssignedIdentity.TenantId resourceGroup.Name

    let pgAdminPassword = createPgPassword ()

    let pgServer =
        createPostgreSqlServer "pulumiPgAdmin" pgAdminPassword.Result resourceGroup.Name

    let pgAdminPasswordSecret =
        addSecretToKeyVault "pgAdminPassword" pgAdminPassword.Result keyVault.Name resourceGroup.Name

    let applicationInsights =
        createApplicationInsights resourceGroup.Name

    let webPlan = createAppPlan resourceGroup.Name

    let webApp = createWebApp resourceGroup.Name webPlan.Id userAssignedIdentity.Name config.SubscriptionId
