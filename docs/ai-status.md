---
ai_status_version: 1
status: actief
updated: 2026-08-31
---
# sociale-kaart-rag

> ## 🟢 Actief — live op Azure; plannen 1–5 uitgevoerd, eval-suite rood op één gegrondheidscase

## Voortgang
RAG-gids "sociale kaart" voor Den Haag en Zoetermeer, live op een Azure Container App
(URL in de README). Plannen 1–4 zijn af (gate 7 gehaald 28-08); plan 5 (ADR-0006:
categorie als scoring-boost i.p.v. hard filter, index `social-map-v2` met meervoudige
categorieën) is gemerged en gedeployed op 31-08. Eval-case g04 (ZoetermeerPas) is
daarmee opgelost, live en in CI bewezen. De wekelijkse eval is nu rood op een nieuwe,
echte bevinding: bij g08 herschrijft het model de bron "Wmo-melding doen" (doelgroep =
zorgvrager) naar mantelzorgers — ongegronde claim, judge keurt terecht af. Kosten
month-to-date ≈ € 0,01.

## Open taken
- [ ] Generator-prompt aanscherpen (claims alleen over de doelgroep die de bron expliciet noemt) zodat g08 groen wordt; lokale eval 33/33 als bewijs
- [ ] PR #45 (eval-rapport) mergen met `--admin` zodra de eval groen is — de volgende run werkt dezelfde branch bij
- [ ] Follow-up 24: oude index `social-map` (v1) verwijderen na een groene CI-eval op v2
- [ ] Overige follow-ups: zie docs/superpowers/plans/followups-na-plan-1.md (o.a. 12, 14, 15, 18–20)

## Ideeën
- [ ] SC-taxonomie als unie over titel+samenvatting+keywords+paginatekst (breder labelen is onder het boost-regime goedkoop)
- [ ] Onafhankelijk tweede judge-model als het budget dat toelaat (follow-up 19)

## Detail
- docs/superpowers/plans/2026-08-29-plan5-categorie-boost.md
- docs/superpowers/plans/followups-na-plan-1.md
- docs/adr/0006-categorie-als-boost-niet-als-filter.md
