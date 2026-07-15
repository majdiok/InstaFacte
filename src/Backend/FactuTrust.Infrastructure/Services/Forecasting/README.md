# Module AI Forecasting — Notes de déploiement

## Migration EF Core

Avant le premier déploiement, générer la migration tenant pour les nouvelles tables :

```bash
dotnet ef migrations add AddForecastingModule_Tenant \
  -c TenantDbContext \
  -p src/Backend/FactuTrust.Infrastructure \
  -s src/Backend/FactuTrust.API \
  -o Migrations/Tenant
```

La commande crée à la fois le fichier de migration et synchronise `TenantDbContextModelSnapshot.cs`. Tables créées :

- `SalesForecasts`
- `ReplenishmentRecommendations`
- `PromotionRecommendations`
- `ProductAbcXyzClassifications`
- `ForecastRecomputeAudits`

## Activation

1. Définir `Features:Forecasting:Enabled=true` dans `appsettings.json` (ou par variable d'environnement).
2. Optionnel : activer les services background `Forecasting:BackgroundRecomputeEnabled=true` et `Forecasting:PromotionDetectorEnabled=true`.
3. Optionnel : activer les tools IA mutants (`prepare_purchase_order_from_replenishment`, `prepare_promotion_application`) en passant `Ollama:EnableMutationTools=true`.

## Mise à jour annuelle

Les dates lunaires tunisiennes sont stockées dans `Resources/tunisian-lunar-holidays.json`. Couverture actuelle : **2024–2030**. Mettre à jour avant le 1er janvier 2031.
