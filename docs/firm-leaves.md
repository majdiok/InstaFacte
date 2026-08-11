# Congés & Absences cabinet

Module Firm-native (Master DB) pour les collaborateurs d’un cabinet comptable.

## Accès

- URL : `/firm/governance/leaves`
- Rôles : `FirmManager`, `FirmAccountant`
- Feature flag : `Features:FirmGovernance` / `firmGovernance`

## Capacités

| Acteur | Actions |
|--------|---------|
| Collaborateur (`FirmAccountant`) | Créer / modifier / soumettre / annuler **ses** demandes, consulter **son** solde et **ses** KPI (vue d’ensemble), voir le calendrier équipe |
| Responsable (`FirmManager`) | Tout + validation (accepter/refuser), types, paramètres, soldes d’ouverture, exports Excel, créer/soumettre pour un autre (assistance) |

## Workflow

`Brouillon` → `En attente` → `Acceptée` | `Refusée` ; annulation possible depuis brouillon/en attente.

- Une demande en **brouillon** peut être soumise depuis l’onglet **Demandes** (bouton « Soumettre ») ou à la création via « Soumettre immédiatement ».
- Seul le **responsable cabinet** peut approuver ou refuser (`POST /leaves/{id}/process`).

### Feuilles de temps (module associé)

Flux strict : `Brouillon` → `Soumis` → `Validé`. Le responsable ne peut valider que des saisies déjà soumises par le collaborateur (ou soumises pour son compte en assistance).

## Sécurité UI

- Onglets **Validation**, **Types**, **Paramètres** : `firmManagerGuard` (accès URL direct bloqué pour les collaborateurs).
- Vue d’ensemble : données scopées au collaborateur connecté (demandes en attente, soldes).

Le solde des types `DeductsBalance` (ex. Congé payé) diminue **uniquement** à l’acceptation.

## API

Préfixe : `api/firm/governance/leaves/*`

## Migration

`AddFirmLeaveManagement_Master` — tables `FirmLeaveTypes`, `FirmLeaveSettings`, `FirmLeaveBalances`, `FirmLeaveRequests`.

`AddFirmLeavePayrollMirror_Master` — mapping vers la paie (`FirmLeaveTypes.PayrollLeaveType`,
`CountsAsAbsence`) et suivi du report (`FirmLeaveRequests.PayrollLeaveRequestId`,
`PayrollMirrorState`, `PayrollMirrorMessage`, `PayrollMirroredAt`).

## Report vers la paie interne du cabinet

Depuis le 2026-08-11, ce module est la **source unique** des congés du cabinet : l'onglet
« Congés » de la fiche salarié passe en lecture seule dès que la paie interne est active
(`firmInternalPayroll`), et l'approbation d'une demande crée le `Payroll.LeaveRequest`
correspondant dans le tenant du cabinet (`FirmLeavePayrollMirrorService`). C'est ce miroir qui
déclenche les mécanismes déjà en place : retenue pour absence non rémunérée, IJ CNSS,
acquisition de droits à la validation du cycle.

Ce que le miroir **ne fait pas** : aucun recalcul. Les jours arrêtés côté cabinet
(demi-journées comprises, fériés issus de `ITunisianCalendarService`) sont transmis tels quels.

**Mapping des types système** — un type sans `PayrollLeaveType` n'a aucun effet sur le bulletin.

| Code | `LeaveType` paie | `CountsAsAbsence` |
|---|---|---|
| `PAID` | `Paid` | oui |
| `RTT` | `Recovery` | oui |
| `SICK` | `Sick` | oui |
| `UNPAID` | `Unpaid` | oui |
| `OTHER` | `Other` | non |
| `TRAINING`, `REMOTE` | — | non |

Un type créé par un cabinet arrive non mappé : il reste sans effet tant que le cabinet ne l'a pas
rattaché explicitement, plutôt que de deviner un impact sur la paie.

**États du report** (`FirmLeavePayrollMirrorState`) : `NotMirrored`, `Mirrored`,
`NoPayrollEffect`, `BlockedFrozenPayroll`, `Failed`, `Revoked`.

**Deux règles de conduite :**

- **Mois arrêté = rien n'est écrit.** Si un `PayrollRun` `Validated`/`Closed` couvre l'un des mois
  de la période, l'état passe à `BlockedFrozenPayroll` et l'approbateur est averti de traiter le
  congé en régularisation. Les bulletins gelés ne se recalculent jamais (`docs/paie.md`).
- **Fail-open.** Une base de paie injoignable n'annule pas l'approbation RH : l'état passe à
  `Failed`, avec son motif, et le report est rejouable. Bloquer une validation de congé parce
  qu'une base est éteinte serait un recul fonctionnel.

## Hors scope

- Ne modifie pas `FirmTimeSheetYearSettings.PaidLeaveDaysPerYear`. Séparation à retenir :
  `FirmLeaveSettings.DefaultAnnualPaidDays` porte le **droit RH**, `PaidLeaveDaysPerYear` est un
  **paramètre de coût**, utilisé pour les heures productives en mode `Parametric` uniquement.
- L'annulation d'une demande **déjà approuvée** n'est pas offerte par le domaine
  (`Cancel()` n'accepte que `Draft`/`Submitted`). `RevokeAsync` existe pour le rapprochement.
