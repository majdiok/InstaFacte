# Runbook de déploiement progressif — Feuilles de temps

## Pré-requis

- Build frontend et backend vert.
- Tests unitaires/integration critiques verts (domaine Start/End/Status, service submit/timer/duplicate-week, non-régression lock/légal).
- Migration Master `EnrichFirmTimeSheetEntry_Master` appliquée.
- Vérification manuelle des parcours manager/comptable sur staging (mode classique + mode enrichi).

## Feature flag UI

- Clé localStorage : `timesheetRichUi` (`1` / `true` = mode enrichi).
- **Off par défaut** en production.
- Activation manuelle pilote : `localStorage.setItem('timesheetRichUi','1')` puis rechargement.
- Rollback UX = `timesheetRichUi=0` (API enrichie reste rétrocompatible).

## Smoke fidélité UX (flag on)

1. Header : Sxx (clic → mini-mois), badge statut, totaux h + % facturable, chronomètre.
2. Filtres : dossier, activité, responsable, facturable, lieu, « Afficher temps validés ».
3. Vue Semaine : grille colonnes absolues 08–18, pastels, weekends hachurés, now-line, totaux + colonne Total.
4. Drag empty → **modal** (pas auto-create) ; Enregistrer crée ; clic Draft → modal édition.
5. Move Draft pointer-based (snap 15 min) ; Validated / lock refusés.
6. Aujourd'hui = focus semaine (pas mode jour forcé) ; Vue Jour reste disponible séparément.
7. Table détail : édition inline brouillons, timer ligne, icônes lieu.
8. Panneau droit : légende pastels + donut dual + infos période.
9. Flag off : parcours liste/cartes inchangé.

## Smoke calendrier interactif (flag on)

- [ ] Un seul bloc DOM continu par entrée timed (pas de labels répétés par heure).
- [ ] Demi-heures pointillées visibles.
- [ ] CTA « + Ajouter du temps » ouvre la modal vide sur focusedDate.
- [ ] Annuler / mask ferme la modal sans create.
- [ ] Mini-mois : saut de date via libellé de semaine.

## Stratégie de rollout

1. Déployer API + migration (déjà en place) + front fidélité.
2. Activer `timesheetRichUi` pour les managers pilote uniquement.
3. Observer 48h (erreurs API, latence, volume validations, feedback, chevauchements, spam inline update).
4. Étendre aux collaborateurs d'un pilote limité.
5. Généraliser à tous les cabinets.

## Indicateurs de monitoring

- Taux d'erreur API `time-sheets` (4xx/5xx), y compris `submit`, `timer/*`, `duplicate-week`, `PUT` inline.
- Taux d'échecs `validate-bulk/detailed`.
- Nombre de réouvertures de périodes (`unlock`) et motifs.
- Temps moyen de saisie hebdomadaire (avant/après).
- Ratio de lignes dupliquées / lignes créées manuellement.
- Ratio saisies avec créneau Start/End vs legacy heures seules.
- Volume créations via drag-confirm vs formulaire CTA.

## Garde-fous de rollback

- Si hausse anormale des 4xx métier (validation/date/plafonds/chevauchement), désactiver le flag riche.
- Si erreurs fréquentes sur édition inline / move grille, désactiver flag et conserver mode classique.
- Si erreur de cohérence Status↔IsValidated, suspendre bulk ; validation unitaire.
- Ne pas rollback la migration sauf incident données.

## Checklist post-release (J+1 / J+7)

- J+1: logs, support N1/N2, KPI saisie, smoke timer + grille + inline + soumission.
- J+7: décision de généralisation, fermeture anomalies non bloquantes.
