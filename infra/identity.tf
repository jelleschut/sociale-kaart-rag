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

# Operator: de ontwikkelaar die lokaal terraform draait (User) krijgt dezelfde data-rollen zodat Ingest
# en lokale integratietests met DefaultAzureCredential werken. De principal staat expliciet in
# var.operator_object_id (lokaal én in CI dezelfde waarde) — niet afgeleid van "wie draait apply":
# dat wisselde per context (User lokaal, CI-identity in Actions) en gaf een replace per apply plus een
# 409 RoleAssignmentExists zodra de CI-identity ook via azurerm_role_assignment.ci zijn rollen kreeg.
# Leeg = geen operator-rollen (de CI-identity heeft zijn eigen rollen hieronder).
resource "azurerm_role_assignment" "operator" {
  for_each             = var.operator_object_id == "" ? {} : local.app_roles
  principal_id         = var.operator_object_id
  principal_type       = "User"
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
