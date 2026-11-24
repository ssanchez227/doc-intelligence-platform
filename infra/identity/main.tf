terraform {
  required_version = ">= 1.9"

  required_providers {
    azuread = {
      source  = "hashicorp/azuread"
      version = "~> 3.0"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.6"
    }
  }

  # Same state storage as the environments; backend.hcl (gitignored) holds its names.
  backend "azurerm" {
    container_name   = "tfstate"
    key              = "identity.tfstate"
    use_azuread_auth = true
  }
}

provider "azuread" {}

data "azuread_client_config" "current" {}

data "azuread_domains" "initial" {
  only_initial = true
}

locals {
  owners = [data.azuread_client_config.current.object_id]
  domain = data.azuread_domains.initial.domains[0].domain_name

  # Role groups match the actors in the scenarios. Department groups stand in for
  # the real groups that per-document permissions are granted to.
  groups = {
    readers        = "Readers"
    contributors   = "Contributors"
    reviewers      = "Reviewers"
    administrators = "Administrators"
    finance        = "Finance"
    hr             = "HR"
  }

  # "outsider" belongs to no group, for the access-denied scenarios.
  test_users = {
    reader      = ["readers", "finance"]
    contributor = ["readers", "contributors", "finance"]
    reviewer    = ["readers", "reviewers", "hr"]
    admin       = ["readers", "administrators"]
    outsider    = []
  }

  memberships = merge([
    for user, groups in local.test_users : {
      for g in groups : "${user}-${g}" => { user = user, group = g }
    }
  ]...)
}

# --- API -------------------------------------------------------------------

resource "random_uuid" "api_scope" {}

resource "azuread_application" "api" {
  display_name     = "DocIntel API"
  owners           = local.owners
  sign_in_audience = "AzureADMyOrg"

  # Group object IDs go into the token; overage (many groups) is handled later.
  group_membership_claims = ["SecurityGroup"]

  api {
    requested_access_token_version = 2

    oauth2_permission_scope {
      id                         = random_uuid.api_scope.result
      value                      = "access_as_user"
      type                       = "User"
      enabled                    = true
      admin_consent_display_name = "Use the Document Intelligence API"
      admin_consent_description  = "Lets the web app call the Document Intelligence API as the signed-in user."
      user_consent_display_name  = "Use the Document Intelligence API"
      user_consent_description   = "Lets the web app call the Document Intelligence API for you."
    }
  }

  # The identifier URI needs the client ID, so a separate resource sets it.
  lifecycle {
    ignore_changes = [identifier_uris]
  }
}

resource "azuread_application_identifier_uri" "api" {
  application_id = azuread_application.api.id
  identifier_uri = "api://${azuread_application.api.client_id}"
}

resource "azuread_service_principal" "api" {
  client_id = azuread_application.api.client_id
  owners    = local.owners
}

# --- Web (SPA, auth code flow with PKCE) -------------------------------------

resource "azuread_application" "web" {
  display_name     = "DocIntel Web"
  owners           = local.owners
  sign_in_audience = "AzureADMyOrg"

  single_page_application {
    redirect_uris = var.web_redirect_uris
  }

  required_resource_access {
    resource_app_id = azuread_application.api.client_id

    resource_access {
      id   = random_uuid.api_scope.result
      type = "Scope"
    }
  }
}

resource "azuread_service_principal" "web" {
  client_id = azuread_application.web.client_id
  owners    = local.owners
}

# The web app may request the API scope without a consent prompt.
resource "azuread_application_pre_authorized" "web" {
  application_id       = azuread_application.api.id
  authorized_client_id = azuread_application.web.client_id
  permission_ids       = [random_uuid.api_scope.result]
}

# --- Test groups and users ---------------------------------------------------

resource "azuread_group" "group" {
  for_each = local.groups

  display_name     = "DocIntel ${each.value}"
  description      = "Test group for the Document Intelligence Platform."
  owners           = local.owners
  security_enabled = true
}

resource "random_password" "test_user" {
  for_each = local.test_users

  length      = 24
  min_upper   = 1
  min_lower   = 1
  min_numeric = 1
  min_special = 1
}

resource "azuread_user" "test" {
  for_each = local.test_users

  user_principal_name   = "docintel.${each.key}@${local.domain}"
  display_name          = "DocIntel Test ${title(each.key)}"
  mail_nickname         = "docintel.${each.key}"
  password              = random_password.test_user[each.key].result
  force_password_change = false
}

resource "azuread_group_member" "test" {
  for_each = local.memberships

  group_object_id  = azuread_group.group[each.value.group].object_id
  member_object_id = azuread_user.test[each.value.user].object_id
}
