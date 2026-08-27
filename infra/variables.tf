variable "location" {
  type    = string
  default = "swedencentral"
}
variable "prefix" {
  type    = string
  default = "skr"
}
variable "budget_amount_eur" {
  type    = number
  default = 25
  validation {
    condition     = var.budget_amount_eur <= 25
    error_message = "Budget mag niet boven €25/maand (spec §2)."
  }
}
variable "budget_contact_email" {
  type    = string
  default = "jelleschut@hotmail.com"
}
variable "chat_model_tpm_thousands" {
  type    = number
  default = 10
}
variable "embedding_model_tpm_thousands" {
  type    = number
  default = 30
}
variable "app_image" {
  type    = string
  default = "mcr.microsoft.com/k8se/quickstart:latest"
}
variable "tags" {
  type = map(string)
  default = {
    project    = "sociale-kaart-rag"
    owner      = "jelleschut"
    costcenter = "demo"
  }
}
