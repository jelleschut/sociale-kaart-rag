# ADR-0006: Categorie als boost, niet als filter — en een fragment mag er meer dan één hebben

Datum: 2026-08-29 · Status: geaccepteerd

## Context

De sociale kaart kent zes vaste domeinen (`gezondheid`, `werk_inkomen`, `wonen`, `vervoer`,
`welzijn`, `mantelzorg`). Tot en met policyVersion 1.0.1 gold: **één categorie per fragment**
(de eerste matchende regel in `Taxonomy.ScRules` won) en **de categorie uit de intent was een
hard OData-filter** (`category eq '…'`). Om de scherpe randen daarvan op te vangen zat er een
retry in `AskOrchestrator`: bij nul of zwakke hits nog eens zoeken, nu zonder categorie.

Eval-case **g04** ("wat is de ZoetermeerPas en voor wie is die?") liet zien dat die constructie
structureel faalt, niet incidenteel — twee runs achtereen (33167786538 en 33170383205) bleef
groundedness op 88 % steken, onder de drempel van 90 %. De diagnose (trace
`c9891277c1314d81aa24337afc2d1c03`):

1. De LLM-intentclassificatie noemt het domein **`welzijn`** — verdedigbaar: een stadspas om mee
   te kunnen doen is welzijn.
2. Het chunk over de ZoetermeerPas (`https://www.zoetermeer.nl/zoetermeerpas`) draagt categorie
   **`werk_inkomen`** — óók verdedigbaar: de regex `voordeelpas` staat in de `werk_inkomen`-regel,
   die bewust vóór `welzijn` komt zodat "meedoenregeling" en "toeslag" niet op het welzijn-woord
   "meedoen" blijven hangen.
3. Het harde filter sneed daarmee precies het enige relevante document weg.
4. De overgebleven welzijn-hits waren *plausibel maar irrelevant* en scoorden RRF ≈ 0,031 —
   **boven** `PolicyVersion.EscalationScoreThreshold` (0,015). De retry vuurde dus niet.
5. De generatie kon niets citeren dat de vraag beantwoordde → `escalated (no_cited_answer)`.

De fout zit niet in de classificatie en niet in de taxonomie: allebei hebben ze gelijk. De fout
zit in de aanname dat een fragment in precies één domein valt, en dat een domein-gok van het
model mag beslissen wát de retrieval überhaupt mág zien.

## Besluit

### 1. Een fragment draagt een geordende set categorieën

`Taxonomy.AllFromSamenwerkendeCatalogi` geeft **elke** matchende trefwoordregel terug, in de
bestaande vaste volgorde, gevolgd door de SC-onderwerpmapping als extra categorie (die was eerder
alleen fallback). `Taxonomy.AllFromOsm` geeft meerdere categorieën waar een voorziening
aantoonbaar op twee domeinen ligt: een verpleeghuis of begeleid wonen is `wonen` + `gezondheid`,
opvang is `wonen` + `welzijn`, dagopvang is `gezondheid` + `welzijn`, hulpverlening
(`healthcare=counselling`) is `welzijn` + `gezondheid`.

De **eerste** categorie blijft de primaire en is per bedoeling identiek aan wat v1 koos — de
enkelvoudige `FromOsm`/`FromSamenwerkendeCatalogi` zijn nu simpelweg `All…().FirstOrDefault()`.
`Chunk.Category` blijft daarom bestaan (attributie, logging, rapportage); `Chunk.Categories`
is de volledige set. Matcht niets, dan blijft de bestaande uitkomst gelden: geen categorie, en
`SocialMapChunker.TryToChunk` weigert het record.

### 2. De categorie uit de intent is een boost, geen filter

De index krijgt een veld `categories` (`Collection(Edm.String)`, filterable en facetable) en een
scoring profile `category-boost` met een `TagScoringFunction` daarop (parameter `cat`, boost
**2,0**). Een query zet `ScoringProfile = "category-boost"` en `ScoringParameters = ["cat-<domein>"]`
in plaats van een `category eq '…'`-clausule. Een document met de gevraagde categorie wordt dus
naar boven geduwd; een document zónder die categorie blijft gewoon vindbaar.

**Interpolatie `Constant`.** Een query geeft altijd precies één categorie mee, dus er valt niets
te interpoleren tussen "weinig" en "veel" matchende tags; `Constant` maakt de boost daarmee
deterministisch en gelijk voor elk document dat de gevraagde categorie draagt. Dat is precies de
semantiek die we willen: een voorkeur, geen rangorde binnen de tags.

**Reikwijdte op een hybride query.** Het scoring profile werkt op de tekst-(BM25-)tak; de
vectortak wordt er niet door gescoord en RRF fuseert beide rangordes daarna. Dat is acceptabel en
zelfs gewenst: de categorie duwt passende documenten omhoog in de tekstrangorde en dus in de
fusie, maar kan nooit een document uitsluiten dat de vectortak wél vindt. De API (2024-07-01)
accepteert het profiel op een hybride query zonder klacht.

### 3. Geo blijft wél een hard filter

`corpus eq '…'` en `geo.distance(…) le <radius>` blijven harde filters. Nabijheid is een
**feitelijke** eis van de vraag ("in de buurt van 2511CV"), geen gok van een classificatiemodel —
en de groundedness-judge vertrouwt er expliciet op dat nabijheidsclaims door de retrieval worden
gewaarborgd (ADR-0005). De uitzondering voor SC-producten (`tags/any(t: t eq 'bron:sc')`) blijft
ongewijzigd.

### 4. Geen retry meer, en een nieuwe index

Met een boost valt er niets meer te herstellen: `AskOrchestrator` doet **precies één** zoekcall en
de trace bevat één `toolCall`. `PolicyVersion.EscalationScoreThreshold` (0,015) verandert niet
maar krijgt zijn oorspronkelijke betekenis terug — "is er überhaupt iets gevonden?" in plaats van
"is de gefilterde oogst goed genoeg om de tweede poging over te slaan?". `PolicyVersion.Current`
gaat naar **1.1.0**: de betekenis van het beleid wijzigt.

Het schema wijzigt, dus de index wordt opnieuw opgebouwd onder een nieuwe naam
**`social-map-v2`** (`SearchIndexes.SocialMap`). De logische corpusnaam blijft `social-map`
(`SearchIndexes.SocialMapCorpus`) — die staat in elke chunk, komt uit de intent-classificatie en
is de waarde in het corpus-filter; hij mag niet met de indexversie meebewegen. De oude index
`social-map` blijft voorlopig staan (Free tier staat 3 indexes toe) zodat de live app tijdens de
overgang blijft werken; opruimen is follow-up 24, ná een groene eval in CI op v2.

## Alternatieven

- **Retry ook bij `no_cited_answer`.** Repareert g04 wél, maar pas nádat een generatie is betaald
  en mislukt; het maakt de orchestrator ingewikkelder in plaats van eenvoudiger en laat de foute
  aanname (één categorie, hard filter) staan. Bovendien voegt het een tweede LLM-call per
  probleemvraag toe.
- **Gefilterde en ongefilterde hits samenvoegen (RRF over beide).** Dubbele zoekkosten en een
  zelfgebouwde fusie bovenop de fusie die Search al doet — precies het soort verborgen
  rankinglogica dat we buiten de applicatie wilden houden.
- **Buurcategorie-tabel** ("bij `welzijn` ook `werk_inkomen` toestaan"). Een tweede, met de hand
  onderhouden taxonomie naast de bestaande, die bij elke nieuwe bron opnieuw fout kan staan. De
  informatie hoort bij het document ("dit ís allebei"), niet bij de vraag.
- **Categorie helemaal laten vallen.** Verliest een goedkoop en werkzaam relevantiesignaal; de
  boost kost niets extra per query.

## Gevolgen

- Retrieval kan nu documenten buiten het geclassificeerde domein teruggeven. Dat is de bedoeling;
  de guardrails die er echt toe doen (citatiefilter, escalatie, untrusted-content-boundary)
  staan verderop in de keten en zijn ongewijzigd.
- Eén zoekcall per vraag i.p.v. maximaal twee: iets minder latency en in het slechtste geval iets
  minder embedding-kosten. Traces van vóór 1.1.0 met twee `toolCalls` blijven historisch geldig.
- De taxonomie beantwoordt niet langer "wat ís dit fragment?" maar "waar hoort dit fragment bij?" —
  dat maakt nieuwe regels toevoegen goedkoper: een extra regex verbreedt de vindbaarheid en kan een
  bestaande primaire categorie niet meer stilzwijgend overrulen.
- Er staan tijdelijk twee indexes; `social-map` is vanaf nu bevroren en wordt niet meer bijgewerkt.
  Zolang die er staat, kan een rollback naar 1.0.x zonder herindexering.
- Het geo-filter blijft naast het corpus de enige harde inperking — de blast radius van een
  verkeerde modelbeslissing is daarmee kleiner dan in 1.0.x.
