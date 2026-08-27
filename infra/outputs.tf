output "resource_group" { value = azurerm_resource_group.main.name }
output "openai_endpoint" { value = azurerm_cognitive_account.openai.endpoint }
output "search_endpoint" { value = "https://${azurerm_search_service.main.name}.search.windows.net" }
output "storage_account_url" { value = azurerm_storage_account.main.primary_blob_endpoint }
output "app_identity_client_id" { value = azurerm_user_assigned_identity.app.client_id }
output "api_url" { value = "https://${azurerm_container_app.api.ingress[0].fqdn}" }
output "app_name" { value = azurerm_container_app.api.name }
output "appinsights_connection_string" {
  value     = azurerm_application_insights.main.connection_string
  sensitive = true
}
output "chat_deployment" { value = azurerm_cognitive_deployment.chat.name }
output "embedding_deployment" { value = azurerm_cognitive_deployment.embedding.name }
