variable "project" {
  description = "Short project code used in resource names."
  type        = string
  default     = "dip"
}

variable "location" {
  description = "Azure region for the state storage."
  type        = string
  default     = "eastus2"
}
