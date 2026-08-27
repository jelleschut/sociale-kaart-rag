# ADR-0003: Free-tier, consumption en bewuste security-scan-afwijkingen

Datum: 2026-08-27 · Status: geaccepteerd (wordt aangevuld in plan 4)

## Context
Demo-/referentie-implementatie met een hard budget van ≤ €25/maand (spec §2, §7).
Productie-hardening die geld kost of geen functie heeft in een publiek demo-scenario
wordt bewust niet gedaan; elke afwijking staat hier met reden.

## Besluit
- Azure AI Search **Free** (geen SLA, 50 MB, 3 indexen) — voldoende voor ~400 kb-chunks + sociale kaart.
- Container Apps **consumption**, 0–1 replica, publieke ingress zonder WAF/private endpoint.
- Storage LRS, Microsoft-managed keys, geen private endpoints; keys uitgeschakeld (alleen RBAC).
- Log Analytics dagquotum 0,5 GB (harde ingest-cap, geen kosten-cap).
- Terraform als per-concern-bestanden in één root i.p.v. submodules (leesbaarheid boven herbruikbaarheid).

## Checkov-skips in `ci.yml` en waarom
| Check | Betekenis | Waarom overgeslagen |
|---|---|---|
| CKV_AZURE_59 | Storage: publieke netwerktoegang uit | Free/consumption-demo zonder private endpoints; keys staan uit, RBAC-only |
| CKV_AZURE_33 | Storage: queue-logging | Geen queues in gebruik |
| CKV_AZURE_206 | Storage: replicatie ≥ GRS/ZRS | LRS volstaat voor demo; traces zijn 90 d, snapshots reproduceerbaar |
| CKV2_AZURE_1 | Storage: customer-managed key | Geen CMK-eis in demo; MMK |
| CKV2_AZURE_33 | Storage: private endpoint | Zie CKV_AZURE_59 |
| CKV2_AZURE_40 | Storage: shared key uit | Staat al uit (`shared_access_key_enabled = false`); check triggert op oudere provider-semantiek |
| CKV_AZURE_43 | Storage: naamgevingsregel | Naam `st<prefix><suffix>` voldoet; false positive op interpolatie |
| CKV_AZURE_124 | Search: publieke netwerktoegang uit | Free-tier-demo; geen private endpoint; auth via Entra ID/RBAC |
| CKV_AZURE_134 | OpenAI: publieke netwerktoegang uit | Idem; Container App consumption heeft geen VNet-integratie in dit ontwerp |
| CKV_AZURE_207 | Search: managed identity | Free-tier ondersteunt geen managed identity; Search hoeft zelf geen Azure-resources te benaderen (geen indexers) |
| CKV_AZURE_208 | Search: SLA index-updates (≥2 replica's) | Free-tier heeft geen SLA (bewust, spec §5) |
| CKV_AZURE_209 | Search: SLA queries (≥3 replica's) | Idem |
| CKV2_AZURE_21 | Storage: blob-logging voor reads | Traces zijn zelf de audit-trail; extra diagnostics kost Log Analytics-ingest |
| CKV2_AZURE_22 | OpenAI: customer-managed key | Geen CMK-eis in demo; MMK |

Als een skip niet meer nodig blijkt (check slaagt), wordt hij verwijderd. Nieuwe skips
komen alleen met een rij in deze tabel.

## Gevolgen
Geen SLA, geen geo-redundantie, publieke ingress. Acceptabel voor het doel: aantonen van
guardrails, traceability en IaC-discipline, niet van productie-hosting.
