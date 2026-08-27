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
