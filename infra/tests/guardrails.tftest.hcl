mock_provider "azurerm" {}
mock_provider "random" {}

run "budget_is_capped_and_alerts_on_three_thresholds" {
  command = plan
  assert {
    condition     = azurerm_consumption_budget_subscription.main.amount <= 25
    error_message = "Budget boven €25."
  }
  assert {
    condition     = length(azurerm_consumption_budget_subscription.main.notification) == 3
    error_message = "Verwacht alerts op 50/80/100 %."
  }
}

run "model_quota_is_low" {
  command = plan
  assert {
    condition     = azurerm_cognitive_deployment.chat.sku[0].capacity <= 20
    error_message = "Chat-TPM te hoog voor demo-budget."
  }
}

run "app_scales_to_zero_single_replica" {
  command = plan
  assert {
    condition     = azurerm_container_app.api.template[0].min_replicas == 0 && azurerm_container_app.api.template[0].max_replicas == 1
    error_message = "Container App moet 0..1 replica's zijn."
  }
}

run "no_local_auth_on_data_services" {
  command = plan
  assert {
    condition     = azurerm_search_service.main.local_authentication_enabled == false && azurerm_cognitive_account.openai.local_auth_enabled == false && azurerm_storage_account.main.shared_access_key_enabled == false
    error_message = "Keys moeten uit staan; alleen RBAC."
  }
}

run "budget_over_25_is_rejected" {
  command = plan
  variables {
    budget_amount_eur = 30
  }
  expect_failures = [var.budget_amount_eur]
}
