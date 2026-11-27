output "tenant_id" {
  value = data.azuread_client_config.current.tenant_id
}

output "api_client_id" {
  description = "The API's audience (v2 tokens carry the client ID as aud)."
  value       = azuread_application.api.client_id
}

output "api_scope" {
  value = "api://${azuread_application.api.client_id}/access_as_user"
}

output "web_client_id" {
  value = azuread_application.web.client_id
}

output "group_object_ids" {
  value = { for k, g in azuread_group.group : k => g.object_id }
}

output "test_users" {
  value = { for k, u in azuread_user.test : k => u.user_principal_name }
}

output "test_user_passwords" {
  description = "Read with: terraform output -json test_user_passwords"
  value       = { for k, p in random_password.test_user : k => p.result }
  sensitive   = true
}
