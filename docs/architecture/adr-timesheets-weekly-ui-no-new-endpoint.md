# ADR — Vue semaine/jour sans nouvel endpoint backend

> **Superseded (2026-07-29)** by the full-parity timesheet plan.
> New endpoints are now part of the contract: `submit`, `timer/start`, `timer/stop`, `duplicate-week`.
> Legacy CRUD + lock/validate invariants remain unchanged; new fields are optional for backward compatibility.

## Contexte (historique)

La première itération UX des feuilles de temps introduisait une vue semaine/jour et des actions de saisie rapide côté frontend **sans** nouvel endpoint.

## Décision initiale

Ne pas ajouter de nouvel endpoint backend dans cette itération.

## Décision actuelle

Pour la fidélité maximale (timer, duplication de semaine, workflow Soumis), des endpoints ciblés JSON-only sont ajoutés. Le mode UI classique reste disponible via le flag `timesheetRichUi`.
