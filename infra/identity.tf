resource "azurerm_user_assigned_identity" "app" {
  name                = "id-${local.name}-app"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  tags                = var.tags
}

locals {
  app_roles = {
    search_data    = { role = "Search Index Data Contributor", scope = azurerm_search_service.main.id }
    search_service = { role = "Search Service Contributor", scope = azurerm_search_service.main.id }
    openai_user    = { role = "Cognitive Services OpenAI User", scope = azurerm_cognitive_account.openai.id }
    blob_data      = { role = "Storage Blob Data Contributor", scope = azurerm_storage_account.main.id }
  }
}

resource "azurerm_role_assignment" "app" {
  for_each             = local.app_roles
  principal_id         = azurerm_user_assigned_identity.app.principal_id
  principal_type       = "ServicePrincipal"
  role_definition_name = each.value.role
  scope                = each.value.scope
}

# Operator (the identity running terraform: developer locally or CI identity) gets the same data roles
# so Ingest and local integration tests work with DefaultAzureCredential.
# Geen principal_type: de operator is een User (lokaal) of ServicePrincipal (CI/OIDC); de provider
# zoekt het type zelf op. De app-identity hierboven heeft het wél nodig omdat die in dezelfde apply ontstaat.
resource "azurerm_role_assignment" "operator" {
  for_each             = local.app_roles
  principal_id         = data.azurerm_client_config.current.object_id
  role_definition_name = each.value.role
  scope                = each.value.scope
}

# De CI-identity (GitHub OIDC, aangemaakt door bootstrap.ps1) draait ingest en eval en heeft daarvoor
# dezelfde data-rollen nodig als de app; Contributor op de subscription geeft géén data-plane-toegang.
data "azurerm_user_assigned_identity" "ci" {
  name                = "id-skr-github-deploy"
  resource_group_name = "rg-skr-tfstate"
}

resource "azurerm_role_assignment" "ci" {
  for_each             = local.app_roles
  principal_id         = data.azurerm_user_assigned_identity.ci.principal_id
  principal_type       = "ServicePrincipal"
  role_definition_name = each.value.role
  scope                = each.value.scope
}
