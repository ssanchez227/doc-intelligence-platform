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
      name     = "GP_S_Gen5"
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
  logs_destination           = "log-analytics"
  log_analytics_workspace_id = azurerm_log_analytics_workspace.main.id
  tags                       = local.tags

  # Azure adds this profile to every new environment; declaring it avoids a diff on each plan.
  workload_profile {
    name                  = "Consumption"
    workload_profile_type = "Consumption"
  }
}

locals {
  # All images listen on 8080 and run as non-root.
  apps = {
    web    = { ingress = true, port = 8080 }
    api    = { ingress = true, port = 8080 }
    worker = { ingress = false, port = 0 }
  }

  # Built from the environment's domain: the apps cannot refer to each other's
  # ingress without a dependency cycle.
  web_url         = "https://ca-${local.name}-web.${azurerm_container_app_environment.main.default_domain}"
  servicebus_fqdn = "${azurerm_servicebus_namespace.main.name}.servicebus.windows.net"

  # Settings per app, on top of the Application Insights and identity ones every app gets.
  app_settings = {
    web = {}
    api = merge({
      Auth__TenantId                      = var.entra_tenant_id
      Auth__Audience                      = var.api_audience
      Cors__AllowedOrigins__0             = local.web_url
      ServiceBus__FullyQualifiedNamespace = local.servicebus_fqdn

      # M1: Azure SQL through the API's managed identity (its database user comes
      # from scripts/dev/grant-api-database-access.sql). The API applies the EF
      # migrations at startup. A paused serverless database takes up to a minute
      # to resume, hence the long connect timeout.
      Database__Provider         = "SqlServer"
      Database__MigrateOnStartup = "true"
      ConnectionStrings__DocIntel = join(";", [
        "Server=tcp:${azurerm_mssql_server.main.fully_qualified_domain_name},1433",
        "Database=${azapi_resource.sql_database.name}",
        "Authentication=Active Directory Managed Identity",
        "User Id=${azurerm_user_assigned_identity.app["api"].client_id}",
        "Encrypt=True",
        "Connect Timeout=90",
      ])

      # Document files in Blob Storage, also through the managed identity.
      Storage__Provider                = "Blob"
      Storage__BlobServiceUri          = azurerm_storage_account.main.primary_blob_endpoint
      Storage__Container               = azurerm_storage_container.documents.name
      Storage__ManagedIdentityClientId = azurerm_user_assigned_identity.app["api"].client_id

      # Publishing without a review, until M5 (decision log, M1 decisions).
      Features__DevPublish = tostring(var.dev_publish)
      },
      # Group keys the API stores -> this tenant's Entra group IDs.
      { for key, id in var.group_object_ids : "Groups__${key}" => id },
    )
    worker = {
      ServiceBus__FullyQualifiedNamespace = local.servicebus_fqdn
      ServiceBus__QueueName               = azurerm_servicebus_queue.ingestion.name
    }
  }
}

resource "azurerm_container_app" "app" {
  for_each                     = local.apps
  name                         = "ca-${local.name}-${each.key}"
  resource_group_name          = azurerm_resource_group.main.name
  container_app_environment_id = azurerm_container_app_environment.main.id
  revision_mode                = "Single"
  workload_profile_name        = "Consumption"
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

      dynamic "env" {
        for_each = local.app_settings[each.key]
        content {
          name  = env.key
          value = env.value
        }
      }
    }

    # The worker has no ingress, so only queue length can wake it from zero.
    # KEDA reads the queue with the worker's identity (it has the Receiver role).
    dynamic "custom_scale_rule" {
      for_each = each.key == "worker" ? [1] : []
      content {
        name             = "ingestion-queue"
        custom_rule_type = "azure-servicebus"
        identity_id      = azurerm_user_assigned_identity.app["worker"].id
        metadata = {
          namespace    = azurerm_servicebus_namespace.main.name
          queueName    = azurerm_servicebus_queue.ingestion.name
          messageCount = "5"
        }
      }
    }
  }

  lifecycle {
    ignore_changes = [template[0].container[0].image]
  }

  depends_on = [azurerm_role_assignment.acr_pull]
}

# ---------------------------------------------------------------------------
# Deployment from GitHub Actions: OIDC federation, no stored secrets. The
# identity trusts only workflow jobs that run in this environment's GitHub
# environment, and may only push images and update the three apps.
# ---------------------------------------------------------------------------

resource "azurerm_user_assigned_identity" "deploy" {
  name                = "id-${local.name}-deploy"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  tags                = local.tags
}

resource "azurerm_federated_identity_credential" "github" {
  name                      = "github-${var.environment}"
  user_assigned_identity_id = azurerm_user_assigned_identity.deploy.id
  audience                  = ["api://AzureADTokenExchange"]
  issuer                    = "https://token.actions.githubusercontent.com"
  subject                   = "repo:${var.github_repository}:environment:${var.environment}"
}

resource "azurerm_role_assignment" "deploy_acr_push" {
  scope                = azurerm_container_registry.main.id
  role_definition_name = "AcrPush"
  principal_id         = azurerm_user_assigned_identity.deploy.principal_id
}

resource "azurerm_role_assignment" "deploy_apps" {
  for_each             = azurerm_container_app.app
  scope                = each.value.id
  role_definition_name = "Container Apps Contributor"
  principal_id         = azurerm_user_assigned_identity.deploy.principal_id
}

resource "azurerm_role_assignment" "deploy_environment_reader" {
  scope                = azurerm_container_app_environment.main.id
  role_definition_name = "Container Apps ManagedEnvironments Reader"
  principal_id         = azurerm_user_assigned_identity.deploy.principal_id
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
