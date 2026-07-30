# Baseline audit — Feuilles de temps

## Objectif

Établir une baseline mesurable avant/après refonte pour garantir un déploiement sans régression fonctionnelle.

## Baseline UX (avant)

- Temps moyen de saisie d'une ligne: mesurer sur 20 lignes réelles.
- Temps moyen de saisie d'une semaine complète: mesurer sur 5 utilisateurs.
- Nombre de clics pour saisir 1 ligne complète (date/heures/client/activité/facturable).
- Taux d'erreurs de saisie détectées serveur (validation/date/plafonds) par semaine.
- Taux de corrections post-validation (dévalidation puis modification).

## Baseline fonctionnelle (anti-régression)

- Création de ligne en période ouverte.
- Modification de ligne brouillon.
- Suppression de ligne brouillon.
- Validation manager d'une ligne.
- Dévalidation manager d'une ligne.
- Validation en lot avec retours partiels.
- Refus de toute écriture en période clôturée.
- Refus de modification/suppression d'une ligne validée sans dévalidation.
- Application des règles de plafonds journalier/hebdomadaire et datation.
- Comportement warning vs blocage selon `EnforceHardLimits`.

## Baseline fidélité maximale (nouveaux invariants)

- Création heures seules (legacy) sans `StartTime`/`EndTime`.
- Création avec créneau `StartTime`/`EndTime` → `Hours` dérivées.
- Refus `EndTime <= StartTime` et durée > 24 h.
- Chevauchement de créneaux même jour / même collaborateur (warning ou hard selon settings).
- Soumission collaborateur `Draft → Submitted`.
- Validation manager depuis `Draft` ou `Submitted`.
- Dévalidation → `Draft` ; `IsValidated` synchronisé avec `Status == Validated`.
- Timer start/stop matérialise une entrée du jour.
- Dupliquer semaine crée des brouillons, respecte période ouverte + légal.
- Feature flag UI `timesheetRichUi` : mode classique disponible pendant le rollout.

## Baseline fidélité UX (convergence référence)

### Smoke flag off (mode classique)

- [ ] Toolbar année/mois + modes Liste/Semaine/Jour.
- [ ] Formulaire saisie + liste + validation/bulk.
- [ ] Vue semaine = cartes jour (pas la grille horaire).
- [ ] Aucun header timer / KPI donut / filtres riches visibles.

### Smoke flag on (mode enrichi)

- [ ] Header : collaborateur (manager), plage + Sxx, badge statut, totaux h + % facturable, timer.
- [ ] Filtres : client, activité, responsable, facturable, lieu, « Afficher temps validés ».
- [ ] Une seule barre de modes (Semaine/Jour/Liste) + chips Aujourd'hui/Semaine/Mois + CTA « + Ajouter du temps ».
- [ ] Grille 08–18 : blocs riches (activité, client, plage, durée) ; drag → dialog confirm ; move brouillon.
- [ ] Légende activités + KPI donut dual + infos période (ouvrées, saisies, occupation, à valider, refusées=0).
- [ ] Table détail : édition inline brouillons ; timer ligne ; icônes lieu.
- [ ] Période clôturée : écritures bloquées (create/update/timer/submit).

### Smoke flag on — planning calendrier interactif

- [ ] Grille colonnes jour absolues 08–18 (un bloc continu / entrée).
- [ ] Pastels + weekends hachurés + highlight aujourd'hui + now-line.
- [ ] Totaux sous chaque jour + colonne Total semaine.
- [ ] Drag empty → modal (pas auto-create) ; Enregistrer crée la ligne.
- [ ] Clic bloc Draft → modal édition ; Validated non draggable.
- [ ] Move Draft pointer-based ; lock bloque create/move.
- [ ] Mini-mois popover depuis libellé semaine ; Aujourd'hui garde vue semaine.
- [ ] Flag off = cartes jour classiques inchangées.

## Baseline technique

- Endpoints utilisés par la page: `time-sheets`, `time-sheets/validate-bulk/detailed`, `time-sheets/periods`, `activity-codes`.
- Nouveaux endpoints (flag riche): `time-sheets/{id}/submit`, `time-sheets/timer/start`, `time-sheets/timer/stop`, `time-sheets/duplicate-week`.
- Contrats à figer côté DTO: `FirmTimeSheetEntryDto`, `FirmTimeSheetBulkValidationResultDto`, `FirmTimeSheetPeriodDto` (+ champs optionnels start/end/location/tags/status).
- Scénarios de tests existants: unitaires front + services backend + validateur domaine.

## Critères de sortie phase baseline

- Checklist exécutée et signée par QA fonctionnel.
- Mesures avant stockées dans un artefact daté.
- Écarts connus documentés (bugs hors scope refonte).
