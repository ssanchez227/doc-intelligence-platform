data "azurerm_client_config" "current" {}

resource "random_string" "suffix" {
  length  = 5
  upper   = false
  special = false
}

locals {
  name = "${var.project}-${var.environment}"
  flat = "${var.project}${var.environment}${random_string.suffix.result}" # for names that allow no hyphens
  tags = {
    project     = var.project
    environment = var.environment
    managed-by  = "terraform"
  }
}

resource "azurerm_resource_group" "main" {
  name     = "rg-${local.name}"
  location = var.location
  tags     = local.tags
}

# ---------------------------------------------------------------------------
# Observability: workspace-based Application Insights, capped under the free 5 GB.
# ---------------------------------------------------------------------------

resource "azurerm_log_analytics_workspace" "main" {
  name                = "log-${local.name}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  sku                 = "PerGB2018"
  retention_in_days   = 30
  daily_quota_gb      = var.log_daily_cap_gb
  tags                = local.tags
}

resource "azurerm_application_insights" "main" {
  name                = "appi-${local.name}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  workspace_id        = azurerm_log_analytics_workspace.main.id
  application_type    = "web"
  tags                = local.tags
}

# ---------------------------------------------------------------------------
# Identities: one user-assigned identity per app, so each gets only the roles it needs.
# ---------------------------------------------------------------------------

resource "azurerm_user_assigned_identity" "app" {
  for_each            = toset(["api", "worker", "web"])
  name                = "id-${local.name}-${each.key}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  tags                = local.tags
}

# ---------------------------------------------------------------------------
# Container registry (Basic: see the cost estimate in the decision log).
# ---------------------------------------------------------------------------

resource "azurerm_container_registry" "main" {
  name                = "cr${local.flat}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  sku                 = "Basic"
  admin_enabled       = false
  tags                = local.tags
}

resource "azurerm_role_assignment" "acr_pull" {
  for_each             = azurerm_user_assigned_identity.app
  scope                = azurerm_container_registry.main.id
  role_definition_name = "AcrPull"
  principal_id         = each.value.principal_id
}

# ---------------------------------------------------------------------------
# Storage: original files and extracted text. Entra auth only, no shared keys.
# ---------------------------------------------------------------------------

resource "azurerm_storage_account" "main" {
  name                            = "st${local.flat}"
  resource_group_name             = azurerm_resource_group.main.name
  location                        = azurerm_resource_group.main.location
  account_tier                    = "Standard"
  account_replication_type        = "LRS"
  min_tls_version                 = "TLS1_2"
  shared_access_key_enabled       = false
  allow_nested_items_to_be_public = false
  default_to_oauth_authentication = true

  blob_properties {
    versioning_enabled = true
    delete_retention_policy {
      days = 30
    }
  }

  tags = local.tags
}

resource "azurerm_storage_container" "documents" {
  name                  = "documents"
  storage_account_id    = azurerm_storage_account.main.id
  container_access_type = "private"
}

resource "azurerm_role_assignment" "blob_rw" {
  for_each             = { for k, v in azurerm_user_assigned_identity.app : k => v if k != "web" }
  scope                = azurerm_storage_account.main.id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = each.value.principal_id
}

# ---------------------------------------------------------------------------
# Service Bus (Basic): the ingestion queue, with retries and a dead-letter queue.
# ---------------------------------------------------------------------------

resource "azurerm_servicebus_namespace" "main" {
  name                = "sb-${local.name}-${random_string.suffix.result}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  sku                 = "Basic"
  local_auth_enabled  = false
  minimum_tls_version = "1.2"
  tags                = local.tags
}

resource "azurerm_servicebus_queue" "ingestion" {
  name                                 = "ingestion"
  namespace_id                         = azurerm_servicebus_namespace.main.id
  max_delivery_count                   = 5
  lock_duration                        = "PT5M"
  dead_lettering_on_message_expiration = true
}

resource "azurerm_role_assignment" "sb_send" {
  scope                = azurerm_servicebus_queue.ingestion.id
  role_definition_name = "Azure Service Bus Data Sender"
  principal_id         = azurerm_user_assigned_identity.app["api"].principal_id
}

resource "azurerm_role_assignment" "sb_receive" {
  scope                = azurerm_servicebus_queue.ingestion.id
  role_definition_name = "Azure Service Bus Data Receiver"
  principal_id         = azurerm_user_assigned_identity.app["worker"].principal_id
}

# ---------------------------------------------------------------------------
# Key Vault: RBAC mode. Few secrets are expected, since services use managed identity.
# ---------------------------------------------------------------------------

resource "azurerm_key_vault" "main" {
  name                       = "kv-${local.flat}"
  resource_group_name        = azurerm_resource_group.main.name
  location                   = azurerm_resource_group.main.location
  tenant_id                  = data.azurerm_client_config.current.tenant_id
  sku_name                   = "standard"
  rbac_authorization_enabled = true
  soft_delete_retention_days = 7
  purge_protection_enabled   = false
  tags                       = local.tags
}

resource "azurerm_role_assignment" "kv_secrets_user" {
  for_each             = { for k, v in azurerm_user_assigned_identity.app : k => v if k != "web" }
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = each.value.principal_id
}

resource "azurerm_role_assignment" "kv_admin_operator" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Administrator"
  principal_id         = data.azurerm_client_config.current.object_id
}

# ---------------------------------------------------------------------------
# Azure SQL: Entra-only authentication; the database uses the free offer and
# pauses when the monthly free amount is used up, so it never bills.
# ---------------------------------------------------------------------------

resource "azurerm_mssql_server" "main" {
  name                          = "sql-${local.name}-${random_string.suffix.result}"
  resource_group_name           = azurerm_resource_group.main.name
  location                      = azurerm_resource_group.main.location
  version                       = "12.0"
  minimum_tls_version           = "1.2"
  public_network_access_enabled = true

  azuread_administrator {
    login_username              = var.sql_admin_group.login
    object_id                   = var.sql_admin_group.object_id
    azuread_authentication_only = true
  }

  tags = local.tags
}

# Lets Azure services (Container Apps) reach the server. Private networking is an M8 hardening item.
resource "azurerm_mssql_firewall_rule" "azure_services" {
  name             = "AllowAzureServices"
  server_id        = azurerm_mssql_server.main.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}

# azapi, because the free-offer settings are newer than the azurerm database resource.
resource "azapi_resource" "sql_database" {
  type      = "Microsoft.Sql/servers/databases@2023-08-01"
  name      = "sqldb-${local.name}"
  parent_id = azurerm_mssql_server.main.id
  location  = azurerm_resource_group.main.location
  tags      = local.tags

  body = {
    sku = {
      name     = "GP_S_Gen5_2"
      tier     = "GeneralPurpose"
      family   = "Gen5"
      capacity = 2
    }
    properties = {
      useFreeLimit                     = true
      freeLimitExhaustionBehavior      = "AutoPause"
      autoPauseDelay                   = 60
      minCapacity                      = 0.5
      maxSizeBytes                     = 34359738368
      requestedBackupStorageRedundancy = "Local"
    }
  }
}

# ---------------------------------------------------------------------------
# Container Apps: consumption environment, three apps that scale to zero.
# CI replaces the placeholder image, so Terraform ignores image changes.
# ---------------------------------------------------------------------------

resource "azurerm_container_app_environment" "main" {
  name                       = "cae-${local.name}"
  resource_group_name        = azurerm_resource_group.main.name
  location                   = azurerm_resource_group.main.location
  log_analytics_workspace_id = azurerm_log_analytics_workspace.main.id
  tags                       = local.tags
}

locals {
  apps = {
    web    = { ingress = true, port = 80 }
    api    = { ingress = true, port = 80 }
    worker = { ingress = false, port = 0 }
  }
}

resource "azurerm_container_app" "app" {
  for_each                     = local.apps
  name                         = "ca-${local.name}-${each.key}"
  resource_group_name          = azurerm_resource_group.main.name
  container_app_environment_id = azurerm_container_app_environment.main.id
  revision_mode                = "Single"
  tags                         = merge(local.tags, { app = each.key })

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.app[each.key].id]
  }

  registry {
    server   = azurerm_container_registry.main.login_server
    identity = azurerm_user_assigned_identity.app[each.key].id
  }

  dynamic "ingress" {
    for_each = each.value.ingress ? [1] : []
    content {
      external_enabled = true
      target_port      = each.value.port
      transport        = "auto"
      traffic_weight {
        latest_revision = true
        percentage      = 100
      }
    }
  }

  template {
    min_replicas = 0
    max_replicas = 1

    container {
      name   = each.key
      image  = var.placeholder_image
      cpu    = 0.25
      memory = "0.5Gi"

      env {
        name  = "APPLICATIONINSIGHTS_CONNECTION_STRING"
        value = azurerm_application_insights.main.connection_string
      }
      env {
        name  = "AZURE_CLIENT_ID"
        value = azurerm_user_assigned_identity.app[each.key].client_id
      }
    }
  }

  lifecycle {
    ignore_changes = [template[0].container[0].image]
  }

  depends_on = [azurerm_role_assignment.acr_pull]
}

# ---------------------------------------------------------------------------
# Budget: alerts at 50%, 80% and 100% of actual spend, and 100% of forecast.
# ---------------------------------------------------------------------------

resource "azurerm_consumption_budget_resource_group" "main" {
  name              = "budget-${local.name}"
  resource_group_id = azurerm_resource_group.main.id
  amount            = var.budget_amount
  time_grain        = "Monthly"

  time_period {
    start_date = var.budget_start_date
  }

  dynamic "notification" {
    for_each = [50, 80, 100]
    content {
      enabled        = true
      threshold      = notification.value
      operator       = "GreaterThanOrEqualTo"
      threshold_type = "Actual"
      contact_emails = var.budget_contact_emails
    }
  }

  notification {
    enabled        = true
    threshold      = 100
    operator       = "GreaterThanOrEqualTo"
    threshold_type = "Forecasted"
    contact_emails = var.budget_contact_emails
  }

  lifecycle {
    ignore_changes = [time_period]
  }
}
