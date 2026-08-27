resource "azurerm_storage_account" "main" {
  name                            = "st${local.nodash}"
  resource_group_name             = azurerm_resource_group.main.name
  location                        = azurerm_resource_group.main.location
  account_tier                    = "Standard"
  account_replication_type        = "LRS"
  min_tls_version                 = "TLS1_2"
  allow_nested_items_to_be_public = false
  shared_access_key_enabled       = false
  tags                            = var.tags
}

resource "azurerm_storage_container" "traces" {
  name                  = "traces"
  storage_account_id    = azurerm_storage_account.main.id
  container_access_type = "private"
}

resource "azurerm_storage_container" "snapshots" {
  name                  = "snapshots"
  storage_account_id    = azurerm_storage_account.main.id
  container_access_type = "private"
}

resource "azurerm_storage_management_policy" "traces_retention" {
  storage_account_id = azurerm_storage_account.main.id
  rule {
    name    = "traces-90d"
    enabled = true
    filters {
      prefix_match = ["traces/"]
      blob_types   = ["appendBlob", "blockBlob"]
    }
    actions {
      base_blob { delete_after_days_since_modification_greater_than = 90 }
    }
  }
}
