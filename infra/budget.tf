resource "azurerm_consumption_budget_subscription" "main" {
  name            = "budget-${local.name}"
  subscription_id = "/subscriptions/${data.azurerm_client_config.current.subscription_id}"
  amount          = var.budget_amount_eur
  time_grain      = "Monthly"

  time_period {
    start_date = "2026-09-01T00:00:00Z"
  }

  dynamic "notification" {
    for_each = [50, 80, 100]
    content {
      enabled        = true
      threshold      = notification.value
      operator       = "GreaterThanOrEqualTo"
      threshold_type = "Actual"
      contact_emails = [var.budget_contact_email]
    }
  }
}
