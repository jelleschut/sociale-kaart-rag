terraform {
  required_version = ">= 1.9"
  required_providers {
    azurerm = { source = "hashicorp/azurerm", version = "~> 4.30" }
    random  = { source = "hashicorp/random", version = "~> 3.6" }
  }
  backend "azurerm" {
    container_name   = "tfstate"
    key              = "sociale-kaart-rag.tfstate"
    use_azuread_auth = true
  }
}

provider "azurerm" {
  features {
    resource_group { prevent_deletion_if_contains_resources = false }
    cognitive_account { purge_soft_delete_on_destroy = true }
  }
  storage_use_azuread = true
}
