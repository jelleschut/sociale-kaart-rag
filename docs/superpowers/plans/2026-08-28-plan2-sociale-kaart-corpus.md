# Sociale-kaart RAG — plan 2: het echte sociale-kaart-corpus (spec-stap 5)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** De `social-map`-index vullen met echte, open sociale-kaart-data voor Den Haag én Zoetermeer, met provenance, grofmazige geo en categorie-taxonomie, en `/ask` daarop laten werken met geo-/categoriefilter — waarna het tijdelijke kb-POC-corpus verdwijnt.

**Architecture:** Twee bron-adapters in `src/Ingest` (Samenwerkende Catalogi via SRU + gemeentepagina-tekst; OpenStreetMap via Overpass) leveren `Chunk`-records met `Corpus = "social-map"` aan de bestaande `IndexUpserter`. Eén taxonomie-mapper in `src/Core` zet bronvelden om naar de vaste categorieën. Retrieval krijgt een optioneel geo-filter (PDOK postcode → punt) en categoriefilter; de orchestrator leidt die af uit de (geredigeerde) vraag en de intent. Alles wat er al ligt (policy, generatie, trace, infra) blijft ongewijzigd.

**Tech Stack:** bestaand (.NET 10, Azure AI Search 12, OpenAI 2.1) + `System.Xml.Linq` (SRU), `HttpClient` met rate-limiting, Overpass QL, PDOK Locatieserver v3_1, `HtmlAgilityPack` voor pagina-tekst.

**Besluiten (28-08-2026, ADR-0002):** corpus = Samenwerkende Catalogi (SC) + OSM, met de gelinkte gemeentepagina's; gemeentelijke "sociale kaart"-datasets bleken niet open of offline (details in ADR-0002). Alle `az`/`gh` via de privé-profielen (zie plan 1).

**Gate (spec §10 stap 5):** geo-filter werkt, geen huisnummers in de index, beide gemeenten aanwezig, attributie zichtbaar in het API-antwoord.

---

## Feiten uit de verkenning (28-08-2026)

| Bron | Toegang | Omvang | Velden | Licentie |
|---|---|---|---|---|
| Samenwerkende Catalogi | `https://zoekdienst.overheid.nl/sru/Search?version=1.2&operation=searchRetrieve&x-connection=sc&query=authority="'s-Gravenhage"` (paging `startRecord`/`maximumRecords≤50`); indexen o.a. `authority`, `keyword`, `subject`, `title`, `abstract`, `uniformeProductnaam`, `productID`, `identifier`, `modified` | Den Haag 266, Zoetermeer 230 | `dcterms:title`, `dcterms:abstract` (DH ≈ 340 tekens, ZM ≈ 120), `dcterms:subject` (TaxonomieBeleidsagenda; ±1/3 leeg), `overheidproduct:uniformeProductnaam`, `dcterms:identifier` (URL gemeentepagina), `dcterms:modified`, `dcterms:spatial` | overheidsinformatie; hergebruiksvoorwaarden overheid.nl **te bevestigen** (copyright-pagina's geven 410) — Task 1 |
| Gemeentepagina's | `denhaag.nl` (`<main>` ≈ 6,5k tekens; robots: alles toegestaan behalve `*.pdf`), `zoetermeer.nl` (`<main>` bevat alleen de intro; content zit dieper — selector bepalen in Task 4; robots: alleen SEO-bots geblokkeerd) | ≈ 500 pagina's | HTML | onderdeel van dezelfde overheidsinformatie |
| OpenStreetMap | Overpass `https://overpass-api.de/api/interpreter`, query op `area["name"="Den Haag"]["admin_level"="8"]` en Zoetermeer; tags `amenity` ∈ social_facility/community_centre/social_centre, `healthcare=*`, `office` ∈ charity/ngo/association | 820 (85 Zoetermeer): 121 tandarts, 106 social_facility, 93 wijkcentrum, 77 apotheek, 76 huisarts, 68 fysio, 37 kliniek, 33 ngo, 25 vereniging, 16 alternatief, 15 begeleid wonen, 13 groepswoning, 11 verpleeghuis, 10 ziekenhuis, 10 counselling, … | `name` (799), `addr:postcode` (522), `addr:street`, `addr:housenumber`, `phone`/`contact:phone` (188), `website`/`contact:website` (339), `opening_hours` (148), `description` (11), coördinaten | ODbL 1.0 — bronvermelding "© OpenStreetMap-bijdragers" verplicht; afgeleide database (onze index) valt onder share-alike |
| PDOK Locatieserver | `https://api.pdok.nl/bzk/locatieserver/search/v3_1/free?q=2511CV&fq=type:postcode&rows=1&fl=centroide_ll` → `POINT(4.3156 52.0813)`; ook `fq=type:gemeente` voor gemeente-centroïde | gratis, geen key | — | CC0 |

---

## Bestandsstructuur (nieuw/gewijzigd)

```
docs/adr/0002-databron-sociale-kaart.md          # Task 1
src/Core/SocialMap/Taxonomy.cs                    # vaste categorieën + mapping (Task 2)
src/Core/Retrieval/ISearchTool.cs                 # SearchQuery krijgt Near + Category (Task 7)
src/Core/Retrieval/AzureSearchTool.cs             # geo-/categoriefilter (Task 7)
src/Core/Retrieval/IGeocoder.cs, PdokGeocoder.cs  # postcode → punt (Task 7)
src/Core/AskOrchestrator.cs                       # postcode uit vraag, categorie uit intent (Task 7)
src/Ingest/Sources/SamenwerkendeCatalogiSource.cs # SRU → SocialMapRecord (Task 3)
src/Ingest/Sources/SocialMapRecord.cs             # bron-neutraal tussenmodel (Task 3)
src/Ingest/PageFetcher.cs                         # gemeentepagina → tekst, rate-limited (Task 4)
src/Ingest/Sources/OsmOverpassSource.cs           # Overpass → SocialMapRecord (Task 5)
src/Ingest/SocialMapChunker.cs                    # record → Chunk (Task 6)
src/Ingest/Program.cs                             # verbs ingest-social-map, ingest-sc, ingest-osm (Task 6)
tests/Ingest.Tests/Fixtures/{sc-denhaag.xml, denhaag-page.html, zoetermeer-page.html, osm-sample.json}
tests/Core.Tests/{TaxonomyTests,GeoQueryTests,AskOrchestratorGeoTests}.cs
tests/Ingest.Tests/{SamenwerkendeCatalogiSourceTests,PageFetcherTests,OsmOverpassSourceTests,SocialMapChunkerTests}.cs
.github/workflows/ingest.yml                      # source-keuze social-map (Task 8)
README.md, docs/superpowers/specs/…design.md      # attributie + corpusbesluit (Task 8, 9)
```

---

### Task 1: ADR-0002 databron + licentiebevestiging

**Files:**
- Create: `docs/adr/0002-databron-sociale-kaart.md`

- [ ] **Step 1: Schrijf de ADR**

```markdown
# ADR-0002: Databron sociale kaart

Datum: 2026-08-28 · Status: geaccepteerd

## Context
Spec §2/§8: publieke sociale-kaart-data zorg & welzijn voor Den Haag/Zoetermeer, alleen organisaties,
open licentie, grofmazige locatie. Verkenning 27/28-08-2026 (zie tabel in plan 2):
- Gemeente Den Haag "Sociale kaart" (ArcGIS-webmap Datalab_DenHaag) is een gebouwenkaart
  (stadhuis, zwembaden, servicepunten met bouwjaar/m²), zonder licentie-tag — geen dienstenbeschrijvingen.
- Den Haag opendatasoft (355 datasets, CC-0) bevat geen sociale kaart; "Voorzieningen seniorvriendelijk"
  (2018) heeft geen bruikbare namen/tekst.
- Zwartewaterland "Sociale kaart" (CC-BY, 2022) staat op data.overheid.nl maar de ArcGIS-bron geeft
  403/"Token Required" — offline.
- Zoetermeerwijzer, socialekaart.nl (SKN), ZorgkaartNederland, Vektis AGB: gesloten of niet-commercieel.
- CIBG LRZa (CC-0): alleen registerregels, alleen gezondheid, geen bulk-export gevonden.

## Besluit
Twee open bronnen, elk met eigen adapter en provenance:
1. **Samenwerkende Catalogi** (overheid.nl, SRU `x-connection=sc`): alle producten/diensten van
   gemeente Den Haag (266) en Zoetermeer (230) — titel, samenvatting, onderwerp, officiële URL.
   De gelinkte gemeentepagina wordt tijdens ingest opgehaald voor de volledige tekst
   (rate-limited, robots.txt gerespecteerd, snapshot in Blob). Dekt de *information*-intent
   (regelingen: Wmo, schuldhulp, bijstand, mantelzorg, …).
2. **OpenStreetMap** (Overpass): 820 locaties (wijkcentra, sociale voorzieningen, huisartsen,
   apotheken, fysio, ngo's, verenigingen) met adres, telefoon, website, openingstijden en
   coördinaten. Dekt de *find_help*-intent met geo.

Geo: OSM-coördinaten worden afgerond op 3 decimalen (≈ 100 m) en huisnummers worden niet
opgeslagen; SC-producten krijgen de gemeente-centroïde (PDOK). Vragen met een postcode worden
via PDOK naar een punt vertaald en gefilterd op afstand.

## Licenties en attributie
- OSM: ODbL 1.0. Attributie "© OpenStreetMap-bijdragers" in README, UI en in elk API-antwoord
  (`sources[].attribution`). Onze index is een afgeleide database; bij publicatie van de index
  zelf geldt share-alike — de index wordt niet gepubliceerd, alleen antwoorden met bronverwijzing.
- Samenwerkende Catalogi / gemeentepagina's: overheidsinformatie; attributie
  "Bron: gemeente <naam> via Samenwerkende Catalogi (overheid.nl)". Hergebruiksvoorwaarden
  bevestigd op: <datum + vindplaats invullen in Task 1 stap 2>.
- PDOK Locatieserver: CC0.

## Gevolgen
- Twee adapters i.p.v. één; SC-tekst hangt af van de gemeentesites (snapshot maakt ingest
  reproduceerbaar).
- Zoetermeer heeft minder OSM-dekking (85 POI's) en kortere SC-samenvattingen.
- Persoonsgegevens: bronnen bevatten organisaties; pagina-tekst wordt gefilterd op e-mailadressen
  en telefoonnummers van personen (alleen organisatie-contact blijft) — zie Task 4.
- Het kb-POC-corpus vervalt zodra dit corpus live is (Task 9).
```

- [x] **Step 2: Bevestig de SC-hergebruiksvoorwaarden** — gedaan 28-08 (web-onderzoek): SC-dataset CC0; zoetermeer.nl niet-commercieel met bronvermelding; denhaag.nl onduidelijk → besluit: denhaag.nl niet ophalen (zie ADR-0002).

Open `https://www.overheid.nl/` → voettekst "Copyright"/"Proclaimer" (curl krijgt 410) en zoek de tekst over hergebruik (verwacht: CC0 of "vrij te hergebruiken met bronvermelding"). Vul in de ADR de zin "Hergebruiksvoorwaarden bevestigd op: …" in met datum en de letterlijke bewoording. Stop en meld het als er een verbod op geautomatiseerd hergebruik staat.

- [ ] **Step 3: Commit via PR**

```powershell
git checkout -b docs/adr-0002
git add docs/adr/0002-databron-sociale-kaart.md docs/superpowers/plans/2026-08-28-plan2-sociale-kaart-corpus.md
git commit -m "docs(adr): 0002 databron sociale kaart — Samenwerkende Catalogi + OpenStreetMap"
git push -u origin docs/adr-0002; gh pr create --fill; gh pr checks --watch; gh pr merge --squash --delete-branch; git checkout main; git pull
```

---

### Task 2: Taxonomie en categorie-mapping

**Files:**
- Create: `src/Core/SocialMap/Taxonomy.cs`
- Test: `tests/Core.Tests/TaxonomyTests.cs`

- [ ] **Step 1: Schrijf de falende tests**

```csharp
using SocialeKaartRag.Core.SocialMap;

namespace SocialeKaartRag.Core.Tests;

public class TaxonomyTests
{
    [Theory]
    [InlineData("doctors", null, null, null, "gezondheid")]
    [InlineData(null, "pharmacy", null, null, "gezondheid")]
    [InlineData("social_facility", null, "assisted_living", null, "wonen")]
    [InlineData("social_facility", null, "nursing_home", null, "wonen")]
    [InlineData("social_facility", null, "outreach", null, "welzijn")]
    [InlineData("community_centre", null, null, null, "welzijn")]
    [InlineData(null, null, null, "ngo", "welzijn")]
    [InlineData(null, "counselling", null, null, "welzijn")]
    [InlineData("social_facility", null, "day_care", null, "mantelzorg")]
    public void Maps_osm_tags(string? amenity, string? healthcare, string? facility, string? office, string expected)
        => Assert.Equal(expected, Taxonomy.FromOsm(amenity, healthcare, facility, office));

    [Fact]
    public void Unknown_osm_tags_map_to_null()
        => Assert.Null(Taxonomy.FromOsm("dentist_school", null, null, null));

    [Theory]
    [InlineData("Sociale zekerheid", "Bijzondere bijstand aanvragen", "", "werk_inkomen")]
    [InlineData("Zorg en gezondheid", "Wmo-ondersteuning", "", "gezondheid")]
    [InlineData("Huisvesting", "Urgentieverklaring woning", "", "wonen")]
    [InlineData(null, "Parkeervergunning voor mantelzorgers aanvragen", "", "mantelzorg")]
    [InlineData(null, "Geld lenen bij de Gemeentelijke Kredietbank", "Om schulden af te betalen", "werk_inkomen")]
    [InlineData(null, "Gehandicaptenparkeerkaart", "", "vervoer")]
    [InlineData(null, "ZoetermeerPas", "voordeelpas voor inwoners met een laag inkomen", "werk_inkomen")]
    [InlineData(null, "Vrijwilligerswerk", "", "welzijn")]
    [InlineData("Recht", "Getuigen aanmelden", "huwelijk", null)]
    [InlineData("Bestuur", "Subsidie duurzame wijkactie", "", null)]
    public void Maps_sc_subject_and_keywords(string? subject, string title, string abstractText, string? expected)
        => Assert.Equal(expected, Taxonomy.FromSamenwerkendeCatalogi(subject, title, abstractText));

    [Fact]
    public void Categories_are_the_fixed_six()
        => Assert.Equal(["gezondheid", "werk_inkomen", "wonen", "vervoer", "welzijn", "mantelzorg"], Taxonomy.Categories);
}
```

- [ ] **Step 2: Run — verwacht compile-fout** (`dotnet test tests/Core.Tests --filter TaxonomyTests`)

- [ ] **Step 3: Schrijf `src/Core/SocialMap/Taxonomy.cs`**

```csharp
using System.Text.RegularExpressions;

namespace SocialeKaartRag.Core.SocialMap;

/// <summary>Vaste taxonomie (spec §4.1) en de mapping van bronvelden naar categorie. Onbekend → null (niet indexeren).</summary>
public static partial class Taxonomy
{
    public static readonly string[] Categories = ["gezondheid", "werk_inkomen", "wonen", "vervoer", "welzijn", "mantelzorg"];

    public static string? FromOsm(string? amenity, string? healthcare, string? socialFacility, string? office)
    {
        if (socialFacility is "assisted_living" or "group_home" or "nursing_home" or "shelter") return "wonen";
        if (socialFacility is "day_care") return "mantelzorg";
        if (socialFacility is not null || amenity is "social_facility" or "community_centre" or "social_centre") return "welzijn";
        if (healthcare is "counselling") return "welzijn";
        if (healthcare is not null || amenity is "doctors" or "pharmacy" or "clinic" or "hospital" or "dentist") return "gezondheid";
        if (office is "charity" or "ngo" or "association") return "welzijn";
        return null;
    }

    // Trefwoorden op titel+samenvatting; het SC-onderwerp (TaxonomieBeleidsagenda) is te grof en vaak leeg.
    private static readonly (string Category, Regex Pattern)[] ScRules =
    [
        ("mantelzorg", Rx(@"mantelzorg|respijt")),
        ("vervoer", Rx(@"gehandicaptenparkeer|regiotaxi|wmo-vervoer|vervoersvoorziening|scootmobiel|rolstoel")),
        ("wonen", Rx(@"urgentie|woning|huisvesting|huurtoeslag|woonkosten|dakloos|opvang|beschermd wonen|woningaanpassing")),
        ("werk_inkomen", Rx(@"bijstand|schuld|inkomen|uitkering|kredietbank|armoede|minima|toeslag|kwijtschelding|werk|re-integratie|voordeelpas|\bpas\b|geld lenen")),
        ("gezondheid", Rx(@"\bwmo\b|zorg|gezondheid|hulpmiddel|thuiszorg|ggd|verslaving|jeugdhulp|dagbesteding|huishoudelijke hulp")),
        ("welzijn", Rx(@"vrijwillig|welzijn|buurt|wijkcentrum|eenzaam|ontmoet|meedoen|inburger|taal|maatschappelijk")),
    ];

    private static readonly Dictionary<string, string> ScSubjects = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Sociale zekerheid"] = "werk_inkomen", ["Zorg en gezondheid"] = "gezondheid", ["Huisvesting"] = "wonen",
        ["Migratie en integratie"] = "welzijn",
    };

    public static string? FromSamenwerkendeCatalogi(string? subject, string title, string abstractText)
    {
        var text = (title + " " + abstractText).ToLowerInvariant();
        foreach (var (category, pattern) in ScRules)
            if (pattern.IsMatch(text)) return category;
        return subject is not null && ScSubjects.TryGetValue(subject, out var c) ? c : null;
    }

    private static Regex Rx(string p) => new(p, RegexOptions.IgnoreCase | RegexOptions.Compiled);
}
```
Volgorde van de regels is bewust: specifiek (mantelzorg, vervoer) vóór generiek (gezondheid, welzijn).

- [ ] **Step 4: Run — verwacht groen**; pas trefwoorden aan tot alle cases slagen zonder de niet-sociale voorbeelden (getuigen, subsidie wijkactie) mee te nemen.

- [ ] **Step 5: Commit via PR** — `feat(social-map): vaste taxonomie en categorie-mapping voor OSM en Samenwerkende Catalogi`

---

### Task 3: Samenwerkende Catalogi-adapter (SRU)

**Files:**
- Create: `src/Ingest/Sources/SocialMapRecord.cs`, `src/Ingest/Sources/SamenwerkendeCatalogiSource.cs`
- Test: `tests/Ingest.Tests/SamenwerkendeCatalogiSourceTests.cs`, fixture `tests/Ingest.Tests/Fixtures/sc-sample.xml`

- [ ] **Step 1: Maak de fixture** — sla één echte SRU-respons op (2 records, Den Haag) via
`curl -s "https://zoekdienst.overheid.nl/sru/Search?version=1.2&operation=searchRetrieve&x-connection=sc&maximumRecords=2&query=authority%3D%22%27s-Gravenhage%22%20AND%20keyword%3Dschuldhulp" -o tests/Ingest.Tests/Fixtures/sc-sample.xml` en zet in de csproj `<None Update="Fixtures\**" CopyToOutputDirectory="PreserveNewest" />`.

- [ ] **Step 2: Schrijf `SocialMapRecord.cs`**

```csharp
namespace SocialeKaartRag.Ingest.Sources;

/// <summary>Bron-neutraal tussenmodel; SocialMapChunker maakt hier een Chunk van.</summary>
public sealed record SocialMapRecord
{
    public required string Source { get; init; }        // "sc" | "osm"
    public required string SourceId { get; init; }      // "sc:<productID>" | "osm:node/123"
    public required string SourceUrl { get; init; }
    public required string Name { get; init; }
    public string? Category { get; init; }
    public string? Summary { get; init; }
    public string? BodyText { get; init; }              // gemeentepagina (sc) of description (osm)
    public required string Municipality { get; init; }  // "Den Haag" | "Zoetermeer"
    public string? Street { get; init; }                // zonder huisnummer
    public string? Postcode { get; init; }
    public string? Phone { get; init; }
    public string? Website { get; init; }
    public string? OpeningHours { get; init; }
    public double? Lat { get; init; }
    public double? Lon { get; init; }
    public string? LastModified { get; init; }
    public required string Attribution { get; init; }
}
```

- [ ] **Step 3: Schrijf de falende tests**

```csharp
using SocialeKaartRag.Ingest.Sources;

namespace SocialeKaartRag.Ingest.Tests;

public class SamenwerkendeCatalogiSourceTests
{
    [Fact]
    public void Parses_sru_records_into_social_map_records()
    {
        var xml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sc-sample.xml"));
        var (records, total) = SamenwerkendeCatalogiSource.Parse(xml, "Den Haag");
        Assert.True(total >= 1);
        var r = records[0];
        Assert.Equal("sc", r.Source);
        Assert.StartsWith("sc:", r.SourceId);
        Assert.StartsWith("https://www.denhaag.nl/", r.SourceUrl);
        Assert.False(string.IsNullOrWhiteSpace(r.Name));
        Assert.False(string.IsNullOrWhiteSpace(r.Summary));
        Assert.Equal("Den Haag", r.Municipality);
        Assert.Contains("Samenwerkende Catalogi", r.Attribution);
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", r.LastModified!);
    }

    [Fact]
    public void Builds_paged_query_urls()
    {
        var u = SamenwerkendeCatalogiSource.QueryUrl("'s-Gravenhage", startRecord: 51, max: 50);
        Assert.Contains("x-connection=sc", u);
        Assert.Contains("startRecord=51", u);
        Assert.Contains("maximumRecords=50", u);
        Assert.Contains("authority%3D%22%27s-Gravenhage%22", u);
    }
}
```

- [ ] **Step 4: Schrijf `SamenwerkendeCatalogiSource.cs`**

```csharp
using System.Net;
using System.Xml.Linq;
using SocialeKaartRag.Core.SocialMap;

namespace SocialeKaartRag.Ingest.Sources;

/// <summary>Samenwerkende Catalogi (overheid.nl) via SRU 1.2, x-connection=sc. Eén record = één gemeentelijk product.</summary>
public sealed class SamenwerkendeCatalogiSource(HttpClient http)
{
    public const string Endpoint = "https://zoekdienst.overheid.nl/sru/Search";
    private static readonly XNamespace Dcterms = "http://purl.org/dc/terms/";
    private static readonly XNamespace Product = "http://standaarden.overheid.nl/product/terms/";

    /// <summary>authority = OWMS-naam van de gemeente: "'s-Gravenhage" of "Zoetermeer".</summary>
    public static string QueryUrl(string authority, int startRecord, int max = 50) =>
        $"{Endpoint}?version=1.2&operation=searchRetrieve&x-connection=sc&maximumRecords={max}&startRecord={startRecord}" +
        $"&query={WebUtility.UrlEncode($"authority=\"{authority}\"")}";

    public async Task<List<SocialMapRecord>> FetchAllAsync(string authority, string municipality, CancellationToken ct = default)
    {
        var all = new List<SocialMapRecord>();
        var start = 1;
        while (true)
        {
            var xml = await http.GetStringAsync(QueryUrl(authority, start), ct);
            var (page, total) = Parse(xml, municipality);
            all.AddRange(page);
            start += 50;
            if (page.Count == 0 || start > total) break;
        }
        return all;
    }

    public static (List<SocialMapRecord> Records, int Total) Parse(string xml, string municipality)
    {
        var doc = XDocument.Parse(xml);
        var total = int.TryParse(doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "numberOfRecords")?.Value, out var t) ? t : 0;
        var records = new List<SocialMapRecord>();
        foreach (var rec in doc.Descendants().Where(e => e.Name.LocalName == "recordData"))
        {
            string? V(XName n) => rec.Descendants(n).FirstOrDefault()?.Value.Trim();
            var title = V(Dcterms + "title"); var url = V(Dcterms + "identifier");
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(url)) continue;
            var abstractText = V(Dcterms + "abstract") ?? "";
            var subject = V(Dcterms + "subject");
            var productId = V(Product + "productID") ?? url;
            records.Add(new SocialMapRecord
            {
                Source = "sc", SourceId = "sc:" + productId, SourceUrl = url, Name = title, Summary = abstractText,
                Category = Taxonomy.FromSamenwerkendeCatalogi(subject, title, abstractText),
                Municipality = municipality, LastModified = V(Dcterms + "modified"),
                Attribution = $"Bron: gemeente {municipality} via Samenwerkende Catalogi (overheid.nl)",
            });
        }
        return (records, total);
    }
}
```

- [ ] **Step 5: Run — groen; commit via PR** — `feat(ingest): Samenwerkende Catalogi-adapter (SRU) → SocialMapRecord`

---

### Task 4: Gemeentepagina-tekst ophalen (rate-limited, robots-bewust, snapshot)

**Files:**
- Create: `src/Ingest/PageFetcher.cs`
- Test: `tests/Ingest.Tests/PageFetcherTests.cs`, fixtures `denhaag-page.html`, `zoetermeer-page.html` (download één pagina per site met `curl -A "Mozilla/5.0" -o …`)

- [ ] **Step 1: Bepaal de content-selector van zoetermeer.nl** — open de fixture en zoek het element dat de volledige producttekst bevat (kandidaten: `article`, `div.content`, `div[class*="product"]`, `section`); noteer de CSS-selector. Den Haag: `main`.

- [ ] **Step 2: Falende tests**

```csharp
using SocialeKaartRag.Ingest;

namespace SocialeKaartRag.Ingest.Tests;

public class PageFetcherTests
{
    private static string Fixture(string f) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", f));

    [Fact]
    public void Extracts_main_text_from_denhaag_page()
    {
        var text = PageFetcher.ExtractText(Fixture("denhaag-page.html"), "www.denhaag.nl");
        Assert.Contains("Kredietbank", text);
        Assert.DoesNotContain("<", text);
        Assert.True(text.Length > 1500 && text.Length <= PageFetcher.MaxChars);
    }

    [Fact]
    public void Extracts_product_text_from_zoetermeer_page()
    {
        var text = PageFetcher.ExtractText(Fixture("zoetermeer-page.html"), "www.zoetermeer.nl");
        Assert.Contains("ZoetermeerPas", text);
        Assert.True(text.Length > 300);
    }

    [Fact]
    public void Removes_personal_contact_details_but_keeps_organisation_lines()
    {
        var html = "<main><p>Bel Jan de Vries, 06-12345678, jan.devries@denhaag.nl</p><p>Algemeen: 14070, info@denhaag.nl</p></main>";
        var text = PageFetcher.ExtractText(html, "www.denhaag.nl");
        Assert.DoesNotContain("06-12345678", text);
        Assert.DoesNotContain("jan.devries@", text);
        Assert.Contains("14070", text);
        Assert.Contains("info@denhaag.nl", text);
    }

    [Fact]
    public void Only_fetches_allowed_hosts()
    {
        Assert.False(PageFetcher.IsAllowed(new Uri("https://evil.example/x")));
        Assert.False(PageFetcher.IsAllowed(new Uri("https://www.denhaag.nl/nl/x/"))); // ADR-0002: geen toestemming
        Assert.True(PageFetcher.IsAllowed(new Uri("https://www.zoetermeer.nl/zoetermeerpas")));
    }
}
```

- [ ] **Step 3: `PageFetcher.cs`** (voeg `HtmlAgilityPack` toe aan `src/Ingest`)

```csharp
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using SocialeKaartRag.Core.Policy;

namespace SocialeKaartRag.Ingest;

/// <summary>Haalt de gemeentepagina achter een SC-product op: max 1 request per 600 ms per host, eigen User-Agent,
/// alleen toegestane hosts, geen pdf (robots denhaag.nl). Persoonlijke contactgegevens worden weggefilterd.</summary>
public sealed partial class PageFetcher(HttpClient http)
{
    public const int MaxChars = 8000;
    public const string UserAgent = "sociale-kaart-rag/1.0 (+https://github.com/jelleschut/sociale-kaart-rag)";
    // Per-host-toestemming (ADR-0002): denhaag.nl uit tot er schriftelijke toestemming is.
    private static readonly HashSet<string> AllowedHosts = ["www.zoetermeer.nl"];
    private static readonly Dictionary<string, string[]> Selectors = new()
    {
        ["www.denhaag.nl"] = ["//main"],
        ["www.zoetermeer.nl"] = ["//article", "//main"], // aanpassen na stap 1
    };
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTimeOffset _last = DateTimeOffset.MinValue;

    public static bool IsAllowed(Uri u) => u.Scheme == "https" && AllowedHosts.Contains(u.Host) && !u.AbsolutePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

    public async Task<string?> FetchTextAsync(Uri url, CancellationToken ct = default)
    {
        if (!IsAllowed(url)) return null;
        await _gate.WaitAsync(ct);
        try
        {
            var wait = _last.AddMilliseconds(600) - DateTimeOffset.UtcNow;
            if (wait > TimeSpan.Zero) await Task.Delay(wait, ct);
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.UserAgent.ParseAdd(UserAgent);
            using var resp = await http.SendAsync(req, ct);
            _last = DateTimeOffset.UtcNow;
            if (!resp.IsSuccessStatusCode) return null;
            return ExtractText(await resp.Content.ReadAsStringAsync(ct), url.Host);
        }
        finally { _gate.Release(); }
    }

    public static string ExtractText(string html, string host)
    {
        var doc = new HtmlDocument(); doc.LoadHtml(html);
        foreach (var n in doc.DocumentNode.SelectNodes("//script|//style|//nav|//header|//footer|//noscript") ?? Enumerable.Empty<HtmlNode>()) n.Remove();
        HtmlNode? node = null;
        foreach (var sel in Selectors.GetValueOrDefault(host, ["//main", "//body"]))
            if ((node = doc.DocumentNode.SelectSingleNode(sel)) is not null) break;
        var text = HtmlEntity.DeEntitize((node ?? doc.DocumentNode).InnerText);
        text = Whitespace().Replace(text, " ").Trim();
        text = RemovePersonalContacts(text);
        return text.Length > MaxChars ? text[..MaxChars] : text;
    }

    /// <summary>Organisatie-contact blijft (algemene nummers, info@/…); mobiele nummers en persoonlijke e-mails (voornaam.achternaam@) gaan weg.</summary>
    private static string RemovePersonalContacts(string text)
    {
        text = MobilePhone().Replace(text, "[telefoon verwijderd]");
        text = PersonalEmail().Replace(text, "[e-mail verwijderd]");
        return text;
    }

    [GeneratedRegex(@"\s+")] private static partial Regex Whitespace();
    [GeneratedRegex(@"(?<!\d)(?:\+31|0031|0)[\s-]?6(?:[\s-]?\d){8}(?!\d)")] private static partial Regex MobilePhone();
    [GeneratedRegex(@"\b[a-z]+\.[a-z]+(?:\.[a-z]+)?@[A-Za-z0-9.-]+\.[A-Za-z]{2,}", RegexOptions.IgnoreCase)] private static partial Regex PersonalEmail();
}
```

- [ ] **Step 4: Run — groen; commit via PR** — `feat(ingest): PageFetcher voor gemeentepagina's met rate-limit, host-allowlist en PII-filter`

---

### Task 5: OpenStreetMap-adapter (Overpass)

**Files:**
- Create: `src/Ingest/Sources/OsmOverpassSource.cs`
- Test: `tests/Ingest.Tests/OsmOverpassSourceTests.cs`, fixture `osm-sample.json` (bewaar de output van de Overpass-query met `out tags center;` en 5 elementen)

- [ ] **Step 1: Falende tests**

```csharp
using SocialeKaartRag.Ingest.Sources;

namespace SocialeKaartRag.Ingest.Tests;

public class OsmOverpassSourceTests
{
    [Fact]
    public void Maps_elements_to_records_with_coarse_geo_and_no_house_numbers()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "osm-sample.json"));
        var records = OsmOverpassSource.Parse(json);
        Assert.NotEmpty(records);
        var r = records.First(x => x.Postcode is not null);
        Assert.Equal("osm", r.Source);
        Assert.StartsWith("osm:", r.SourceId);
        Assert.StartsWith("https://www.openstreetmap.org/", r.SourceUrl);
        Assert.Contains("OpenStreetMap", r.Attribution);
        Assert.DoesNotMatch(@"\d", r.Street ?? "");              // straat zonder huisnummer
        Assert.Equal(Math.Round(r.Lat!.Value, 3), r.Lat);          // ≈100 m
        Assert.Equal(Math.Round(r.Lon!.Value, 3), r.Lon);
        Assert.Contains(r.Municipality, new[] { "Den Haag", "Zoetermeer" });
        Assert.NotNull(r.Category);
    }

    [Fact]
    public void Elements_without_name_or_category_are_skipped()
    {
        var json = """{"elements":[{"type":"node","id":1,"lat":52.1,"lon":4.3,"tags":{"amenity":"doctors"}},{"type":"node","id":2,"lat":52.1,"lon":4.3,"tags":{"name":"X","amenity":"cafe"}}]}""";
        Assert.Empty(OsmOverpassSource.Parse(json));
    }

    [Fact]
    public void Query_covers_both_municipalities()
    {
        Assert.Contains("\"Den Haag\"", OsmOverpassSource.Query);
        Assert.Contains("\"Zoetermeer\"", OsmOverpassSource.Query);
        Assert.Contains("out tags center", OsmOverpassSource.Query);
    }
}
```

- [ ] **Step 2: `OsmOverpassSource.cs`**

```csharp
using System.Text.Json;
using SocialeKaartRag.Core.SocialMap;

namespace SocialeKaartRag.Ingest.Sources;

/// <summary>OpenStreetMap via Overpass. Coördinaten afgerond op 3 decimalen, huisnummers niet overgenomen (spec §4.1).</summary>
public sealed class OsmOverpassSource(HttpClient http)
{
    public const string Endpoint = "https://overpass-api.de/api/interpreter";
    public const string Attribution = "© OpenStreetMap-bijdragers (ODbL 1.0)";
    public const string Query =
        """
        [out:json][timeout:120];
        area["name"="Den Haag"]["admin_level"="8"]->.dh;
        area["name"="Zoetermeer"]["admin_level"="8"]->.zm;
        (
          nwr(area.dh)["amenity"~"^(social_facility|community_centre|social_centre)$"];
          nwr(area.zm)["amenity"~"^(social_facility|community_centre|social_centre)$"];
          nwr(area.dh)["healthcare"]; nwr(area.zm)["healthcare"];
          nwr(area.dh)["office"~"^(charity|ngo|association)$"]; nwr(area.zm)["office"~"^(charity|ngo|association)$"];
        );
        out tags center;
        """;

    public async Task<List<SocialMapRecord>> FetchAllAsync(CancellationToken ct = default)
    {
        using var content = new FormUrlEncodedContent([new KeyValuePair<string, string>("data", Query)]);
        using var req = new HttpRequestMessage(HttpMethod.Post, Endpoint) { Content = content };
        req.Headers.UserAgent.ParseAdd(PageFetcher.UserAgent);
        using var resp = await http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        return Parse(await resp.Content.ReadAsStringAsync(ct));
    }

    public static List<SocialMapRecord> Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var list = new List<SocialMapRecord>();
        foreach (var e in doc.RootElement.GetProperty("elements").EnumerateArray())
        {
            if (!e.TryGetProperty("tags", out var tags)) continue;
            string? T(string k) => tags.TryGetProperty(k, out var v) ? v.GetString() : null;
            var name = T("name"); if (string.IsNullOrWhiteSpace(name)) continue;
            var category = Taxonomy.FromOsm(T("amenity"), T("healthcare"), T("social_facility"), T("office"));
            if (category is null) continue;
            var (lat, lon) = Coords(e);
            var city = (T("addr:city") ?? "").ToLowerInvariant();
            var municipality = city.Contains("zoetermeer") ? "Zoetermeer" : "Den Haag";
            var type = e.GetProperty("type").GetString(); var id = e.GetProperty("id").GetInt64();
            list.Add(new SocialMapRecord
            {
                Source = "osm", SourceId = $"osm:{type}/{id}", SourceUrl = $"https://www.openstreetmap.org/{type}/{id}",
                Name = name, Category = category, Summary = Describe(tags), BodyText = T("description"),
                Municipality = municipality, Street = T("addr:street"), Postcode = T("addr:postcode")?.Replace(" ", "").ToUpperInvariant(),
                Phone = T("phone") ?? T("contact:phone"), Website = T("website") ?? T("contact:website"),
                OpeningHours = T("opening_hours"), Lat = lat is null ? null : Math.Round(lat.Value, 3), Lon = lon is null ? null : Math.Round(lon.Value, 3),
                Attribution = Attribution,
            });
        }
        return list;
    }

    private static (double? Lat, double? Lon) Coords(JsonElement e)
    {
        if (e.TryGetProperty("lat", out var la) && e.TryGetProperty("lon", out var lo)) return (la.GetDouble(), lo.GetDouble());
        if (e.TryGetProperty("center", out var c)) return (c.GetProperty("lat").GetDouble(), c.GetProperty("lon").GetDouble());
        return (null, null);
    }

    private static readonly Dictionary<string, string> TypeNames = new()
    {
        ["doctors"] = "huisartsenpraktijk", ["pharmacy"] = "apotheek", ["dentist"] = "tandarts", ["clinic"] = "kliniek", ["hospital"] = "ziekenhuis",
        ["physiotherapist"] = "fysiotherapie", ["community_centre"] = "wijkcentrum", ["social_facility"] = "sociale voorziening", ["social_centre"] = "ontmoetingscentrum",
        ["counselling"] = "hulpverlening/begeleiding", ["assisted_living"] = "begeleid wonen", ["nursing_home"] = "verpleeghuis", ["group_home"] = "woongroep",
        ["day_care"] = "dagopvang", ["outreach"] = "outreach/veldwerk", ["ngo"] = "maatschappelijke organisatie", ["association"] = "vereniging", ["charity"] = "goed doel",
    };

    /// <summary>Korte Nederlandse typering uit de tags, bv. "apotheek" of "sociale voorziening (begeleid wonen)".</summary>
    private static string Describe(JsonElement tags)
    {
        string? T(string k) => tags.TryGetProperty(k, out var v) ? v.GetString() : null;
        var main = T("social_facility") ?? T("healthcare") ?? T("amenity") ?? T("office") ?? "";
        var label = TypeNames.GetValueOrDefault(main, main.Replace('_', ' '));
        var target = T("social_facility:for");
        return target is null ? label : $"{label} voor {target.Replace('_', ' ').Replace(';', ',')}";
    }
}
```

- [ ] **Step 3: Run — groen; commit via PR** — `feat(ingest): OpenStreetMap-adapter via Overpass met grofmazige geo`

---

### Task 6: Chunking, snapshot en ingest-verbs

**Files:**
- Create: `src/Ingest/SocialMapChunker.cs`
- Modify: `src/Ingest/Program.cs`
- Test: `tests/Ingest.Tests/SocialMapChunkerTests.cs`

- [ ] **Step 1: Falende tests**

```csharp
using SocialeKaartRag.Ingest;
using SocialeKaartRag.Ingest.Sources;

namespace SocialeKaartRag.Ingest.Tests;

public class SocialMapChunkerTests
{
    private static SocialMapRecord Osm() => new()
    {
        Source = "osm", SourceId = "osm:node/1", SourceUrl = "https://www.openstreetmap.org/node/1", Name = "Wijkcentrum De Regenvalk",
        Category = "welzijn", Summary = "wijkcentrum", Municipality = "Den Haag", Street = "Regentesseplein", Postcode = "2562EN",
        Phone = "+31 70 123 4567", Website = "https://example.org", OpeningHours = "Mo-Fr 09:00-17:00", Lat = 52.078, Lon = 4.286,
        Attribution = "© OpenStreetMap-bijdragers (ODbL 1.0)",
    };

    [Fact]
    public void Osm_record_becomes_one_chunk_with_contact_text_and_geo()
    {
        var c = SocialMapChunker.ToChunk(Osm());
        Assert.Equal("social-map", c.Corpus);
        Assert.Equal("osm", c.Source);
        Assert.Equal("welzijn", c.Category);
        Assert.Contains("Wijkcentrum De Regenvalk", c.Text);
        Assert.Contains("Regentesseplein", c.Text);
        Assert.Contains("2562EN", c.Text);
        Assert.Contains("Den Haag", c.Text);
        Assert.Contains("+31 70 123 4567", c.Text);
        Assert.Contains("Mo-Fr 09:00-17:00", c.Text);
        Assert.Equal(52.078, c.Lat); Assert.Equal(4.286, c.Lon);
        Assert.Contains("OpenStreetMap", c.Tags);
        Assert.Equal(SocialeKaartRag.Core.Chunks.Chunk.MakeId("social-map", "osm:node/1"), c.Id);
    }

    [Fact]
    public void Sc_record_uses_title_summary_and_body_and_municipality_centroid()
    {
        var r = new SocialMapRecord
        {
            Source = "sc", SourceId = "sc:123", SourceUrl = "https://www.denhaag.nl/x", Name = "Bijzondere bijstand", Category = "werk_inkomen",
            Summary = "Extra geld bij hoge kosten.", BodyText = "Lange uitleg …", Municipality = "Den Haag", Lat = 52.08, Lon = 4.31,
            Attribution = "Bron: gemeente Den Haag via Samenwerkende Catalogi (overheid.nl)",
        };
        var c = SocialMapChunker.ToChunk(r);
        Assert.StartsWith("Bijzondere bijstand (gemeente Den Haag)", c.Text);
        Assert.Contains("Extra geld", c.Text);
        Assert.Contains("Lange uitleg", c.Text);
        Assert.Equal("https://www.denhaag.nl/x", c.SourceUrl);
    }

    [Fact]
    public void Records_without_category_are_rejected()
        => Assert.Null(SocialMapChunker.TryToChunk(Osm() with { Category = null }));
}
```

- [ ] **Step 2: `SocialMapChunker.cs`**

```csharp
using System.Text;
using SocialeKaartRag.Core.Chunks;
using SocialeKaartRag.Ingest.Sources;

namespace SocialeKaartRag.Ingest;

/// <summary>Sociale kaart = één chunk per organisatie-dienst (spec §4.1). Tekst is leesbaar Nederlands, zodat BM25 én embedding werken.</summary>
public static class SocialMapChunker
{
    public static Chunk? TryToChunk(SocialMapRecord r) => r.Category is null ? null : ToChunk(r);

    public static Chunk ToChunk(SocialMapRecord r)
    {
        var sb = new StringBuilder();
        sb.Append(r.Name).Append(" (gemeente ").Append(r.Municipality).AppendLine(")");
        if (!string.IsNullOrWhiteSpace(r.Summary)) sb.AppendLine(r.Summary);
        if (r.Street is not null || r.Postcode is not null) sb.Append("Adres: ").Append(r.Street).Append(' ').Append(r.Postcode).Append(' ').AppendLine(r.Municipality);
        if (r.Phone is not null) sb.Append("Telefoon: ").AppendLine(r.Phone);
        if (r.Website is not null) sb.Append("Website: ").AppendLine(r.Website);
        if (r.OpeningHours is not null) sb.Append("Openingstijden: ").AppendLine(r.OpeningHours);
        if (!string.IsNullOrWhiteSpace(r.BodyText)) sb.AppendLine().AppendLine(r.BodyText);
        var text = sb.ToString().Trim();
        return new Chunk
        {
            Id = Chunk.MakeId("social-map", r.SourceId), Corpus = "social-map", Source = r.Source, SourceId = r.SourceId, SourceUrl = r.SourceUrl,
            RetrievedAt = DateTimeOffset.UtcNow, ContentHash = Chunk.HashContent(text), Category = r.Category, LastVerified = r.LastModified,
            HeadingPath = r.Name, Tags = [r.Municipality, r.Source, r.Attribution], Lat = r.Lat, Lon = r.Lon, Text = text,
        };
    }
}
```
Attributie reist mee in `Tags` (de index heeft geen apart veld; `Tags` is filterbaar en komt terug in het zoekresultaat als je `tags` aan `Select` toevoegt — doe dat in Task 7).

- [ ] **Step 3: `Program.cs` — nieuwe verbs** (naast de bestaande)

```csharp
    case "ingest-social-map":
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        var blobs = sp.GetRequiredService<BlobServiceClient>().GetBlobContainerClient("snapshots");
        var day = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var geocoder = new SocialeKaartRag.Core.Retrieval.PdokGeocoder(http);

        // 1. Samenwerkende Catalogi + gemeentepagina's
        var sc = new SamenwerkendeCatalogiSource(http);
        var fetcher = new PageFetcher(http);
        var records = new List<SocialMapRecord>();
        foreach (var (authority, municipality) in new[] { ("'s-Gravenhage", "Den Haag"), ("Zoetermeer", "Zoetermeer") })
        {
            var page = await sc.FetchAllAsync(authority, municipality);
            var centroid = await geocoder.MunicipalityCentroidAsync(municipality);
            var withText = 0;
            for (var i = 0; i < page.Count; i++)
            {
                var body = await fetcher.FetchTextAsync(new Uri(page[i].SourceUrl));
                if (body is not null) withText++;
                page[i] = page[i] with { BodyText = body, Lat = centroid?.Lat, Lon = centroid?.Lon };
            }
            Console.WriteLine($"sc {municipality}: {page.Count} producten, {withText} met paginatekst, {page.Count(p => p.Category is not null)} in taxonomie");
            records.AddRange(page);
        }
        await blobs.GetBlobClient($"social-map/{day}/sc.json").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(records)), overwrite: true);

        // 2. OpenStreetMap
        var osmRaw = await new OsmOverpassSource(http).FetchAllAsync();
        await blobs.GetBlobClient($"social-map/{day}/osm.json").UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(osmRaw)), overwrite: true);
        Console.WriteLine($"osm: {osmRaw.Count} locaties ({osmRaw.Count(r => r.Municipality == "Zoetermeer")} Zoetermeer)");
        records.AddRange(osmRaw);

        // 3. Chunk + upsert
        var chunks = records.Select(SocialMapChunker.TryToChunk).Where(c => c is not null).Cast<SocialeKaartRag.Core.Chunks.Chunk>().ToList();
        var skipped = records.Count - chunks.Count;
        Console.WriteLine($"chunks: {chunks.Count} ({skipped} zonder categorie overgeslagen)");
        var n = await sp.GetRequiredService<IndexUpserter>().UpsertAsync(SearchIndexes.SocialMap, chunks);
        Console.WriteLine($"klaar: {n} chunks in '{SearchIndexes.SocialMap}'");
        return 0;
    }
```
Voeg `using System.Text.Json;` toe. `PdokGeocoder` komt uit Task 7 — implementeer Task 7 stap 3 (de geocoder) vóór je dit verb draait, of laat de centroïde tijdelijk `null`.

- [ ] **Step 4: Run — alle tests groen; commit via PR** — `feat(ingest): social-map chunker, snapshots en ingest-social-map verb`

---

### Task 7: Geo- en categoriefilter in retrieval en orchestrator

**Files:**
- Modify: `src/Core/Retrieval/ISearchTool.cs`, `src/Core/Retrieval/AzureSearchTool.cs`, `src/Core/AskOrchestrator.cs`
- Create: `src/Core/Retrieval/IGeocoder.cs`, `src/Core/Retrieval/PdokGeocoder.cs`
- Test: `tests/Core.Tests/GeoQueryTests.cs`, `tests/Core.Tests/AskOrchestratorGeoTests.cs`; Api `Program.cs` registreert `IGeocoder`

- [ ] **Step 1: Falende tests**

```csharp
using SocialeKaartRag.Core.Retrieval;

namespace SocialeKaartRag.Core.Tests;

public class GeoQueryTests
{
    [Fact]
    public void Filter_includes_corpus_category_and_distance()
    {
        var f = AzureSearchTool.BuildFilter("social-map", "welzijn", new GeoPoint(52.0813, 4.3156), 5);
        Assert.Equal("corpus eq 'social-map' and category eq 'welzijn' and geo.distance(geo, geography'POINT(4.3156 52.0813)') le 5", f);
    }

    [Fact]
    public void Filter_without_optional_parts()
        => Assert.Equal("corpus eq 'kb'", AzureSearchTool.BuildFilter("kb", null, null, 5));

    [Theory]
    [InlineData("welke hulp is er in 2511CV?", "2511CV")]
    [InlineData("ik woon in 2511 cv, waar kan ik terecht", "2511CV")]
    [InlineData("hulp bij schulden", null)]
    public void Postcode_is_extracted_from_question(string q, string? expected)
        => Assert.Equal(expected, PostcodeDetector.Find(q));

    [Fact]
    public void Pdok_response_is_parsed_to_point()
    {
        var json = """{"response":{"numFound":1,"docs":[{"type":"postcode","centroide_ll":"POINT(4.31557609 52.08130867)"}]}}""";
        var p = PdokGeocoder.ParsePoint(json)!;
        Assert.Equal(52.0813, p.Lat, 4); Assert.Equal(4.3156, p.Lon, 4);
    }
}
```

- [ ] **Step 2: Contractwijzigingen**

`ISearchTool.cs`: `public sealed record GeoPoint(double Lat, double Lon);` en `public sealed record SearchQuery(string Text, string? Category = null, GeoPoint? Near = null, double RadiusKm = 5, int TopK = 6);`

`AzureSearchTool.cs`: vervang de `Filter =`-regel door `Filter = BuildFilter(corpus, query.Category, query.Near, query.RadiusKm)` en voeg toe:
```csharp
    public static string BuildFilter(string corpus, string? category, GeoPoint? near, double radiusKm)
    {
        var f = $"corpus eq '{corpus}'";
        if (category is not null) f += $" and category eq '{category.Replace("'", "''")}'";
        if (near is not null) f += $" and geo.distance(geo, geography'POINT({near.Lon.ToString(System.Globalization.CultureInfo.InvariantCulture)} {near.Lat.ToString(System.Globalization.CultureInfo.InvariantCulture)})') le {radiusKm.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        return f;
    }
```
en voeg `"tags"` toe aan `options.Select`; breid `SearchHit` uit met `string[] Tags` (attributie zit erin) en vul die in `SearchAsync`.

- [ ] **Step 3: Geocoder + postcode-detectie**

```csharp
namespace SocialeKaartRag.Core.Retrieval;

public interface IGeocoder
{
    Task<GeoPoint?> PostcodeAsync(string postcode, CancellationToken ct = default);
    Task<GeoPoint?> MunicipalityCentroidAsync(string municipality, CancellationToken ct = default);
}

/// <summary>PDOK Locatieserver v3_1 (CC0, geen key). Alleen postcode-/gemeenteniveau (spec: grofmazig).</summary>
public sealed class PdokGeocoder(HttpClient http) : IGeocoder
{
    private const string Base = "https://api.pdok.nl/bzk/locatieserver/search/v3_1/free";
    public Task<GeoPoint?> PostcodeAsync(string postcode, CancellationToken ct = default) => Lookup($"{Base}?q={postcode}&fq=type:postcode&rows=1&fl=centroide_ll", ct);
    public Task<GeoPoint?> MunicipalityCentroidAsync(string municipality, CancellationToken ct = default) => Lookup($"{Base}?q={Uri.EscapeDataString(municipality)}&fq=type:gemeente&rows=1&fl=centroide_ll", ct);
    private async Task<GeoPoint?> Lookup(string url, CancellationToken ct) => ParsePoint(await http.GetStringAsync(url, ct));

    public static GeoPoint? ParsePoint(string json)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var docs = doc.RootElement.GetProperty("response").GetProperty("docs");
        if (docs.GetArrayLength() == 0) return null;
        var m = System.Text.RegularExpressions.Regex.Match(docs[0].GetProperty("centroide_ll").GetString()!, @"POINT\(([-\d.]+) ([-\d.]+)\)");
        return m.Success ? new GeoPoint(double.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture), double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)) : null;
    }
}

public static partial class PostcodeDetector
{
    [System.Text.RegularExpressions.GeneratedRegex(@"(?<!\d)([1-9]\d{3})\s?([A-Za-z]{2})(?![A-Za-z0-9])")] private static partial System.Text.RegularExpressions.Regex Rx();
    public static string? Find(string text) { var m = Rx().Match(text); return m.Success ? (m.Groups[1].Value + m.Groups[2].Value).ToUpperInvariant() : null; }
}
```

- [ ] **Step 4: Orchestrator** — constructor krijgt `IGeocoder geocoder`; na de intent, vóór de tool-call:
```csharp
            GeoPoint? near = null;
            if (tool.Corpus == SearchIndexes.SocialMap && PostcodeDetector.Find(pii.Text) is { } pc)
                near = await geocoder.PostcodeAsync(pc, ct); // postcode-only is toegestaan (PiiFilter laat hem staan)
            var category = tool.Corpus == SearchIndexes.SocialMap ? DomainToCategory(intent.Domain) : null;
            var query = new SearchQuery(pii.Text, category, near);
            var hits = await tool.SearchAsync(query, ct);
            if (hits.Count == 0 && category is not null) { query = query with { Category = null }; hits = await tool.SearchAsync(query, ct); } // fallback zonder categorie
```
met `private static string? DomainToCategory(string d) => d is "gezondheid" or "werk_inkomen" or "wonen" or "vervoer" or "welzijn" or "mantelzorg" ? d : null;`. Trace: `ToolCall.ArgumentsHash` over `Text|Category|Near|TopK`. `SourceRef` krijgt `string? Attribution` (uit `hit.Tags` — de tag die "©" of "Bron:" bevat) en de API geeft `sources[].attribution` terug. Tests in `AskOrchestratorGeoTests.cs`: (a) vraag met postcode op social-map → `SearchQuery.Near` gevuld (fake geocoder), (b) kb-corpus → geen geocoding, (c) lege hits met categorie → tweede call zonder categorie, (d) attributie in `AskResult.Sources`.

- [ ] **Step 5: Api `Program.cs`**: `builder.Services.AddHttpClient(); builder.Services.AddSingleton<IGeocoder>(sp => new PdokGeocoder(sp.GetRequiredService<IHttpClientFactory>().CreateClient()));`

- [ ] **Step 6: Run — groen (incl. alle oude tests; pas `FakeSearch`/orchestrator-tests aan op de nieuwe constructor); commit via PR** — `feat(retrieval): geo- en categoriefilter, PDOK-geocoder, attributie in bronnen`

---

### Task 8: Live ingest + gate 5

- [ ] **Step 1: Ingest lokaal** (zelfde env-vars als plan 1 Task 7): `dotnet run --project src/Ingest -- ingest-social-map`. Verwacht: `sc Den Haag: 266 producten, 0 met paginatekst` (ADR-0002), `sc Zoetermeer: 230 producten, ≥ 180 met paginatekst`, `osm: ~820 locaties (~85 Zoetermeer)`, `chunks: ≥ 900`. Duur ≈ 230 × 0,6 s ≈ 2,5 min voor de Zoetermeer-pagina's.
- [ ] **Step 2: Gate-checks**
  - Geo: `POST /ask {"question":"waar is een wijkcentrum in de buurt van 2511CV?"}` → antwoord met OSM-bronnen; `GET /trace/{id}` → `toolCalls[0].argumentsHash` anders dan zonder postcode. Hetzelfde met een Zoetermeer-postcode (bv. `2711CD`) → Zoetermeer-locaties.
  - Regeling: `{"question":"hoe vraag ik bijzondere bijstand aan in Zoetermeer?"}` → SC-bron met `denhaag.nl`/`zoetermeer.nl`-URL en attributie "Samenwerkende Catalogi".
  - Geen huisnummers: `az rest`/Search-query `search=*&$filter=corpus eq 'social-map' and source eq 'osm'&$select=text&$top=200` en grep op `Adres: [^\n]*\d` → 0 treffers.
  - Attributie: elk `sources[]`-item heeft `attribution`.
- [ ] **Step 3: `ingest.yml`**: `options: [kb, social-map]` en `run: dotnet run --project src/Ingest -- "ingest-$SOURCE"` werkt al. Draai hem één keer via `workflow_dispatch` na merge (approval in `azure`).
- [ ] **Step 4: README**: sectie "Bronnen en attributie" (OSM ODbL-regel, SC-regel, PDOK CC0) + hoe ingest te draaien. Commit via PR — `feat(social-map): live corpus voor Den Haag en Zoetermeer; ingest.yml; attributie`.

---

### Task 9: kb-POC-corpus verwijderen (besluit 27-08)

- [ ] **Step 1:** Verwijder `ingest-kb`, `KbChunksSource` + tests, `kb-chunks.jsonl`, `SearchIndexes.Kb`-registratie in Api/Ingest (`index-create` maakt alleen `social-map`), `platform_kennis`/`kb` uit `IntentClassifier` (prompt én schema), de kb-optie in `ingest.yml`; de orchestrator-tests gebruiken voortaan `social-map`/`find_help`. Verwijder de `kb`-index live: `az rest --method delete --url "https://srch-skr-9asax.search.windows.net/indexes/kb?api-version=2024-07-01"` met een Search-token (zie plan 1).
- [ ] **Step 2:** Spec §2 corpus-rij: alleen (a); ADR-0002 "Gevolgen" bijwerken; README-status. `dotnet test` groen; commit via PR — `refactor: kb-POC-corpus verwijderd (spec-besluit 27-08)`.

---

## Self-review

**Spec-dekking stap 5:** §4.1 bron-adapters, validatie (records zonder naam/categorie worden geteld en overgeslagen — voeg in Task 6 een telling per reden toe aan de console-output), normalisatie (taxonomie, postcode zonder spatie, telefoon/URL uit OSM-tags), geocoding (PDOK, alleen postcode/gemeente; OSM-coördinaten afgerond, geen huisnummers), chunking (één chunk per organisatie-dienst), provenance (source/sourceId/sourceUrl/retrievedAt/contentHash/corpus/category/geo/lastVerified), snapshot naar Blob, `workflow_dispatch`. §4.2 geo-/categoriefilter. §8 ADR-0002 met licenties en bronvermelding. Gate 5 in Task 8.

**Type-consistentie:** `SearchQuery(Text, Category, Near, RadiusKm, TopK)` in Task 7 en Program/Ingest `query`-verb (die gebruikt positional `new SearchQuery(text)` — blijft geldig); `SearchHit.Tags` nieuw in Task 7 en gebruikt in `SourceRef.Attribution`; `SocialMapRecord` velden in Task 3/5/6 gelijk; `Taxonomy.Categories` ↔ `DomainToCategory` ↔ intent-domeinen.

**Onzekerheden:** zoetermeer.nl content-selector (Task 4 stap 1); SC-hergebruikstekst (Task 1 stap 2); Overpass-rate-limits (één query, ruim binnen fair use); HtmlAgilityPack-versie (laatste stabiele).
