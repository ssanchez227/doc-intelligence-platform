output "resource_group_name" {
  value = azurerm_resource_group.main.name
}

output "container_registry_login_server" {
  value = azurerm_container_registry.main.login_server
}

output "app_urls" {
  description = "Public URLs of the apps that have ingress."
  value       = { for k, v in azurerm_container_app.app : k => "https://${v.ingress[0].fqdn}" if local.apps[k].ingress }
}

output "app_identity_client_ids" {
  value = { for k, v in azurerm_user_assigned_identity.app : k => v.client_id }
}

output "sql_server_fqdn" {
  value = azurerm_mssql_server.main.fully_qualified_domain_name
}

output "sql_database_name" {
  value = azapi_resource.sql_database.name
}

output "storage_blob_endpoint" {
  value = azurerm_storage_account.main.primary_blob_endpoint
}

output "servicebus_fqdn" {
  value = "${azurerm_servicebus_namespace.main.name}.servicebus.windows.net"
}

output "key_vault_uri" {
  value = azurerm_key_vault.main.vault_uri
}

output "deploy" {
  description = "Values the GitHub environment needs (none of them are secrets)."
  value = {
    client_id          = azurerm_user_assigned_identity.deploy.client_id
    container_registry = azurerm_container_registry.main.name
    resource_group     = azurerm_resource_group.main.name
    app_names          = { for k, v in azurerm_container_app.app : k => v.name }
  }
}
