# sociale-kaart-rag

Kleine, publiek toonbare referentie-implementatie van een RAG-gids over een
sociale kaart op Azure AI Foundry + Azure AI Search, met guardrails buiten het
model, traceability per request, evaluatie en Terraform-IaC. Geen productie-
ambitie, wel productie-discipline.

- Ontwerp: [`docs/superpowers/specs/2026-08-27-sociale-kaart-rag-design.md`](docs/superpowers/specs/2026-08-27-sociale-kaart-rag-design.md)
- Plan 1 (fundament tot `/ask`): [`docs/superpowers/plans/2026-08-27-fundament-tot-ask.md`](docs/superpowers/plans/2026-08-27-fundament-tot-ask.md)
- Budget: ≤ €25/maand, afgedwongen met Azure Budget-alerts en lage TPM-quota.

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

