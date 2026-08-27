# sociale-kaart-rag

Kleine, publiek toonbare referentie-implementatie van een RAG-gids over een
sociale kaart op Azure AI Foundry + Azure AI Search, met guardrails buiten het
model, traceability per request, evaluatie en Terraform-IaC. Geen productie-
ambitie, wel productie-discipline.

- Ontwerp: [`docs/superpowers/specs/2026-08-27-sociale-kaart-rag-design.md`](docs/superpowers/specs/2026-08-27-sociale-kaart-rag-design.md)
- Plan 1 (fundament tot `/ask`): [`docs/superpowers/plans/2026-08-27-fundament-tot-ask.md`](docs/superpowers/plans/2026-08-27-fundament-tot-ask.md)
- Budget: ≤ €25/maand, afgedwongen met Azure Budget-alerts en lage TPM-quota.

Status (27-08-2026): **plan 1 klaar** — infra live in Sweden Central (`rg-skr-9asax`), `kb`-index gevuld
met 388 POC-chunks, `POST /ask` geeft gecieerde antwoorden, weigert medisch advies en out-of-scope,
redigeert PII vóór retrieval en schrijft per request een trace (Blob + App Insights) zonder vraag- of
antwoordtekst. CI: build/test, Terraform-checks, gitleaks, semgrep, checkov, trivy; deploy via OIDC.
Volgende: sociale-kaart-ingest met PDOK (plan 2), eval-suite (plan 3), htmx-UI + ADR's (plan 4).
Het soevereinlab-kb-corpus is tijdelijk (POC) en verdwijnt na plan 2.
