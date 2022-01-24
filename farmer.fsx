#r "nuget: Farmer"

open Farmer
open Farmer.Builders
open Farmer.PostgreSQL

let plan = servicePlan {
    name "demoPlan"
}

let ai = appInsights {
    name "demoInsights"
}

let webapp = webApp {
    name "demoWebApp"
    sku WebApp.Sku.Free
    link_to_service_plan plan
    link_to_app_insights ai
    system_identity
    always_on
}

let vault = keyVault {
    name "DemoVault"
    add_access_policies [
        AccessPolicy.create webapp.SystemIdentity
    ]
}

let postgres = postgreSQL {
    admin_username "dbadmin"
    name "postgres"
    capacity 4<VCores>
    storage_size 50<Gb>
    tier GeneralPurpose
    add_database "demo_db"
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
template |> Deploy.execute "rg-farmer" [("password-for-postgres", "admin123!admin123")]