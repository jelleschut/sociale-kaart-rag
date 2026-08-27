resource "azurerm_cognitive_account" "openai" {
  name                          = "oai-${local.name}"
  location                      = azurerm_resource_group.main.location
  resource_group_name           = azurerm_resource_group.main.name
  kind                          = "OpenAI"
  sku_name                      = "S0"
  custom_subdomain_name         = "oai-${local.name}"
  local_auth_enabled            = false
  public_network_access_enabled = true
  tags                          = var.tags
}

resource "azurerm_cognitive_deployment" "chat" {
  name                 = "gpt-4o-mini"
  cognitive_account_id = azurerm_cognitive_account.openai.id
  model {
    format  = "OpenAI"
    name    = "gpt-4o-mini"
    version = "2024-07-18"
  }
  sku {
    # Standard, niet GlobalStandard: quota-check (27-08-2026) op deze subscription in
    # swedencentral toont OpenAI.GlobalStandard.gpt-4o-mini = 0 en OpenAI.Standard.gpt-4o-mini = 200.
    name     = "Standard"
    capacity = var.chat_model_tpm_thousands
  }
}

resource "azurerm_cognitive_deployment" "embedding" {
  name                 = "text-embedding-3-small"
  cognitive_account_id = azurerm_cognitive_account.openai.id
  model {
    format  = "OpenAI"
    name    = "text-embedding-3-small"
    version = "1"
  }
  sku {
    name     = "GlobalStandard"
    capacity = var.embedding_model_tpm_thousands
  }
  depends_on = [azurerm_cognitive_deployment.chat]
}
