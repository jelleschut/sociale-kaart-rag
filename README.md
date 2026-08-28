# sociale-kaart-rag

Kleine, publiek toonbare referentie-implementatie van een RAG-gids over een
sociale kaart op Azure AI Foundry + Azure AI Search, met guardrails buiten het
model, traceability per request, evaluatie en Terraform-IaC. Geen productie-
ambitie, wel productie-discipline.

- Ontwerp: [`docs/superpowers/specs/2026-08-27-sociale-kaart-rag-design.md`](docs/superpowers/specs/2026-08-27-sociale-kaart-rag-design.md)
- Plan 1 (fundament tot `/ask`): [`docs/superpowers/plans/2026-08-27-fundament-tot-ask.md`](docs/superpowers/plans/2026-08-27-fundament-tot-ask.md)
- Budget: ≤ €25/maand, afgedwongen met Azure Budget-alerts en lage TPM-quota.

Status (28-08-2026): **plan 3 klaar** — eval-suite met 33 cases in vijf categorieën, alle drempels gehaald (zie docs/eval-report.md).

Status (28-08-2026): **plan 2 klaar** — de `social-map`-index bevat 794 chunks: 176 gemeentelijke regelingen
(Samenwerkende Catalogi, CC0; Zoetermeer met volledige paginatekst) en 618 zorg-/welzijnslocaties uit OpenStreetMap
voor Den Haag en Zoetermeer, met categorie, grofmazige geo (geen huisnummers) en attributie per bron. `/ask` filtert
op categorie (uit de intent) en op afstand tot een postcode in de vraag (PDOK).

Status (27-08-2026): **plan 1 klaar** — infra live in Sweden Central (`rg-skr-9asax`), tijdelijk
POC-corpus (inmiddels verwijderd), `POST /ask` geeft gecieerde antwoorden, weigert medisch advies en out-of-scope,
redigeert PII vóór retrieval en schrijft per request een trace (Blob + App Insights) zonder vraag- of
antwoordtekst. CI: build/test, Terraform-checks, gitleaks, semgrep, checkov, trivy; deploy via OIDC.
Volgende: sociale-kaart-ingest met PDOK (plan 2), eval-suite (plan 3), htmx-UI + ADR's (plan 4).
Het soevereinlab-kb-corpus was tijdelijk (POC) en is verwijderd (plan 2 Task 9).

## Bronnen en attributie

| Bron | Gebruik | Licentie / voorwaarde |
|---|---|---|
| [Samenwerkende Catalogi](https://data.overheid.nl/dataset/samenwerkende-catalogi-producten-en-diensten) (overheid.nl/KOOP) | producten en diensten van gemeente Den Haag en Zoetermeer: titel, samenvatting, link | CC0 1.0 — attributie: "Bron: gemeente <naam> via Samenwerkende Catalogi (overheid.nl, CC0)" |
| [zoetermeer.nl](https://www.zoetermeer.nl/c-zoetermeer) | volledige tekst van de gelinkte productpagina's | niet-commercieel hergebruik met bronvermelding (gemeente Zoetermeer) |
| denhaag.nl | **niet opgehaald** — gebruiksvoorwaarden geven geen hergebruiksrecht (ADR-0002) | — |
| [OpenStreetMap](https://www.openstreetmap.org/copyright) | wijkcentra, sociale voorzieningen, zorglocaties met adres, telefoon, website, openingstijden | © OpenStreetMap-bijdragers, [ODbL 1.0](https://opendatacommons.org/licenses/odbl/) |
| [PDOK Locatieserver](https://www.pdok.nl/) | postcode/gemeente → coördinaat | CC0 |

Ingest: `dotnet run --project src/Ingest -- ingest-social-map` (of de workflow `ingest` met bron `social-map`).

## Evaluatie

Een reproduceerbare eval (spec §4.6, ADR-0005) draait 33 cases in-process tegen de echte
`AskOrchestrator` en scoort ze deterministisch, op één categorie na (groundedness) waar een
LLM-as-judge beoordeelt of elke claim door de geciteerde bronnen wordt gedekt.

| Categorie | Cases | Drempel |
|---|---|---|
| groundedness | 8 | ≥ 90 % |
| refusal | 7 | 100 % |
| injection | 5 | 100 % |
| pii | 6 | 100 % |
| provenance | 7 | 100 % |

Lokaal draaien (zelfde `Azure__*`-omgevingsvariabelen als de ingest):

```
dotnet run --project src/Eval -c Release
```

Het rapport met score per categorie en kosten staat in
[`docs/eval-report.md`](docs/eval-report.md). De workflow `eval.yml` draait wekelijks
(maandag 05:17 UTC) en op `workflow_dispatch`, en levert het rapport af als een PR op branch
`eval/report` (main is protected) — zie ADR-0005 voor de `GITHUB_TOKEN`-beperking daarbij.

