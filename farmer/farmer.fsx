#r "nuget: Farmer"

open Farmer
open Farmer.Builders
open Farmer.PostgreSQL
open System

let managedIdentity =
    userAssignedIdentity {
        name "FarmerIaCPocUMI"
    }

let umiPolicy =
    accessPolicy {
        object_id managedIdentity.PrincipalId
        certificate_permissions [ KeyVault.Certificate.List ]
        secret_permissions KeyVault.Secret.All
        key_permissions [ KeyVault.Key.List ]
    }


let vault =
    keyVault {
        name "FarmerIaCPocKV"
        sku KeyVault.Sku.Standard

        enable_disk_encryption_access
        enable_resource_manager_access
        enable_soft_delete_with_purge_protection

        add_access_policy umiPolicy

        add_secret "pgAdminPassword"
    }


let postgres = postgreSQL {
    admin_username "farmerPgAdmin"
    name "farmeriacpocpgdb"
    capacity 2<VCores>
    storage_size 5<Gb>
    tier Basic
    backup_retention 7<Days>
    disable_geo_redundant_backup
}



let dbPass = Guid.NewGuid()
                .ToString()
                .Replace("-", "") + "!#"

let plan = servicePlan {
    name "FarmerIaCWebAppPlan"
    operating_system OS.Linux
    sku WebApp.Sku.B1
}

let ai = appInsights {
    name "FarmerIaCAI"
}

let webapp = webApp {
    name "FarmerIaCWebApp"
    sku WebApp.Sku.Free
    link_to_service_plan plan
    link_to_app_insights ai
    add_identity managedIdentity
    docker_image "appsvcsample/python-helloworld:latest" ""
    setting "DOCKER_REGISTRY_SERVER_URL" "https://index.docker.io"
    always_on
}

let template = arm {
    location Location.NorthEurope
    add_resource managedIdentity
    add_resource plan
    add_resource ai
    add_resource webapp
    add_resource vault
    add_resource postgres
    output "vault-uri" vault.VaultUri
}

Deploy.setSubscription (System.Guid.Parse "a4e0808e-ccc1-41c0-b84f-0b683b57dbde")
template |> Deploy.execute "FarmerIaCRG" [
        ("password-for-farmeriacpocpgdb", dbPass)
        ("pgAdminPassword", dbPass)]