#r "nuget: Farmer"

open Farmer
open Farmer.Builders
open Farmer.PostgreSQL
open System 

let dbPass = Guid.NewGuid()
                .ToString()
                .Replace("-", "")

let plan = servicePlan {
    name "rolnikDemoPlan"
}

let ai = appInsights {
    name "rolnikDemoInsights"
}

let webapp = webApp {
    name "rolnikDemoWebApp"
    sku WebApp.Sku.Free
    link_to_service_plan plan
    link_to_app_insights ai
    system_identity
}

let vaultPolicy =
    accessPolicy {
        object_id Guid.Empty
        application_id Guid.Empty
        certificate_permissions [ KeyVault.Certificate.List ]
        secret_permissions KeyVault.Secret.All
        key_permissions [ KeyVault.Key.List ]
    }

let vault = keyVault {
    name "rolnikDemoVault"
    add_access_policy vaultPolicy
    add_secret "simpleSecret"
    add_access_policies [
        AccessPolicy.create webapp.SystemIdentity
    ]
}

let postgres = postgreSQL {
    admin_username "dbadmin"
    name "rolnik-postgres"
    capacity 4<VCores>
    storage_size 50<Gb>
    tier GeneralPurpose
    add_database "rolnikDb"
    enable_azure_firewall
}

let template = arm {
    location Location.NorthEurope
    add_resource plan
    add_resource ai
    add_resource webapp
    add_resource vault
    add_resource postgres
    output "vault-uri" vault.VaultUri
}

Deploy.setSubscription (System.Guid.Parse "a4e0808e-ccc1-41c0-b84f-0b683b57dbde")
template |> Deploy.execute "rg-rolnik" [("password-for-rolnik-postgres", dbPass)]