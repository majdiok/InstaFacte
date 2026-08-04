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

## Hors scope

- Ne remplace pas les congés **paie** (`LeaveRequest` tenant / RH & Paie).
- Ne modifie pas `FirmTimeSheetYearSettings.PaidLeaveDaysPerYear` (paramètre productivité).
