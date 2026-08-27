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
  name                 = "gpt-4.1-mini"
  cognitive_account_id = azurerm_cognitive_account.openai.id
  model {
    format  = "OpenAI"
    name    = "gpt-4.1-mini"
    version = "2025-04-14"
  }
  sku {
    # Standard, niet GlobalStandard: gpt-4o-mini 2024-07-18 is per 31-03-2026 gedeprecieerd
    # (ServiceModelDeprecated), vervangen door gpt-4.1-mini 2025-04-14. Quota-check (27-08-2026)
    # op deze subscription in swedencentral toont OpenAI.GlobalStandard.gpt-4o-mini = 0 en
    # OpenAI.Standard.gpt-4.1-mini = 200, dus Standard.
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
    # GlobalStandard is de enige aangeboden SKU voor text-embedding-3-small v1 in swedencentral;
    # quota 1000K TPM bevestigd op 27-08-2026.
    name     = "GlobalStandard"
    capacity = var.embedding_model_tpm_thousands
  }
  depends_on = [azurerm_cognitive_deployment.chat]
}
