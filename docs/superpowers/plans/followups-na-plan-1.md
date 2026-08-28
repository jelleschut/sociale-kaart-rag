# Follow-ups na plan 1 (eindreview 27-08-2026)

Uit de afsluitende review over de hele plan-1-implementatie. Geen blockers; op te pakken in plan 2–4.

| # | Onderwerp | Waarom | Waar | Plan |
|---|---|---|---|---|
| 1 | `modelVersion` als apart trace-veld (spec §4.5) | nu alleen `Model` uit de SDK-respons | `src/Core/Trace/TraceRecord.cs`, `AskOrchestrator` | 4 (traceability.md) |
| 2 | Tokens/kosten van de intent-classificatie én de retrieval-embedding meetellen | `estimatedCostEur` onderschat elk request; ook weigeringen kosten een classificatie-call | `OpenAiIntentClassifier` (Usage doorgeven), `AzureSearchTool`, `AskOrchestrator` | 3 (eval rapporteert kosten) |
| 3 | `SourceUrl` en `Id` escapen in `Prompts.BuildUserTurn` (één escaper voor alle attributen) | vóór externe open-data-URL's de untrusted-content-boundary binnenkomen | `src/Core/Generation/Prompts.cs` | 2 (vóór PDOK-ingest) |
| 4 | App-identity: `Search Service Contributor` weg (alleen `Search Index Data Contributor`); indexbeheer hoort bij de ingest-identity | least privilege | `infra/identity.tf` | 2 |
| 5 | Expliciete `UseExceptionHandler` met generiek problem-details-antwoord + test | "geen lek bij fouten" nu alleen impliciet via framework-default | `src/Api/Program.cs` | 4 |
| 6 | App Insights-sink: ook `piiTypes`, `promptHash`, `toolCalls`, chunk-ids als properties | pariteit met Blob-sink voor KQL | `AppInsightsTraceSink.cs` | 4 |
| 7 | `Embedder`: ook retry op 5xx/timeouts | robuustere ingest | `src/Ingest/Embedder.cs` | 2 |
| 8 | semgrep-container op digest pinnen | consistent met SHA-pinning | `.github/workflows/ci.yml` | 4 |
| 9 | kb-POC-corpus verwijderen (index `kb`, `ingest-kb`, `platform_kennis`-intent, spec §2) | besluit 27-08 | overal | na 2 |

## Na plan 2 (eindreview 28-08-2026)

| # | Onderwerp | Waarom | Waar | Plan |
|---|---|---|---|---|
| 10 | **Beleidsvraag**: OSM-`name` van eenmanspraktijken ("Huisartsenpraktijk J. de Vries") is mogelijk een persoonsgegeven; nu ongefilterd geïndexeerd | AVG; spec "organisaties, geen personen" | `OsmOverpassSource.cs` (Name), ADR-0002 Gevolgen | besluit Jelle; daarna ADR-0002 aanvullen (risico-acceptatie óf generalisatie "Huisartsenpraktijk (naam verwijderd)") |
| 11 | OSM `phone`/`contact:phone` niet door `RemovePersonalContacts` (mobiel nummer van een eenmanszaak) | consistent PII-beleid | `OsmOverpassSource.cs` | 3 |
| 12 | Zoetermeer-paginatekst (niet-commercieel + bronvermelding) en SC-metadata (CC0) zitten in één `attribution:`-string | twee licentiegronden apart benoemen | `SocialMapChunker`, `Program.cs` ingest | 4 |
| 13 | Stale kb-verwijzingen: spec §10 gate 3, ADR-0003 "~400 kb-chunks" | docs-hygiëne | docs | 4 |
| 14 | `PostcodeDetector` accepteert SA/SD/SS-letterparen (PostNL kent ze niet) | precisie | `Geocoding.cs` | 4 |
| 15 | Eval-cases voor geo-filter (DH-postcode → DH-hits, ZM → ZM, SC altijd) en voor praktijknamen | regressiebescherming | eval | 3 |
| 16 | UI toont `sources[].attribution` zichtbaar (ODbL/CC0) | licentieplicht in de UI | htmx-pagina | 4 |
