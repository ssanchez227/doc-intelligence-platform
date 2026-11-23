terraform {
  required_version = ">= 1.9"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 5.0"
    }
    azapi = {
      source  = "azure/azapi"
      version = "~> 2.0"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.6"
    }
  }

  # Storage account and resource group come from backend.hcl (gitignored), written
  # from the bootstrap's backend_config output. Init with:
  #   terraform init -backend-config=backend.hcl
  backend "azurerm" {
    container_name   = "tfstate"
    key              = "demo.tfstate"
    use_azuread_auth = true
  }
}

provider "azurerm" {
  features {}
  storage_use_azuread = true

  # The subscription is new, so register only what this project uses.
  resource_provider_registrations = "none"
  resource_providers_to_register = [
    "Microsoft.App",
    "Microsoft.ContainerRegistry",
    "Microsoft.Insights",
    "Microsoft.KeyVault",
    "Microsoft.ManagedIdentity",
    "Microsoft.OperationalInsights",
    "Microsoft.ServiceBus",
    "Microsoft.Sql",
    "Microsoft.Storage",
  ]
}

provider "azapi" {}

module "platform" {
  source = "../../modules/platform"

  environment           = "demo"
  location              = var.location
  sql_admin_group       = var.sql_admin_group
  budget_amount         = var.budget_amount
  budget_start_date     = var.budget_start_date
  budget_contact_emails = var.budget_contact_emails
}
