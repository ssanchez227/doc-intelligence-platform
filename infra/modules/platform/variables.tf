variable "project" {
  description = "Short project code used in resource names."
  type        = string
  default     = "dip"
}

variable "environment" {
  description = "Environment name, such as dev or demo."
  type        = string

  validation {
    condition     = contains(["dev", "demo"], var.environment)
    error_message = "environment must be dev or demo."
  }
}

variable "location" {
  description = "Azure region."
  type        = string
  default     = "centralus"
}

variable "sql_admin_group" {
  description = "Entra principal that administers the SQL server (SQL uses Entra-only authentication)."
  type = object({
    login     = string
    object_id = string
  })
}

variable "log_daily_cap_gb" {
  description = "Daily ingestion cap for Log Analytics. 0.15 GB a day keeps a month under the 5 GB free allowance."
  type        = number
  default     = 0.15
}

variable "budget_amount" {
  description = "Monthly budget for the environment's resource group, in the billing currency."
  type        = number
  default     = 10
}

variable "budget_start_date" {
  description = "First day of the budget period, as YYYY-MM-01T00:00:00Z. Must be the current or a later month when first created."
  type        = string
}

variable "budget_contact_emails" {
  description = "Who receives budget alerts. Pass in a gitignored tfvars file or TF_VAR_budget_contact_emails; never commit it."
  type        = list(string)
  sensitive   = true
}

variable "placeholder_image" {
  description = "Image the container apps start with, until CI deploys the real ones."
  type        = string
  default     = "mcr.microsoft.com/k8se/quickstart:latest"
}

variable "github_repository" {
  description = "Repository part of the GitHub OIDC subject claim, in the immutable form owner@owner_id/name@repo_id (from the repo's actions/oidc/customization/sub API)."
  type        = string
}

variable "entra_tenant_id" {
  description = "Tenant that issues the API's access tokens (infra/identity output tenant_id)."
  type        = string
}

variable "api_audience" {
  description = "The API's client ID, which v2 access tokens carry as aud (infra/identity output api_client_id)."
  type        = string
}
