resource "random_string" "suffix" {
  length  = 5
  special = false
  upper   = false
}

locals {
  name   = "${var.prefix}-${random_string.suffix.result}"
  nodash = "${var.prefix}${random_string.suffix.result}"
}

resource "azurerm_resource_group" "main" {
  name     = "rg-${local.name}"
  location = var.location
  tags     = var.tags
}

data "azurerm_client_config" "current" {}
