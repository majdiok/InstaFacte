# Runbook (archivé) — Cutover du module Réapprovisionnement V2 → standard

> ✅ **CUTOVER EFFECTUÉ LE 2026-05-15** — la V1 a été supprimée, V2 renommée en composant canonique
> (sans suffixe `V2` dans le code). Ce runbook est conservé comme **archive historique** : les étapes
> ci-dessous ne doivent plus être exécutées en production. Pour la référence technique courante,
> voir [`docs/architecture/forecasting-replenishment.md`](../architecture/forecasting-replenishment.md).

> **Objectif (historique)** : déployer le module **Réapprovisionnement V2** (`Forecasting > Réapprovisionnement`) en production en minimisant le risque opérationnel.
>
> **Stratégie (historique)** : double pipeline (V1 = comportement existant, V2 = nouveau code) derrière un *feature flag* binaire activable par tenant, sans modification de schéma destructive.

---

## 0. TL;DR

| Étape | Action | Réversible ? |
|---|---|---|
| 1 | Déployer le code (V1 + V2 cohabitent, flag V2 = `false`) | ✅ Trivial |
| 2 | Appliquer la migration EF Core `AddReplenishmentV2_Tenant` (additive) | ✅ Via `Down()` |
| 3 | Activer le flag pour 1 tenant pilote (`staging`) | ✅ Bascule à chaud (`false` ↔ `true`) |
| 4 | Recette utilisateur 5 jours | — |
| 5 | Activation progressive par cohorte de tenants | ✅ Par tenant |
| 6 | (Optionnel, +30 j) Suppression du code V1 | ❌ Définitif |

Toutes les étapes 1 → 5 sont **rollback-safe** : un simple `false` sur le flag fait retomber tout un tenant sur V1.

---

## 1. Pré-requis avant déploiement

- [ ] **Build vert** sur la branche `feature/replenishment-v2` :
  - `dotnet build FactuTrust.sln` → 0 warning, 0 erreur
  - `dotnet test tests/FactuTrust.Infrastructure.Tests` → 366/366 passés
  - `dotnet test tests/FactuTrust.API.Tests` → 20/20 passés
  - `bun run build` (Angular) → succès
  - `npx playwright test --list e2e/forecasting/` → 66 tests détectés
- [ ] La migration EF Core est **régénérée** localement :
  ```powershell
  cd src/Backend/FactuTrust.Infrastructure
  Remove-Item Migrations/Tenant/20260513000000_AddReplenishmentV2_Tenant.cs
  dotnet ef migrations add AddReplenishmentV2_Tenant `
      --context TenantDbContext `
      --output-dir Migrations/Tenant `
      --startup-project ../FactuTrust.API/FactuTrust.API.csproj
  ```
  → vérifier que `TenantDbContextModelSnapshot.cs` et `*.Designer.cs` sont commités.
- [ ] Le fichier `appsettings.json` contient bien le bloc :
  ```jsonc
  "Forecasting": {
    "ReplenishmentV2": {
      "Enabled": false,           // OFF par défaut
      "AutoCreatePurchaseOrders": true,
      "UrgencyThresholdDays": 3,
      "DefaultServiceLevelZ": 1.65,
      "MaxPrepareBatchSize": 200,
      "UndoWindowHours": 24
    }
  }
  ```

---

## 2. Déploiement initial (J0)

### 2.1 Code

Merger la branche `feature/replenishment-v2` sur `main`. Le déploiement standard pousse le code en production.

État résultant :
- L'URL `/forecasting/replenishment` continue à pointer vers le composant V1 (`environment.forecastingReplenishmentV2 = false`).
- Les nouveaux endpoints `/api/forecasting/replenishment/v2/*` retournent **HTTP 503** car `Features:Forecasting:ReplenishmentV2:Enabled = false`.
- Les nouveaux DLLs sont chargés, mais aucun chemin d'exécution V2 n'est encore parcouru.

### 2.2 Migration BD

La migration `AddReplenishmentV2_Tenant` est **additive** :
- `ALTER TABLE Products ADD PreferredSupplierId UNIQUEIDENTIFIER NULL` (× 5 colonnes nullables)
- `ALTER TABLE ReplenishmentRecommendations ADD ...` (× 8 colonnes, 2 avec `DEFAULT 0`)
- `CREATE TABLE ReplenishmentDecisionAudits`
- 3 nouveaux indexes

Aucun `DROP` / `RENAME`. Les lignes existantes restent valides (les nouveaux champs valent `NULL` ou `0`).

Si `TenantMigrations.ApplyOnStartup = true` (cas par défaut), la migration s'applique automatiquement au démarrage du process API. Sinon :

```powershell
cd src/Backend/FactuTrust.Infrastructure
dotnet ef database update --context TenantDbContext --connection "<chaîne>"
```

### 2.3 Smoke tests (J0+1h)

- [ ] L'écran `/forecasting/replenishment` est toujours fonctionnel (V1 vivant).
- [ ] Le bouton "Recalculer tout" du hub déclenche bien la chaîne complète :
  ```
  POST /api/forecasting/recompute → ForecastRecomputeOrchestrator → ReplenishmentService.GenerateRecommendationsAsync(null, null, ct)
  ```
- [ ] La modal "Voir la prévision de demande" → bouton "Créer une recommandation" envoie `productId` dans la query (fix F-C3 livré rétroactivement à V1).
- [ ] Logs : aucune exception nouvelle (`ReplenishmentRecommendation`, `Product`, `TenantDbContext`).

---

## 3. Activation pilote (J+1 → J+5)

### 3.1 Choix du tenant pilote

Critères :
- Tenant *interne* ou client de confiance — pas un client production critique.
- Volume modéré : 50–500 recommandations actives, 2–5 utilisateurs avec `forecasting:manage`.
- Inventaire bien renseigné (fournisseur préféré + MOQ + packaging sur ≥ 70 % des produits stock-managés) pour exercer pleinement V2.

### 3.2 Activation du flag

Le flag est lisible **par tenant** via `IOptionsSnapshot<ForecastingOptions>` (les options sont liées au `Forecasting:` config root, qui peut être surchargé via `PlatformTenantModuleOverridesController`).

Si la surcharge tenant est en place :
```http
POST /api/platform/tenants/{tenantId}/module-overrides
Body: { "section": "Forecasting:ReplenishmentV2", "key": "Enabled", "value": "true" }
```
Sinon, basculer globalement dans `appsettings.{Environment}.json` :
```json
"Forecasting": { "ReplenishmentV2": { "Enabled": true } }
```
puis redémarrer le process API.

Côté **frontend**, le flag `environment.forecastingReplenishmentV2` est compile-time : modifier `environment.prod.ts` à `true` et redéployer.
> Pour éviter un redéploiement par tenant, l'URL **dédiée** `/forecasting/replenishment-v2` est disponible et charge toujours V2 — un super-admin peut renvoyer les pilotes vers cette URL.

### 3.3 Recette pilote (Definition of Done — extrait)

Cocher chaque scénario manuel :

| # | Scénario | Attendu |
|---|---|---|
| 1 | Génération ciblée produit (modal demande → "Créer reco") | UNE seule reco générée, pas le catalogue entier |
| 2 | Préparation BC multi-fournisseurs | N BC créés (1 par fournisseur), chaque reco devient `Ordered` + lien cliquable |
| 3 | Rejet avec raison (drop-down + texte libre) | Reason persisté, visible dans l'historique |
| 4 | Annulation < 24h | Reco revient à `Pending`, audit row "Revert" écrite |
| 5 | Override quantité + fournisseur | Valeurs prises en compte au prochain "Préparer BC" |
| 6 | Stock minimum produit respecté | Reco déclenchée même si formule statistique dirait non |
| 7 | MOQ + packaging arrondis | Quantité finale ≥ MOQ ET multiple de PackagingQty |
| 8 | Stock en commande déduit | Pas de nouvelle reco si `onHand + onOrder > ROP` |
| 9 | Export CSV | Fichier téléchargé, encodage UTF-8 BOM, séparateur `;` |
| 10 | KPI bar | Taux de service > 90 %, top urgences cohérent |
| 11 | Accessibilité clavier | Tab/Shift+Tab dans modales, Escape ferme, skip-link visible au focus |
| 12 | Régression V1 (tenant non-pilote) | Aucun changement de comportement |

---

## 4. Activation progressive (J+5 → J+30)

Stratégie en cohortes de 5 tenants tous les 2 jours :

```
Jour 5    → Cohorte 1 (tenants les + actifs)
Jour 7    → Cohorte 2
Jour 9    → Cohorte 3
…
Jour 30   → Activation globale (flag par défaut à `true`)
```

À chaque cohorte :
1. Vérifier que la cohorte précédente n'a pas remonté de ticket P1/P2.
2. Activer le flag.
3. Notifier les key users.
4. Monitorer les logs sur 48h.

### Métriques à surveiller

- `replenishmentsGenerated` dans `ForecastRecomputeAudits` — variance par rapport à la baseline V1.
- Codes erreur 5xx sur `/api/forecasting/replenishment/v2/*` — doivent rester ≤ 0.1 %.
- Temps de réponse moyens sur les endpoints V2 — comparable à V1 (la génération V2 fait quelques jointures supplémentaires : on-order + supplier name).
- Taux d'utilisation des nouvelles fonctions :
  - % de recos qui transitent par `Override` (cible : > 5 %).
  - % de BC créés via `CreatePurchaseOrders` (cible : > 80 % des recos approuvées).

---

## 5. Procédure de rollback

### 5.1 Rollback à chaud (un tenant en difficulté)

```http
POST /api/platform/tenants/{tenantId}/module-overrides
Body: { "section": "Forecasting:ReplenishmentV2", "key": "Enabled", "value": "false" }
```

Effet immédiat :
- La route `/forecasting/replenishment` retourne V1 au prochain `loadComponent` (rechargement de page suffit côté navigateur).
- Les endpoints `/v2/*` répondent 503 — pas d'accès accidentel.
- Les **données déjà saisies** (overrides manuelles, notes, history audits, recos en `Ordered`) **restent en base** — elles redeviendront visibles dès la prochaine bascule.

### 5.2 Rollback global

Sortir le flag global dans `appsettings.json` → redéployer. Identique à 5.1 mais pour tous les tenants.

### 5.3 Rollback de schéma (à éviter, dernier recours)

Si une corruption de schéma est détectée :

```powershell
cd src/Backend/FactuTrust.Infrastructure
dotnet ef database update PreviousMigrationName --context TenantDbContext --connection "<chaîne>"
```

Note : la migration `AddReplenishmentV2_Tenant.cs::Down()` drope toutes les colonnes V2 + la table audit. Les overrides utilisateur et la timeline d'audit seront **définitivement perdus** — d'où le caractère "dernier recours".

---

## 6. Post-cutover (J+60, optionnel)

Une fois 100 % des tenants stabilisés sur V2 pendant 30 jours consécutifs, on peut **supprimer** le code V1 :

- [ ] `replenishment-board.component.ts` (et son spec)
- [ ] `ReplenishmentService.cs` (V1)
- [ ] Endpoints `/api/forecasting/replenishment/*` non préfixés (sauf `/generate` qui sert encore au product-demand modal)
- [ ] Méthode `IReplenishmentService.GenerateRecommendationsAsync` côté `IForecastRecomputeOrchestrator` → migrer vers `IReplenishmentServiceV2`.

> ⚠️ **Avant** suppression, exécuter les tests V1 une dernière fois pour confirmer qu'aucun chemin n'en dépend encore (`grep -r ReplenishmentService` sans le `V2`).

---

## 7. Annexes

### 7.1 Tableau de bord runtime

| Métrique | Source | Cible |
|---|---|---|
| % recos transitées en `Ordered` | `ReplenishmentRecommendations.Status` | > 60 % |
| Latence `POST /v2/create-purchase-orders` (p95) | OpenTelemetry trace | < 800 ms |
| Erreurs 5xx sur endpoints V2 | Application Insights | < 0.1 % |
| Taille `ReplenishmentDecisionAudits` | SQL count | linéaire avec usage |

### 7.2 Contacts

- **Owner technique** : équipe Forecasting
- **PO module** : Achats / Logistique
- **Escalade** : channel `#factutrust-forecasting`
