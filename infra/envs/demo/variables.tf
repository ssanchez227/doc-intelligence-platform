variable "location" {
  type    = string
  default = "centralus"
}

variable "sql_admin_group" {
  description = "Entra principal that administers SQL: login name and object ID."
  type = object({
    login     = string
    object_id = string
  })
}

variable "budget_amount" {
  type    = number
  default = 10
}

variable "budget_start_date" {
  type = string
}

variable "budget_contact_emails" {
  type      = list(string)
  sensitive = true
}

variable "github_repository" {
  type    = string
  default = "ssanchez227@30909494/doc-intelligence-platform@1462134496"
}

variable "entra_tenant_id" {
  description = "infra/identity output tenant_id."
  type        = string
}

variable "api_audience" {
  description = "infra/identity output api_client_id."
  type        = string
}

variable "group_object_ids" {
  description = "Entra group object IDs by key: terraform output -json group_object_ids in infra/identity."
  type        = map(string)
}
