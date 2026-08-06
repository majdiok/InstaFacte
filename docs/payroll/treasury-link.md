# Lien Trésorerie Paie

## Workflow

1. **Valider** le cycle de paie → écriture OD `JOD` (engagement 421).
2. **Exporter le virement** CSV (optionnel, stateless).
3. Après confirmation bancaire : **Régler la paie** → écriture `JB`/`JC` + lettrage 421.

## Feature flags (`Accounting` dans appsettings)

Activés par défaut (`true` dans le code et dans `appsettings.json` / `appsettings.Production.json`).

| Flag | Effet |
|------|--------|
| `PayrollTreasuryLinkEnabled` | Active les endpoints et l'UI de paiement |
| `PayrollEmployeeAuxiliaryEnabled` | Ventile le crédit 421 par salarié à la validation |
| `PayrollCnssRemittanceEnabled` | Active le bordereau CNSS mensuel et le versement compte 453 |

## API

- `POST /api/payroll/runs/{id}/payments` — enregistrer un paiement (permission `payroll:pay`)
- `GET /api/payroll/runs/{id}/payments` — historique
- `POST /api/payroll/payments/{id}/cancel` — annuler un paiement
- `POST /api/payroll/runs/{id}/payments/cancel-all` — annuler tous (prérequis pour rouvrir)
- `POST /api/payroll/declarations/cnss-remittance/payment` — versement CNSS mensuel (débit 453)

## Comptes SCE

- Engagement : `640`, `647` / `421xxxx` (auxiliaire salarié), `432`, `453`, `425`
- Décaissement salaires : débit `421xxxx` / crédit `5321` ou `5411`
- Décaissement CNSS : débit `453` / crédit `5321` ou `5411`

## Réouverture

Impossible tant que des paiements actifs existent. Utiliser **Annuler tous les paiements** puis **Rouvrir**.
