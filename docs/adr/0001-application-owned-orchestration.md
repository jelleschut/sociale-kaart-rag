# ADR-0001: Application-owned orchestration

Datum: 2026-08-27 (ontwerp), vastgelegd 2026-08-28 · Status: geaccepteerd

## Context

De gids moet aantoonbaar voldoen aan eisen die buiten het taalmodel liggen: PII-redactie vóór
het model, een harde weigering bij medische vragen, een tool-allow-list, een grens tussen
vertrouwde instructies en opgehaalde (onvertrouwde) bronnen, escalatie zonder bron, en een trace
per request die evalueerbaar is (spec §4.3–§4.6). De vraag is wie de orkestratie bezit: onze
applicatie, of een agent-runtime van de leverancier.

## Overwogen opties

| | A. Application-owned (.NET-API orkestreert; Foundry/OpenAI alleen model-runtime) | B. Azure AI Foundry Agent Service met AI Search-tool | C. Semantic Kernel / Agent Framework |
|---|---|---|---|
| Guardrails buiten het model | Elke stap is eigen, geteste code (`AskOrchestrator`): PII → intent → tool → generatie → citatiefilter | Tool-keuze en tool-aanroep liggen bij de agent; PII-redactie en weigering moeten in de prompt of in een pre-hook | Mogelijk via filters/planners, maar de flow zit in frameworkabstracties |
| Evalueerbaarheid / traceability | Elk veld in `TraceRecord` komt uit een eigen regel code; canaries en drempels meten precies die stappen | Threads/runs zijn observeerbaar, maar de interne beslissingen (welke tool, waarom) zijn een zwarte doos | Telemetrie beschikbaar, maar gekoppeld aan frameworkversies |
| Lock-in | Laag: `ISearchTool`, `IAnswerGenerator`, `IIntentClassifier` zijn vervangbaar | Hoog: agent-definities, tool-schema's en threads zijn Foundry-specifiek | Middel: framework-API's veranderen snel |
| Kosten | Alleen model-calls (≈ € 0,001 per vraag gemeten); geen extra runtime | Extra runtime; onvoorspelbare tool-iteraties | Geen extra runtime, wel extra tokens door planner-loops |
| Hoeveelheid eigen code | Meer (orchestrator, prompts, schema's) | Minder | Middel |

## Besluit

Optie **A**. De applicatie is de orchestrator; Azure OpenAI (via AI Foundry) is uitsluitend
model-runtime voor twee gestructureerde calls (intent-classificatie en generatie) plus embeddings.
Het model kiest nooit een tool: de orchestrator roept de enige geregistreerde `ISearchTool` zelf
aan met een JSON-schema-gebonden intent als stuurinformatie (ADR-0004).

## Gevolgen

- Meer eigen code, maar elke policy-stap heeft unit-tests (128 in Core) en een eval-categorie
  (ADR-0005) — dat is precies het bewijs dat de opdracht vraagt.
- Prompts, schema's en drempels zijn versioneerbare assets (`PolicyVersion`), zichtbaar in elke
  trace.
- Vervangen van model of zoekmachine raakt één interface; vervangen van de orkestratie-aanpak
  zou een herontwerp zijn — dat is de bewuste keuze.
- Wat we niet krijgen: multi-step agent-gedrag (tool-loops). Voor een gids over een sociale kaart
  is één retrieval-stap voldoende; als dat verandert, is een tweede `ISearchTool` of een expliciete
  tweede stap in de orchestrator de route, niet een vrije agent-loop.
