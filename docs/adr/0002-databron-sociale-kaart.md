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
   apotheken, fysio en andere zorglocaties (`office=*` — ngo's/verenigingen — en tandartsen
   bewust uitgesloten)) met adres, telefoon, website, openingstijden en coördinaten. Dekt de
   *find_help*-intent met geo.

Geo: OSM-coördinaten worden afgerond op 3 decimalen (≈ 100 m) en huisnummers worden niet
opgeslagen; SC-producten krijgen de gemeente-centroïde (PDOK). Vragen met een postcode worden
via PDOK naar een punt vertaald en gefilterd op afstand.

## Licenties en attributie
- OSM: ODbL 1.0. Attributie "© OpenStreetMap-bijdragers" in README, UI en in elk API-antwoord
  (`sources[].attribution`). Onze index is een afgeleide database; bij publicatie van de index
  zelf geldt share-alike — de index wordt niet gepubliceerd, alleen antwoorden met bronverwijzing.
- Samenwerkende Catalogi: de dataset "Producten en Diensten" is op data.overheid.nl gepubliceerd onder
  **CC0 1.0** (bevestigd 28-08-2026, https://data.overheid.nl/dataset/samenwerkende-catalogi-producten-en-diensten).
  Attributie (goed gebruik, niet verplicht): "Bron: gemeente <naam> via Samenwerkende Catalogi (overheid.nl, CC0)".
- Gemeentepagina's (volledige tekst): **zoetermeer.nl** staat niet-commercieel hergebruik expliciet toe met
  bronvermelding (https://www.zoetermeer.nl/c-zoetermeer, bevestigd 28-08-2026) → wordt opgehaald.
  **denhaag.nl** claimt IE-rechten op alle content en verbiedt overname van beeld, zonder uitspraak over tekst
  (https://www.denhaag.nl/nl/gebruiksvoorwaarden/) → **wordt niet opgehaald**; Haagse producten gebruiken
  uitsluitend de CC0-samenvatting + link. Toestemming kan via datashop@denhaag.nl worden gevraagd; de
  PageFetcher heeft daarvoor een per-host-schakelaar.
- PDOK Locatieserver: CC0.

## Gevolgen
- Twee adapters i.p.v. één; SC-tekst hangt af van de gemeentesites (snapshot maakt ingest
  reproduceerbaar).
- Zoetermeer heeft minder OSM-dekking (85 POI's) en kortere SC-samenvattingen.
- Persoonsgegevens: bronnen bevatten organisaties; pagina-tekst wordt gefilterd op e-mailadressen
  en telefoonnummers van personen (alleen organisatie-contact blijft) — zie Task 4.
  OSM-`description`-tags gaan door hetzelfde PII-filter als paginatekst.
- Het kb-POC-corpus vervalt zodra dit corpus live is (Task 9).
