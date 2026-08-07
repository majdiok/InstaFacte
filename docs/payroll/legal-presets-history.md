# Historique des presets légaux (LF 2020–2026)

Les paramètres officiels par exercice sont codés dans `PayrollLegalPresets.cs` et exposés via :

- `GET /api/payroll/settings/parameters/{year}/preset`
- Bouton **Recharger défauts LF** dans Paramètres paie

Le preset 2026 est strictement aligné sur l'ancien `CreateDefaults` (non-régression garantie par `PayrollLegalPresetsRegressionTests`).

Les exercices existants ne sont jamais modifiés automatiquement : seul un rechargement explicite applique le preset.
