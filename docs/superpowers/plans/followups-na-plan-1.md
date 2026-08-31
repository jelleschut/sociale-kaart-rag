# Follow-ups na plan 1 (eindreview 27-08-2026)

Uit de afsluitende review over de hele plan-1-implementatie. Geen blockers; op te pakken in plan 2–4.

| # | Onderwerp | Waarom | Waar | Plan |
|---|---|---|---|---|
| 1 | `modelVersion` als apart trace-veld (spec §4.5) | nu alleen `Model` uit de SDK-respons | `src/Core/Trace/TraceRecord.cs`, `AskOrchestrator` | 4 (traceability.md)  ✅ gedaan (plan 4 Task 2) |
| 2 | Tokens/kosten van de intent-classificatie én de retrieval-embedding meetellen | `estimatedCostEur` onderschat elk request; ook weigeringen kosten een classificatie-call | `OpenAiIntentClassifier` (Usage doorgeven), `AzureSearchTool`, `AskOrchestrator` | 3 (eval rapporteert kosten) |
| 3 | `SourceUrl` en `Id` escapen in `Prompts.BuildUserTurn` (één escaper voor alle attributen) | vóór externe open-data-URL's de untrusted-content-boundary binnenkomen | `src/Core/Generation/Prompts.cs` | 2 (vóór PDOK-ingest) |
| 4 | App-identity: `Search Service Contributor` weg (alleen `Search Index Data Contributor`); indexbeheer hoort bij de ingest-identity | least privilege | `infra/identity.tf` | 2 |
| 5 | Expliciete `UseExceptionHandler` met generiek problem-details-antwoord + test | "geen lek bij fouten" nu alleen impliciet via framework-default | `src/Api/Program.cs` | 4  ✅ gedaan (plan 4 Task 2) |
| 6 | App Insights-sink: ook `piiTypes`, `promptHash`, `toolCalls`, chunk-ids als properties | pariteit met Blob-sink voor KQL | `AppInsightsTraceSink.cs` | 4 |
| 7 | `Embedder`: ook retry op 5xx/timeouts | robuustere ingest | `src/Ingest/Embedder.cs` | 2 |
| 8 | semgrep-container op digest pinnen | consistent met SHA-pinning | `.github/workflows/ci.yml` | 4 |
| 9 | kb-POC-corpus verwijderen (index `kb`, `ingest-kb`, `platform_kennis`-intent, spec §2) | besluit 27-08 | overal | na 2 |

## Na plan 2 (eindreview 28-08-2026)

| # | Onderwerp | Waarom | Waar | Plan |
|---|---|---|---|---|
| 10 | **Beleidsvraag**: OSM-`name` van eenmanspraktijken ("Huisartsenpraktijk J. de Vries") is mogelijk een persoonsgegeven; nu ongefilterd geïndexeerd | AVG; spec "organisaties, geen personen" | `OsmOverpassSource.cs` (Name), ADR-0002 Gevolgen | besluit Jelle; daarna ADR-0002 aanvullen (risico-acceptatie óf generalisatie "Huisartsenpraktijk (naam verwijderd)") |
| 11 | OSM `phone`/`contact:phone` niet door `RemovePersonalContacts` (mobiel nummer van een eenmanszaak) | consistent PII-beleid | `OsmOverpassSource.cs` | 3  ✅ gedaan (28-08) |
| 12 | Zoetermeer-paginatekst (niet-commercieel + bronvermelding) en SC-metadata (CC0) zitten in één `attribution:`-string | twee licentiegronden apart benoemen | `SocialMapChunker`, `Program.cs` ingest | 4 |
| 13 | Stale kb-verwijzingen: spec §10 gate 3, ADR-0003 "~400 kb-chunks" | docs-hygiëne | docs | 4  ✅ gedaan (plan 4 Task 3) |
| 14 | `PostcodeDetector` accepteert SA/SD/SS-letterparen (PostNL kent ze niet) | precisie | `Geocoding.cs` | 4 |
| 15 | Eval-cases voor geo-filter (DH-postcode → DH-hits, ZM → ZM, SC altijd) en voor praktijknamen | regressiebescherming | eval | 3 |
| 16 | UI toont `sources[].attribution` zichtbaar (ODbL/CC0) | licentieplicht in de UI | htmx-pagina | 4  ✅ gedaan (plan 4 Task 1) |

## Na plan 3 (eindreview 28-08-2026)

| # | Onderwerp | Waarom | Waar | Plan |
|---|---|---|---|---|
| 17 | Rapport-PR van `eval.yml` triggert geen CI (GITHUB_TOKEN); mergen met `--admin` of een fine-grained PAT als secret | GitHub-beperking | `.github/workflows/eval.yml`, ADR-0005 | 4 |
| 18 | `contents: write`/`pull-requests: write` gelden voor de hele eval-job; opsplitsen in twee jobs met artifact-overdracht als least-privilege gewenst is | permissie-scope | `eval.yml` | later |
| 19 | Judge = zelfde model als generator (zelfbeoordeling); overweeg een tweede, onafhankelijk judge-model als het budget dat toelaat | eval-onafhankelijkheid | `src/Eval/Judge.cs`, ADR-0005 | later |
| 20 | Escalatie-cases v04–v06 accepteren ook `refused_scope`; als de classifier structureel `out_of_scope` kiest voor andere gemeenten is een eigen categorie "buiten werkgebied" zuiverder | intent-taxonomie | `IntentClassifier`, `eval/cases.yaml` | 4 |

## Na plan 4 (gate 7, 28-08-2026)

| # | Onderwerp | Waarom | Waar | Plan |
|---|---|---|---|---|
| 21 | `eval.yml` kon geen PR aanmaken ("GitHub Actions is not permitted to create or approve pull requests"): repo-instelling *Allow GitHub Actions to create and approve pull requests* aanzetten; daarna een dispatch-run als bewijs | rapport-PR-flow bewezen krijgen | repo-instellingen, README "Evaluatie" | ✅ gedaan (28-08: instelling aan, run 33170383205 opende PR #42 zelf) |
| 22 | g04 ("wat is de ZoetermeerPas") → `escalated (no_cited_answer)` in runs 33167786538 en 33170383205 (2× 88 % < 90 %). **Diagnose 28-08:** chunks staan in de index (BM25 22, categorie `werk_inkomen`), maar de classifier kiest domein `welzijn`; het categoriefilter levert alleen welzijn-subsidies met RRF ≈ 0,03 > `EscalationScoreThreshold` 0,015, dus de retry zonder categorie vuurt niet en de generatie ziet de ZoetermeerPas nooit. Opties: (a) retry zonder filter óók wanneer de generatie `no_cited_answer` geeft, (b) gefilterde en ongefilterde hits samenvoegen (RRF over beide), (c) `werk_inkomen` als tweede categorie bij `welzijn`-intent. Trace: c9891277c1314d81aa24337afc2d1c03 | eval-stabiliteit | `AskOrchestrator.cs:65-81`, `PolicyVersion.cs`, `eval/cases.yaml` | ✅ gedaan (29-08, plan 5, PR #44): gekozen is een vierde optie — meervoudige categorieën per fragment plus de categorie als tag-boost i.p.v. filter, waarmee de retry-tak verviel ([ADR-0006](../../adr/0006-categorie-als-boost-niet-als-filter.md)) |
| 23 | Operator-datarollen wisselden per apply-context (lokaal User vs CI-identity) en werden door een CI-apply vernietigd; nu `var.operator_object_id` (PR #39). Overweeg `terraform plan`-check in CI die een destroy van rolaanwijzingen blokkeert | guarded automation | `infra/identity.tf`, `ci.yml` | later |
| 24 | Oude index `social-map` verwijderen nu `social-map-v2` in gebruik is (ADR-0006); pas dóén ná een groene eval in CI op v2 en een bevestigde deploy, zodat een rollback naar policyVersion 1.0.x zonder herindexering mogelijk blijft. Free tier staat 3 indexes toe, dus er is geen haast | opruimen / geen dode index | Azure AI Search `srch-skr-9asax`, `SearchIndexes.SocialMap` | volgende |
