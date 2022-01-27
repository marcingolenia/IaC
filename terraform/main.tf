terraform {
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 2.65"
    }
  }

  required_version = ">= 1.1.0"
}

provider "azurerm" {
  features {
    key_vault {
      purge_soft_delete_on_destroy = true
    }
  }
}

variable "resource_group_name" {
    default = "terraformRG2"
}

variable "location" {
    default = "northeurope"
}

variable "pg_admin_password" {
    default = "pgAdminP@ssword4321"
}


data "azurerm_client_config" "current" {}

resource "azurerm_resource_group" "rg" {
  name     = var.resource_group_name
  location = var.location
}

resource "azurerm_user_assigned_identity" "umi" {
  resource_group_name = azurerm_resource_group.rg.name
  location            = azurerm_resource_group.rg.location
  name = "terraform-umi"
  
}

resource "azurerm_key_vault" "kv" {
  name                        = "${var.resource_group_name}terraformkv"
  location                    = azurerm_resource_group.rg.location
  resource_group_name         = azurerm_resource_group.rg.name
  enabled_for_template_deployment = true
  enabled_for_disk_encryption = true
  enabled_for_deployment = true
  tenant_id                   = data.azurerm_client_config.current.tenant_id

  sku_name = "standard"

  access_policy {
    tenant_id = data.azurerm_client_config.current.tenant_id
    object_id = data.azurerm_client_config.current.object_id

     key_permissions = [ "get","list","update","create","import","delete","recover","backup","restore"]

    secret_permissions = ["get","list","delete","recover","backup","restore","set"]

    certificate_permissions = ["get","list","update","create","import","delete","recover","backup","restore", "deleteissuers", "getissuers", "listissuers", "managecontacts", "manageissuers", "setissuers"]
  }
}

resource "azurerm_key_vault_access_policy" "umiaccesspolicy" {
  key_vault_id = azurerm_key_vault.kv.id
  tenant_id    = data.azurerm_client_config.current.tenant_id
  object_id    = azurerm_user_assigned_identity.umi.principal_id


  secret_permissions = [
    "Get", "List"
  ]
}


resource "azurerm_postgresql_server" "pgServer" {
  name                = "terraform-pgserver-poc-rg2"
  location            = azurerm_resource_group.rg.location
  resource_group_name = azurerm_resource_group.rg.name

  administrator_login          = "terraformPgAdmin"
  administrator_login_password = var.pg_admin_password

  sku_name   = "B_Gen5_2"
  version    = "10.0"
  storage_mb = 5120
  create_mode = "Default"

  backup_retention_days        = 7
  geo_redundant_backup_enabled = false

  ssl_enforcement_enabled          = true
  ssl_minimal_tls_version_enforced = "TLS1_2"
}

resource "azurerm_key_vault_secret" "pgAdminPassword" {
  name         = "pgAdminPassword"
  value        = var.pg_admin_password
  key_vault_id = azurerm_key_vault.kv.id
}

resource "azurerm_application_insights" "ai" {
  name                = "terraformPocAi${var.resource_group_name}"
  location            = azurerm_resource_group.rg.location
  resource_group_name = azurerm_resource_group.rg.name
  application_type    = "web"
}

resource "azurerm_app_service_plan" "appPlan" {
  name                = "terraformPocAppPlan${var.resource_group_name}"
  location            = azurerm_resource_group.rg.location
  resource_group_name = azurerm_resource_group.rg.name
  kind                = "Linux"
  reserved            = true

  sku {
    tier = "Basic"
    size = "B1"
  }
}

resource "azurerm_app_service" "webAp" {
  name                = "terraformPocWebApp${var.resource_group_name}"
  location            = "${azurerm_resource_group.rg.location}"
  resource_group_name = "${azurerm_resource_group.rg.name}"
  app_service_plan_id = "${azurerm_app_service_plan.appPlan.id}"

  app_settings = {
    WEBSITES_ENABLE_APP_SERVICE_STORAGE = false
    DOCKER_REGISTRY_SERVER_URL = "https://index.docker.io"
  }

  site_config {
    linux_fx_version = "DOCKER|appsvcsample/python-helloworld:latest"
    always_on        = "true"
  }

  identity {
    type = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.umi.id]
  }
}