# Lien Trésorerie Paie

## Workflow

1. **Valider** le cycle de paie → écriture OD `JOD` (engagement, crédit 425 par salarié).
2. **Exporter le virement** CSV (optionnel, stateless).
3. Après confirmation bancaire : **Régler la paie** → écriture `JB`/`JC` + lettrage sur 425.

## Feature flags (`Accounting` dans appsettings)

Activés par défaut (`true` dans le code et dans `appsettings.json` / `appsettings.Production.json`).

| Flag | Effet |
|------|--------|
| `PayrollTreasuryLinkEnabled` | Active les endpoints et l'UI de paiement |
| `PayrollEmployeeAuxiliaryEnabled` | Ventile le crédit 425 par salarié à la validation |
| `PayrollCnssRemittanceEnabled` | Active le bordereau CNSS mensuel et le versement compte 453 |

Le **décaissement des avances et prêts** (`DisbursementEntriesEnabled`) se règle par dossier depuis
*Paie → Paramètres → Comptabilisation de la paie*, et non par `appsettings`. Il est **éteint par
défaut**.

## API

- `POST /api/payroll/runs/{id}/payments` — enregistrer un paiement (permission `payroll:pay`)
- `GET /api/payroll/runs/{id}/payments` — historique
- `POST /api/payroll/payments/{id}/cancel` — annuler un paiement
- `POST /api/payroll/runs/{id}/payments/cancel-all` — annuler tous (prérequis pour rouvrir)
- `POST /api/payroll/declarations/cnss-remittance/payment` — versement CNSS mensuel (débit 453)

## Comptes

Cartographie complète et différences entre profils : `docs/payroll/nct01-mapping.md`.

- Engagement : débit `640` / `647` (et `6611`, `6612`, `6404`, `64602` sous le profil SCE) —
  crédit `425xxxxxxx` (auxiliaire salarié), `432`, `453`, `437`, `421`
- Décaissement des salaires : débit `425xxxxxxx` / crédit `5321` ou `5411`
- Décaissement CNSS : débit `453` / crédit `5321` ou `5411`
- Décaissement d'une avance : débit `421` / crédit `5321` ou `5411`
- Décaissement d'un prêt salarié : débit `421.1` / crédit `5321` ou `5411`

> **Attention.** Sans écriture de décaissement, la retenue d'avance opérée sur le bulletin crédite
> `421` sans qu'aucun débit ne l'ait ouvert : le compte, qui est un compte d'actif, devient
> durablement créditeur. Soit on active le décaissement automatique, soit on saisit l'OD à la main
> à chaque versement.

## Réouverture

Impossible tant que des paiements actifs existent. Utiliser **Annuler tous les paiements** puis
**Rouvrir**. La réouverture extourne l'OD d'engagement **et**, le cas échéant, l'OD de reclassement
SCE du cycle.
