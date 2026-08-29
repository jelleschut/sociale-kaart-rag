# Traceability

Elke aanroep van `POST /ask` (JSON) of `POST /ask/fragment` (pagina) levert precies één
`TraceRecord` op — ook bij weigering, escalatie of fout. Het record bevat **nooit** de vraag of
het antwoord (data-minimalisatie, spec §4.5); wel hashes, identifiers, tellingen en uitkomsten.

## Velden

| Veld | Waarom | Waar gezet |
|---|---|---|
| `correlationId` | Koppelt UI (zichtbaar onderaan elk antwoord), `X-Correlation-Id`-header, `GET /trace/{id}`, Blob en App Insights | `AskEndpoint` (GUID, 32 hex) |
| `timestamp` | Volgorde en retentie (90 dagen) | `TraceRecord.Start` |
| `policyVersion` | Welke prompt-/drempelversie het antwoord bepaalde; verhoogd bij elke wijziging (`PolicyVersion.Current`) | `TraceRecord` default |
| `model`, `modelVersion` | Reproduceerbaarheid: naam en versie (bv. `gpt-4.1-mini` / `2025-04-14`) uit de model-respons | `AskOrchestrator` (`SplitModel`) |
| `promptHash` | SHA-256 van system-prompt + user-turn: bewijst welke prompt draaide zonder de tekst op te slaan; gelijk voor gelijke bronnen → cache-diagnostiek | `OpenAiAnswerGenerator` |
| `piiRedacted`, `piiTypes` | Dát er geredigeerd is en welke typen (`bsn`, `email`, `phone`, `address`); nooit de waarden | `PiiFilter` via `AskOrchestrator` |
| `intent`, `domain` | Uitkomst van de classificatie (`find_help`/`information`/`medical_advice`/`other`; zes domeinen of `out_of_scope`) | `OpenAiIntentClassifier` |
| `toolCalls[]` (`name`, `argumentsHash`, `resultCount`) | Welke tool de orchestrator aanriep, met een hash van tekst/categorie/geo/topK en het aantal hits; sinds policyVersion 1.1.0 staat er precies één — de categorie is een boost geworden, dus er is geen retry zonder categorie meer (ADR-0006) | `AskOrchestrator` |
| `retrievedChunkIds[]`, `retrievedScores[]` | Provenance: welke chunks zijn opgehaald met welke RRF-score; elke citatie in het antwoord verwijst naar een van deze id's | `AzureSearchTool` via `AskOrchestrator` |
| `tokensIn`, `tokensOut`, `tokensCached` | Kosten en prompt-cache-effect van de generatie-call | `OpenAiAnswerGenerator` |
| `estimatedCostEur` | Raming (lijstprijs × 0,92 €/$); geen factuur | `CostEstimator` |
| `latencyMs` | Doorlooptijd end-to-end | `AskOrchestrator` |
| `outcome` | `answered` \| `refused_medical` \| `refused_scope` \| `escalated` \| `error` | `AskOrchestrator` |
| `refusalReason` | Vaste code: `medical_advice`, `out_of_scope`, `no_tool_for_corpus`, `low_retrieval_score`, `no_cited_answer` | `AskOrchestrator` |

## Bewust afwezig

- Vraagtekst, geredigeerde vraag, antwoordtekst, brontekst — alleen hashes en id's.
- IP-adres, user-agent, cookies of sessie — de API kent geen gebruikers.

## Waar traces landen

| Doel | Locatie | Retentie |
|---|---|---|
| Append-only audit | Blob `traces/yyyy/MM/dd.jsonl` (één JSON-regel per request) | 90 dagen (lifecycle-policy in Terraform) |
| Opzoeken per id | Blob `traces/by-id/<correlationId>.json` → `GET /trace/{id}` | 90 dagen |
| Dashboards/alerts | App Insights custom event `rag.request` (properties) + metrics `rag.estimatedCostEur`, `rag.latencyMs`, `rag.tokensIn`, `rag.tokensOut` | 30 dagen (Log Analytics), sampling 20 % op requests, events niet gesampled |

De eval-runner (ADR-0005) schrijft géén traces naar Blob of App Insights: hij gebruikt een
opvangende sink, zodat testverkeer de productie-audit niet vervuilt.

## Van UI naar trace in drie stappen

1. Onderaan elk antwoord staat `correlation-id <id>`; klik erop → `GET /trace/<id>` (JSON).
2. Dezelfde regel staat in Blob: `traces/<jaar>/<maand>/<dag>.jsonl`, filter op `"correlationId":"<id>"`.
3. In App Insights: `customEvents | where name == "rag.request" and customDimensions.correlationId == "<id>"`.
