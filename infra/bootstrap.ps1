#requires -Version 7
<#
Eenmalige bootstrap voor sociale-kaart-rag. Idempotent.
Vereist: az login in het privé-profiel (AZURE_CONFIG_DIR), gh auth login.
Maakt: resource providers, tfstate-storage, user-assigned identity voor GitHub OIDC,
       rollen op de subscription, en GitHub Actions-variabelen.
#>
param(
  [string]$SubscriptionId = "8a2b4868-a9c0-4122-bda8-8823c5c3f3e3",
  [string]$Location = "swedencentral",
  [string]$Repo = "jelleschut/sociale-kaart-rag",
  [string]$StateRg = "rg-skr-tfstate",
  [string]$IdentityName = "id-skr-github-deploy"
)
$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $true
if (-not $env:AZURE_CONFIG_DIR) { throw "AZURE_CONFIG_DIR is niet gezet; weiger tegen het standaardprofiel te draaien." }

function Test-AzExists([scriptblock]$Probe) {
  $prev = $PSNativeCommandUseErrorActionPreference
  $PSNativeCommandUseErrorActionPreference = $false
  try { $out = & $Probe 2>$null; return ($LASTEXITCODE -eq 0 -and $out) } finally { $PSNativeCommandUseErrorActionPreference = $prev }
}

function Ensure-RoleAssignment([string]$ObjectId, [string]$PrincipalType, [string]$Role, [string]$Scope) {
  if (Test-AzExists { az role assignment list --assignee $ObjectId --role $Role --scope $Scope --query "[0].id" -o tsv }) {
    Write-Host "  role: $Role @ $Scope (bestond al)"
  } else {
    az role assignment create --assignee-object-id $ObjectId --assignee-principal-type $PrincipalType --role $Role --scope $Scope | Out-Null
    Write-Host "  role: $Role @ $Scope (aangemaakt)"
  }
}

az account set --subscription $SubscriptionId
$tenant = az account show --query tenantId -o tsv

Write-Host "== resource providers"
foreach ($ns in "Microsoft.CognitiveServices","Microsoft.Search","Microsoft.App","Microsoft.OperationalInsights","Microsoft.Insights","Microsoft.Storage","Microsoft.ManagedIdentity","Microsoft.Consumption","Microsoft.CostManagement") {
  az provider register --namespace $ns --wait | Out-Null
  Write-Host "  $ns registered"
}

Write-Host "== tfstate storage"
az group create -n $StateRg -l $Location --tags project=sociale-kaart-rag owner=jelleschut costcenter=demo | Out-Null
$existing = az storage account list -g $StateRg --query "[?starts_with(name,'stskrtfstate')].name | [0]" -o tsv
if (-not $existing) {
  $suffix = -join (1..6 | ForEach-Object { [char](Get-Random -Minimum 97 -Maximum 123) })
  $existing = "stskrtfstate$suffix"
  az storage account create -n $existing -g $StateRg -l $Location --sku Standard_LRS --kind StorageV2 --allow-blob-public-access false --min-tls-version TLS1_2 | Out-Null
}
$stateAccount = $existing
az storage container create --name tfstate --account-name $stateAccount --auth-mode login | Out-Null
Write-Host "  $stateAccount/tfstate"

Write-Host "== deploy identity + OIDC"
az identity create -n $IdentityName -g $StateRg -l $Location | Out-Null
$id = az identity show -n $IdentityName -g $StateRg | ConvertFrom-Json
foreach ($sub in @("repo:${Repo}:environment:azure", "repo:${Repo}:ref:refs/heads/main", "repo:${Repo}:pull_request")) {
  $fname = ($sub -replace '[^a-zA-Z0-9]', '-')
  if (-not (Test-AzExists { az identity federated-credential show --name $fname --identity-name $IdentityName -g $StateRg --query name -o tsv })) {
    az identity federated-credential create --name $fname --identity-name $IdentityName -g $StateRg `
      --issuer "https://token.actions.githubusercontent.com" --subject $sub --audiences "api://AzureADTokenExchange" | Out-Null
    Write-Host "  federated: $sub (aangemaakt)"
  } else {
    Write-Host "  federated: $sub (bestond al)"
  }
}
$scope = "/subscriptions/$SubscriptionId"
foreach ($role in "Contributor","User Access Administrator") {
  Ensure-RoleAssignment -ObjectId $id.principalId -PrincipalType ServicePrincipal -Role $role -Scope $scope
}
$stateAccountId = az storage account show -n $stateAccount -g $StateRg --query id -o tsv
Ensure-RoleAssignment -ObjectId $id.principalId -PrincipalType ServicePrincipal -Role "Storage Blob Data Contributor" -Scope $stateAccountId
# Ook de ingelogde gebruiker mag de state lezen/schrijven (lokale terraform-runs).
$me = az ad signed-in-user show --query id -o tsv
Ensure-RoleAssignment -ObjectId $me -PrincipalType User -Role "Storage Blob Data Contributor" -Scope $stateAccountId

Write-Host "== GitHub Actions-variabelen"
gh variable set AZURE_CLIENT_ID --repo $Repo --body $id.clientId
gh variable set AZURE_TENANT_ID --repo $Repo --body $tenant
gh variable set AZURE_SUBSCRIPTION_ID --repo $Repo --body $SubscriptionId
gh variable set TFSTATE_STORAGE_ACCOUNT --repo $Repo --body $stateAccount
gh variable set TFSTATE_RG --repo $Repo --body $StateRg

@{ stateAccount = $stateAccount; clientId = $id.clientId; tenant = $tenant } | ConvertTo-Json | Set-Content "$PSScriptRoot/bootstrap.local.json"
Write-Host "Klaar. Backend: storage_account_name=$stateAccount resource_group_name=$StateRg container=tfstate key=sociale-kaart-rag.tfstate"
