# Sociale-kaart RAG — plan 5: categorie als boost i.p.v. filter (follow-up 22)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Eval-case g04 ("wat is de ZoetermeerPas en voor wie is die?") structureel groen krijgen door
de foute aanname weg te nemen dat een fragment in precies één domein valt en dat de domein-gok van
het classificatiemodel mag bepalen wát de retrieval mág zien. Een fragment krijgt een geordende set
categorieën; de categorie uit de intent wordt een scoring-boost in plaats van een hard filter.

**Architecture:** `Taxonomy` levert `AllFromOsm` / `AllFromSamenwerkendeCatalogi` (geordende set,
eerste = primair); `Chunk`/`SocialMapRecord` dragen `Category` (primair) én `Categories`. De index
krijgt onder de nieuwe naam `social-map-v2` een veld `categories` (`Collection(Edm.String)`) en een
scoring profile `category-boost` (`TagScoringFunction`, parameter `cat`, boost 2,0, interpolatie
`Constant`). `AzureSearchTool` zet dat profiel + `ScoringParameters` op de query; corpus- en
geo-filter blijven harde filters. `AskOrchestrator` verliest daarmee zijn retry-tak: één zoekcall,
één `toolCall` in de trace. `PolicyVersion` → 1.1.0.

**Besluit en alternatieven:** [ADR-0006](../../adr/0006-categorie-als-boost-niet-als-filter.md).

**Gate:** `dotnet build` + `dotnet test` groen (> 201 tests), `social-map-v2` opnieuw geïngest met
het verwachte aantal documenten, en een lokale eval-run waarin g04 groen is en alle 33 cases de
drempels halen (groundedness 8/8).

---

## Task 1: categorie als boost, meervoudige taxonomie, v2-index

**Files:**
- `src/Core/SocialMap/Taxonomy.cs` — `AllFromOsm`, `AllFromSamenwerkendeCatalogi`
- `src/Core/Chunks/Chunk.cs`, `src/Ingest/Sources/SocialMapRecord.cs` — `Categories`
- `src/Core/Retrieval/SearchIndexes.cs` — `SocialMapCorpus`/`SocialMap` (v2), veld `categories`, scoring profile
- `src/Core/Retrieval/AzureSearchTool.cs` — `ScoringProfile`/`ScoringParameters`, `BuildFilter` zonder categorie
- `src/Core/AskOrchestrator.cs`, `src/Core/Policy/PolicyVersion.cs` — retry weg, 1.1.0
- `src/Ingest/*`, `src/Eval/*`, `src/Api/Program.cs` — indexnaam vs. corpusnaam
- `docs/adr/0006-…`, `README.md`, `docs/superpowers/plans/followups-na-plan-1.md`

**Steps:**

- [x] **Step 1: Taxonomie meervoudig.** `AllFromOsm` en `AllFromSamenwerkendeCatalogi` geven een
  geordende set; de enkelvoudige methoden worden `All…().FirstOrDefault()`, zodat de primaire
  categorie per bedoeling ongewijzigd blijft. Het SC-onderwerp is nu een extra categorie in plaats
  van alleen fallback. `Chunk.Categories`/`SocialMapRecord.Categories` reizen mee door chunker,
  SC-bron, OSM-bron en de trefwoord-fallback in de ingest. Tests: ZoetermeerPas-achtige tekst
  ("voordeelpas … meedoen") → `[werk_inkomen, welzijn]`, verpleeghuis → `[wonen, gezondheid]`,
  primaire = eerste; alle bestaande taxonomie-tests blijven ongewijzigd groen.

- [x] **Step 2: Index v2 met boost.** Veld `categories` (`Collection(Edm.String)`, filterable +
  facetable) naast het bestaande `category`; scoring profile `category-boost` met
  `TagScoringFunction("categories", 2.0, new TagScoringParameters("cat"))` en interpolatie
  `Constant` (één categorie per query — er valt niets te interpoleren; zie ADR-0006). Indexnaam
  `social-map-v2`; de logische corpusnaam blijft `social-map` in een eigen constante, zodat het
  harde `corpus eq '…'`-filter niet met de indexversie meebeweegt. `index-create` maakt v2 aan
  zonder de oude index te raken. `SearchDocumentDto` vult `categories` terug naar `[category]` als
  een chunk (canary, oudere bron) alleen een primaire draagt.

- [x] **Step 3: Query boost i.p.v. filter.** `AzureSearchTool` zet `ScoringProfile` +
  `ScoringParameters = ["cat-<categorie>"]`; `BuildFilter` verliest het category-argument en houdt
  corpus + geo als harde filters. In een codecommentaar vastgelegd dat het profiel op de
  BM25-tak van de hybride query werkt en RRF daarna fuseert — acceptabel, want de boost kan een
  document nooit uitsluiten. De API-versie accepteert het profiel op een hybride query.

- [x] **Step 4: Orchestrator vereenvoudigd.** Retry-tak verwijderd: één zoekcall, één `toolCall` in
  de trace, escalatie ongewijzigd op `EscalationScoreThreshold`. `PolicyVersion.Current` → 1.1.0;
  de versiestring is nagelopen in `TraceRecordTests`, de Api-tests (literalen, ongewijzigd) en
  `docs/eval-report.md` (door de workflow bijgehouden, niet met de hand aangepast).

- [x] **Step 5: Herindexeren in v2.** `index-create` + `ingest-social-map` lokaal gedraaid met
  dezelfde `Azure__*`-variabelen als `.github/workflows/ingest.yml` gebruikt, en via de Search-REST
  geverifieerd op documentaantal en op de `categories` van het ZoetermeerPas-document.

- [x] **Step 6: Lokale eval-run als bewijs.** `dotnet run --project src/Eval -c Release` tegen de
  code op deze branch; acceptatie g04 groen én alle drempels gehaald. `docs/eval-report.md` is
  bewust **niet** meegecommit (die houdt de workflow bij).

- [x] **Step 7: Docs.** ADR-0006, README (ADR-lijst + retrieval-tekst), follow-up 22 afgevinkt en
  follow-up 24 toegevoegd (oude index `social-map` verwijderen na een bewezen v2), dit planbestand.
