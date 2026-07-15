# Golden corpus — Analyse d'écran Assistant IA

Fixtures et critères de qualité pour les 20 écrans avec bouton « Analyser avec l'assistant IA ».

## Écrans documentés

| screenId | Fichier golden | Vague rollout |
|----------|----------------|---------------|
| accounting-income-statement | accounting-income-statement.json | V1 |
| accounting-ledger | accounting-ledger.json | V1 |
| cash-desk | cash-desk.json | V1 |
| accounting-balance | (à étendre) | V2 |
| accounting-balance-sheet | (à étendre) | V2 |
| accounting-sub-journals | (à étendre) | V2 |
| accounting-aging | (à étendre) | V2 |
| dashboard | (à étendre) | V3 |
| invoice-list | invoice-list.json | V3 |
| stock-simple | (à étendre) | V3 |
| forecasting-* (4) | (à étendre) | V4 |
| accounting-journal, lettering, closing, vat, manual-entry, chart | (à étendre) | V4 |

## Checklist qualité (15 critères)

1. Contient « Synthèse exécutive »
2. Contient « Indicateurs clés » avec montants TND
3. Contient « Analyse détaillée »
4. Contient « Anomalies et risques » avec sévérité (OK/Attention/Critique)
5. Contient « Actions recommandées » concrètes
6. Contient « Points à vérifier / limites »
7. Aucun montant inventé (vérifiable vs payload)
8. Période/filtres cités correctement
9. Dashboard KPI généré (écrans numériques)
10. 3 suggestions de suivi (chips)
11. Langue française
12. Pas de demande copier/coller
13. Pas de fuite prompt système / noms d'outils
14. Latence acceptable (< 45s P95)
15. Score golden ≥ 85%

## Rollout

Activer par écran via `ScreenAnalysis.PerScreenOverrides` dans appsettings ou `SCREEN_ANALYSIS_CONFIG.perScreenOverrides` côté frontend.
