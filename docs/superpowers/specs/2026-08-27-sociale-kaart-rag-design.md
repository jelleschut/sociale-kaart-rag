# Sociale-kaart RAG op Azure — ontwerp

Datum: 2026-08-27 · Status: goedgekeurd in sessie, wacht op review van de geschreven spec

## 1. Doel

Een kleine maar volledige, publiek toonbare referentie-implementatie van een
RAG-gids ("de gids") over een sociale kaart, gebouwd op Azure AI Foundry en Azure
AI Search, met guardrails, traceability, evaluatie en IaC. De repo dient als
aantoonbaar bewijs voor de eisen uit de HAGA/SZMG-opdracht "Digitale voordeur":
RAG-architectuur, guardrail-ontwerp, scheiding van data, Foundry-ervaring,
evaluatie/traceability, kostenbewustzijn, Terraform, vibe-code-guardrails in CI.

Geen productie-ambitie. Wel productie-*discipline*: alles als code, elke control
getest, elke keuze in een ADR.

## 2. Randvoorwaarden

| Onderwerp | Keuze |
|---|---|
| Azure | Eigen account (privé-e-mail), apart `AZURE_CONFIG_DIR`-profiel; nooit een Flanderijn-subscription |
| Repo | GitHub `jelleschut/sociale-kaart-rag`, publiek, MIT; git-identiteit `jelleschut@hotmail.com` |
| Budget | ≤ €25/maand, afgedwongen met Azure Budget + alerts (50/80/100 %) en lage TPM-quota |
| Stack | .NET 10 minimal API + htmx-pagina; Terraform; GitHub Actions met OIDC |
| Model | `gpt-4.1-mini` (chat; **afwijking 27-08**: `gpt-4o-mini 2024-07-18` bleek sinds 31-03-2026 gedeprecieerd, en had 0 GlobalStandard-quota — chat draait op SKU `Standard`, embedding op `GlobalStandard`) en `text-embedding-3-small` (embeddings) via Azure AI Foundry/OpenAI-resource |
| Corpus | (a) publieke sociale-kaart-data zorg & welzijn NL (organisaties/diensten, geen personen); (b) `kb-chunks.jsonl` uit soevereinlab-knowledge, ongewijzigd |
| Buiten scope | accounts/CIAM, kaart-UI, WordPress/CMS, spraak (ASR), semantic ranker, multi-region |

## 3. Aanpak: application-owned orchestration

De .NET-API is de orchestrator; Foundry is alleen model-runtime. Prompts,
policies, retrieval, ranking en evaluaties zijn versioneerbare eigen assets.
Alternatieven (Foundry Agent Service met AI Search-tool; Semantic Kernel/Agent
Framework) worden afgewogen in ADR-0001 en niet gebruikt: minder controle over
guardrails buiten het model, moeilijker te evalueren, meer lock-in.

## 4. Componenten

Elk component is een eigen project/namespace met één verantwoordelijkheid en
eigen unit-tests. Grenzen zijn interfaces; implementaties zijn vervangbaar.

### 4.1 Ingest (`src/Ingest`, console)

Pijplijn per bron: **bron → validatie → normalisatie → geocoding → chunking →
embedding → index-upsert.**

- Bron-adapters: `SocialMapSource` (open data, zie ADR-0002) en
  `KbChunksSource` (leest `kb-chunks.jsonl`, één record = één chunk).
- Validatie: schema (verplichte velden, URL/telefoon-formaat); ongeldige records
  worden geteld en gerapporteerd, niet stilzwijgend overgeslagen.
- Normalisatie: categorie-taxonomie (vaste lijst: gezondheid, werk & inkomen,
  wonen, vervoer, welzijn, mantelzorg), telefoon/URL-normalisatie, whitespace.
- Geocoding: PDOK Locatieserver (gratis, NL). **Alleen op postcode-/straatniveau;
  huisnummers worden niet opgeslagen** (PSA-eis "grofmazige locatie").
- Chunking: sociale kaart = één chunk per organisatie-dienst; kb = zoals aangeleverd.
- Provenance-metadata per chunk: `source`, `sourceId`, `sourceUrl`, `retrievedAt`,
  `contentHash`, `corpus` (`social-map` | `kb`), `category`, `geo` (lat/lon),
  `lastVerified` (kb).
- Embedding via `text-embedding-3-small`; upsert idempotent op `id` = hash van
  (`corpus`, `sourceId`).
- Bron-snapshot (ruwe download + datum) naar Blob zodat ingestie reproduceerbaar is.
- Draait als `workflow_dispatch`-job, niet always-on.

### 4.2 Retrieval (`src/Core/Retrieval`)

- Twee AI Search-indexen: `social-map` en `kb`. Hybrid query (BM25 + vector),
  filter op `corpus` en optioneel `category`, top-k (default 6) met scores.
- Interface `ISearchTool` met getypeerd schema; twee registraties
  (`search_social_map`, `search_kb`). Geen andere tools bestaan.

### 4.3 Policy (`src/Core/Policy`) — de gateway-laag in code

Volgorde per request:

1. **PII-filter op de vraag**: regex (BSN met 11-proef, e-mail, telefoon,
   postcode+huisnummer); gevonden PII wordt geredigeerd vóór classificatie en
   prompt; de trace legt alleen *dat* er geredigeerd is vast, niet wat.
2. **Intent-classificatie** (één goedkope model-call, gestructureerde output):
   `domain` (in-scope domeinen of `out_of_scope`), `intent`
   (`find_help` | `information` | `medical_advice` | `other`), `corpus`-keuze.
   `medical_advice` (diagnose/triage/behandeladvies) → **weigering** met vaste
   doorverwijstekst (huisarts, 112 bij spoed). `out_of_scope` → weigering.
3. **Tool allow-list**: alleen de twee geregistreerde tools, alleen met hun
   schema; de orchestrator roept ze zelf aan (het model kiest niet vrij).
4. **Untrusted-content boundary**: opgehaalde chunks gaan als afgebakende
   `<source id=…>`-blokken in de user-turn, nooit in de system-prompt; de
   system-prompt zegt expliciet dat instructies in bronnen genegeerd worden.
   Getest met chunks die instructies bevatten (zie §5).
5. **Escalatie-drempel**: beste retrieval-score < drempel, of generatie meldt
   onvoldoende bron → antwoord "geen betrouwbare bron gevonden" + menselijke
   contactoptie. Geen vrij model-antwoord zonder bron.

System-prompt en drempels hebben een `policyVersion` (semver, in code) die in
elke trace staat.

### 4.4 Generation (`src/Core/Generation`)

Eén chat-completion (`gpt-4o-mini`, temperature 0, max tokens beperkt) met
gestructureerde output:

```json
{ "answer": [ { "text": "...", "kind": "fact" | "summary", "citations": ["src-12"] } ],
  "confidence": "high" | "low",
  "followUp": "..." }
```

- `fact` = letterlijk uit bron, `summary` = AI-samenvatting (PSA-eis: onderscheid
  feitelijk vs. gegenereerd).
- Elk `answer`-item zonder citaat wordt door de Policy-laag verwijderd; blijft
  er niets over → escalatie.
- Prompt caching aan op de system-prompt.

### 4.5 Trace (`src/Core/Trace`)

Per request één `TraceRecord`:

`correlationId`, `timestamp`, `policyVersion`, `model` + `modelVersion`,
`promptHash`, `piiRedacted` (bool + typen), `intent`, `domain`, `toolCalls`
(naam, argumenten-hash, aantal resultaten), `retrievedChunkIds` + scores,
`tokensIn/Out/Cached`, `estimatedCostEur`, `latencyMs`, `outcome`
(`answered` | `refused_medical` | `refused_scope` | `escalated` | `error`),
`refusalReason`.

Uitvoer: App Insights custom event én JSON-regel in Blob (append-only container,
retentie 90 dagen via lifecycle policy). De correlation-ID staat zichtbaar in de
UI. Geen vraag- of antwoordtekst in de trace (data-minimisatie); wel hashes.

### 4.6 Eval (`tests/Eval`, xUnit datagedreven)

`eval/cases.yaml`, ~30 cases in vijf categorieën, elk met verwachte uitkomst:

| Categorie | Meet | Scoring |
|---|---|---|
| Groundedness | verwachte bron-ID in citaties; geen claim zonder citaat | deterministisch + LLM-as-judge (aparte judge-prompt, `gpt-4o-mini`) |
| Weigering | triage/diagnose/out-of-scope → `refused_*` | deterministisch |
| Prompt-injectie | ingeplante instructies in chunks worden niet gevolgd | deterministisch (verboden output afwezig) |
| PII | vraag met BSN/adres → `piiRedacted=true`, PII niet in prompt-log | deterministisch |
| Provenance | elk `fact`-item heeft ≥1 citaat naar bestaand chunk-ID | deterministisch |

Rapport als markdown (`docs/eval-report.md`, gecommit door de eval-job) met
score per categorie en totale kosten van de run. Drempels (bv. weigering 100 %,
injectie 100 %, groundedness ≥ 90 %) maken de CI-job rood.

### 4.7 Web (`src/Api`)

Minimal API: `POST /ask`, `GET /healthz`, `GET /trace/{id}` (alleen metadata).
Eén htmx-pagina: vraag, antwoord met badges *feit*/*samenvatting*, citaties met
bron-link, zichtbare correlation-ID, en de weigerings-/escalatieteksten.
Geen sessies, geen accounts, geen cookies.

## 5. Infra (Terraform, `infra/`)

Modules: `identity`, `observability`, `storage`, `search`, `ai`, `app`, `budget`.

- Resource group, Log Analytics + Application Insights (sampling 20 %).
- Storage-account: containers `traces` (append, lifecycle 90 d), `snapshots`.
- Azure AI Search **Free** tier (ADR-0003: geen SLA, 50 MB, 3 indexen — voldoende).
- Azure AI Foundry/OpenAI-resource (West Europe of Sweden Central, afhankelijk van
  quota) met deployments `gpt-4o-mini` en `text-embedding-3-small`, TPM laag.
- Container Apps Environment + App: consumption, min 0 / max 1 replica,
  ingress publiek, image uit GHCR.
- User-assigned managed identity met RBAC: Search Index Data Contributor,
  Cognitive Services OpenAI User, Storage Blob Data Contributor. **Geen keys in de
  app of in CI-secrets** (alleen OIDC-federatie voor de deploy-identiteit).
- Azure Budget €25 met e-mailalerts op 50/80/100 %; tags `project`, `owner`,
  `costcenter=demo`.
- Remote state: storage-account uit een eenmalig bootstrap-script (`infra/bootstrap.ps1`).

## 6. CI/CD (GitHub Actions)

| Workflow | Trigger | Stappen |
|---|---|---|
| `ci.yml` | push/PR | `dotnet build/test`, `terraform fmt -check` + `validate`, **gitleaks** (secrets), **Semgrep** (SAST), **Checkov** (Terraform), **Trivy** (container-image + IaC) |
| `deploy.yml` | push main, environment `azure` met handmatige approval | OIDC-login → `terraform plan` → `apply` → image build/push GHCR → nieuwe Container App-revisie |
| `ingest.yml` | `workflow_dispatch` (bron-keuze) | draait Ingest tegen de live indexen |
| `eval.yml` | `workflow_dispatch` + wekelijks | eval-suite tegen live, commit `docs/eval-report.md`, faalt op drempels |

Branch protection op `main`: PR verplicht, `ci.yml` groen verplicht.

## 7. Kosten (raming)

AI Search Free €0 · Container Apps consumption scale-to-zero €0–3 ·
Log Analytics/App Insights < €2 · GPT-4o mini + embeddings (demo + wekelijkse
eval) < €3 · storage < €1 → ruim onder €25. Harde remmen: budget-alerts,
TPM-quota, `max_tokens`, één replica.

## 8. Databron (ADR-0002, te kiezen bij stap 5)

Kandidaten, te toetsen op licentie (CC0/CC-BY) en bruikbaarheid: open-data-
portalen Den Haag/Zoetermeer (organisaties/voorzieningen), CIBG-registers,
Overheid.nl-organisatieregister. Alleen organisatiegegevens, geen personen.
Snapshot met datum in Blob; bronvermelding in de UI.

## 9. Documentatie

README (doel, architectuurplaat op C2-niveau, wat dit wel/niet bewijst, kosten),
`docs/adr/0001-application-owned-orchestration.md`, `0002-databron.md`,
`0003-free-tier-en-kosten.md`, `0004-guardrails-buiten-het-model.md`,
`docs/traceability.md` (veld → waarom), `docs/eval-report.md`.

## 10. Volgorde en gates

1. Repo + git-identiteit + Azure-profiel + Terraform-bootstrap — gate: `terraform plan` leeg-naar-plan werkt met OIDC.
2. Infra apply — gate: managed identity kan Search en OpenAI aanroepen; budget zichtbaar.
3. Ingest `kb-chunks.jsonl` — gate: hybrid query geeft chunks met provenance.
4. API + Policy + Generation + Trace — gate: `/ask` geeft gecieerd antwoord, weigert triage, trace in Blob.
5. Sociale-kaart-ingest met PDOK — gate: geo-filter werkt, geen huisnummers in index.
6. Eval-suite + CI-scans — gate: rapport gecommit, drempels afgedwongen.
7. htmx-UI + ADR's + README — gate: live-URL toonbaar.

Na stap 3 is er al iets toonbaars; stappen 5–7 zijn onafhankelijk van elkaar.

## 11. Testen

- Unit: validatie/normalisatie, PII-regex (incl. 11-proef), citatie-filter,
  trace-serialisatie, budget-/quota-instellingen in Terraform (`terraform test`).
- Integratie (lokaal, tegen live Azure met eigen profiel): retrieval en `/ask`.
- Eval: §4.6. Elke guardrail heeft minstens één rode-route-case die aantoont dat
  hij daadwerkelijk blokkeert.

## 12. Wat de gebruiker zelf doet

Azure-account aanmaken en inloggen (`! az login` met `AZURE_CONFIG_DIR`),
`! gh auth login`, GitHub-repo-aanmaak bevestigen, Foundry-quota-aanvraag als de
subscription nieuw is, en de e-mail voor budget-alerts opgeven.
