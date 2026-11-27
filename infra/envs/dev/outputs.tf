output "platform" {
  value = {
    resource_group_name             = module.platform.resource_group_name
    container_registry_login_server = module.platform.container_registry_login_server
    app_urls                        = module.platform.app_urls
    app_identity_client_ids         = module.platform.app_identity_client_ids
    sql_server_fqdn                 = module.platform.sql_server_fqdn
    sql_database_name               = module.platform.sql_database_name
    storage_blob_endpoint           = module.platform.storage_blob_endpoint
    servicebus_fqdn                 = module.platform.servicebus_fqdn
    key_vault_uri                   = module.platform.key_vault_uri
    deploy                          = module.platform.deploy
  }
}
