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
