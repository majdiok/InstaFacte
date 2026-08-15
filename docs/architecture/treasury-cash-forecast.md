# Trésorerie prévisionnelle par IA — référence technique

> Audience : développeurs backend / frontend, support N3, QA.
>
> Périmètre : module `TreasuryForecast`. Projection du solde de trésorerie sur 1 à 12 mois,
> scénarios probabilisés, alertes de tension.

---

## 1. Principe

Le module projette le solde de trésorerie à partir de données déjà présentes en base — créances
clients, dettes fournisseurs, effets de commerce, cycles de paie, échéancier fiscal, échéanciers
d'emprunt — auxquelles s'ajoutent des engagements récurrents saisis à la main.

**Les chiffres sont déterministes.** Le modèle de langage n'intervient qu'après le calcul, pour
deux choses : pondérer les probabilités des trois scénarios dans une borne étroite, et rédiger
alertes, facteurs d'influence et recommandations. Il ne produit jamais un montant ni une date.

---

## 2. Feature flag

`src/Backend/FactuTrust.API/appsettings.json` :

```jsonc
"TreasuryForecast": {
  "Enabled": false,                  // interrupteur maître — 503 sur toutes les routes si false
  "DefaultHorizonMonths": 6,
  "MaxHorizonMonths": 12,
  "ConfidenceZ": 1.96,
  "CacheTtlMinutes": 30,             // au-delà, une lecture déclenche un recalcul
  "HistoryMonthsForVolatility": 24,
  "MaxRecomputeRunsPerDay": 6,       // plafond des recalculs MANUELS (429 au-delà)
  "PayrollPaymentDayOfMonth": 28,
  "IncludeSalesOrderBacklog": false, // sources optionnelles : risque de double comptage
  "IncludePurchaseOrderCommitments": false,
  "IncludeRecurringJournalTemplates": false,
  "BackgroundRecomputeEnabled": false,
  "Ai": {
    "Enabled": false,
    "MaxProbabilityShiftPoints": 15, // borne de l'ajustement IA, en points
    "TimeoutSeconds": 45,
    "MaxInsights": 8
  }
}
```

Front (`environment.ts`) : `featureFlags.treasuryCashForecast`.
**Basculer les deux ensemble** : flag front à `true` avec le back à `false` donne un écran qui
affiche « module désactivé ».

---

## 3. Schéma

Migration `20260813190000_AddTreasuryCashForecast_Tenant` — 7 tables, strictement additive.
Script idempotent : [`docs/runbooks/sql/AddTreasuryCashForecast_Tenant.idempotent.sql`](../runbooks/sql/AddTreasuryCashForecast_Tenant.idempotent.sql).

| Table | Rôle |
|---|---|
| `CashFlowForecastRuns` | Une projection calculée (racine d'agrégat) |
| `CashFlowForecastLines` | Flux attendus datés, rattachés à leur source |
| `CashFlowForecastBuckets` | Agrégats mensuels |
| `CashFlowScenarios` | Optimiste / réaliste / pessimiste |
| `CashFlowForecastInsights` | Alertes, facteurs, recommandations |
| `RecurringCashCommitments` | Engagements récurrents saisis |
| `CashFlowForecastSettings` | Seuils de la jauge |

Un recalcul remplace le run précédent du même horizon (cascade sur les quatre tables enfants).

> Montants en `decimal(18,3)` et **non** en owned type `Money` : soldes et flux nets sont signés,
> or `Money.Subtract` lève une exception sur un résultat négatif.

---

## 4. Chaîne de calcul

```
ITreasuryPositionService        → solde d'ouverture (classe 5, hors 58x, hors brouillons)
ICashFlowSourceCollector × 8    → flux datés et probabilisés
CashFlowScenarioEngine          → buckets mensuels, intervalle, 3 scénarios
CashFlowAlertRules              → alertes et facteurs déterministes
ICashFlowAiAdvisor (optionnel)  → pondération bornée + rédaction
CashFlowForecastService         → assemblage et persistance (ITenantUnitOfWork)
```

### Collecteurs

| Collecteur | Source | Probabilité |
|---|---|---|
| `ClientReceivablesCollector` | Factures clients non soldées | Courbe par ancienneté (95 → 35 %) |
| `ClientEffetCollector` | Traites en portefeuille | `ClientEffetProbabilityPercent` (95 %) |
| `SupplierPayablesCollector` | Factures fournisseurs, net de RS | 100 % |
| `SupplierEffetCollector` | Traites fournisseurs | 100 % |
| `PayrollCollector` | Cycles de paie + charges | 100 % / 90 % si projeté |
| `FiscalObligationsCollector` | `FiscalScheduleEntry` non payées | 100 % |
| `LoanScheduleCollector` | `LoanScheduleLine` à échoir | 100 % |
| `RecurringCommitmentCollector` | Engagements saisis | 100 % |

L'échec d'un collecteur ne fait pas tomber la projection : il est journalisé et tracé dans
`InputsJson` (compteur `-2` ; `-1` = source désactivée).

### Règles anti-double-comptage

1. **Traite ↔ facture** — un règlement par traite est un `Payment`, il réduit donc déjà le reste dû
   de la facture ; le collecteur d'effets le reprend à sa date propre.
2. **Dette de paie ↔ cycle projeté** — un mois qui possède un cycle n'est jamais projeté.
3. **Charges sociales ↔ échéancier fiscal** — les charges ne sont émises que pour les mois qu'aucune
   obligation `CnssMonthlyRemittance` / `CnssDtsQuarterly` / `PayrollIrppWithholding` ne couvre.
4. **Retenue à la source** — les dettes fournisseurs sont retenues **nettes** ; la retenue est
   portée par l'échéancier fiscal.

### Retard de paiement observé

Seule partie « apprise » du moteur : le retard **médian** entre échéance et règlement effectif, par
client (au-delà de `MinInvoicesForClientDelayMedian` factures soldées), sinon la médiane société.
Médiane et non moyenne, pour qu'un litige isolé ne déplace pas tout le portefeuille. Plafonné par
`MaxPaymentDelayShiftDays`. Un règlement en avance ne raccourcit jamais la projection.

### Intervalle de prévision

Variance de Bernoulli des flux incertains (`Σ montant² × p × (1−p)`), **cumulée** d'un mois sur
l'autre car le solde de clôture accumule les aléas. Un mois dont tous les flux sont certains a un
intervalle nul.

---

## 5. Garde-fous de la couche IA

Portés par le domaine et par `CashFlowAiAdjustmentApplier`, chacun couvert par un test :

1. Chaque probabilité reste dans `[déterministe ± MaxProbabilityShiftPoints]`
   (`CashFlowScenario.ApplyAiProbability`).
2. La somme des trois vaut exactement 100 après renormalisation — laquelle se fait **au prorata de
   la marge restante**, jamais proportionnellement au total, sous peine de repousser une valeur
   hors de sa borne.
3. Une répartition incomplète est rejetée en bloc.
4. `DeterministicProbabilityPercent` est conservée et affichée en infobulle.
5. Échec, délai dépassé ou JSON invalide ⇒ repli silencieux sur le déterministe,
   `AiAdjustmentApplied = false`. **Un recalcul n'échoue jamais à cause du modèle.**

---

## 6. API

`/api/treasury/cash-forecast` — 503 partout quand le flag est éteint.

| Verbe & route | Policy |
|---|---|
| `GET /` | `TreasuryForecastView` |
| `POST /recompute` | `TreasuryForecastManage` (429 au-delà du plafond) |
| `GET /{runId}/lines` | `TreasuryForecastView` |
| `GET /{runId}/export` | `TreasuryForecastView` |
| `GET/POST/PUT/DELETE /commitments[/{id}]` | View / Manage |
| `GET/PUT /settings` | View / Manage |

Permissions : `treasury_forecast:view` / `treasury_forecast:manage`, accordées à Administrateur,
Superviseur, **Comptable**, Auditeur (vue seule) et, en cabinet délégué, en lecture seule.

> Le Comptable n'a pas `forecasting:view` : réutiliser les permissions du module Prévisions IA
> aurait rendu l'écran invisible à son utilisateur principal.

### Enregistrement DI

Les services sont enregistrés **inconditionnellement**, contrairement au module Prévisions IA :
ce module expose des handlers MediatR, que MediatR découvre par balayage d'assembly quel que soit
l'état du flag. Les laisser sans dépendances ferait échouer `ValidateOnBuild` au démarrage. Seul
`ICashFlowAiAdvisor` reste conditionnel — l'orchestrateur le reçoit en paramètre optionnel.

---

## 7. Écran

`src/app/features/treasury/` — route `/treasury/cash-forecast`, plus un onglet « Trésorerie » dans
le hub `/forecasting` qui monte le même composant.

Le bouton « Synchroniser les banques » de la maquette d'origine a été renommé
**« Importer un relevé bancaire »** et pointe vers le rapprochement existant : le produit ne
dispose d'aucune synchronisation bancaire automatique.

---

## 8. Tâche de fond

Job Hangfire `treasury-forecast-recompute`, **8 h UTC** (premier créneau libre). Itère les tenants
actifs, l'échec de l'un n'empêchant pas les autres. Sort immédiatement si
`Enabled` ou `BackgroundRecomputeEnabled` est faux. Les recalculs automatiques ne consomment pas le
quota journalier, réservé aux recalculs manuels.

---

## 9. Outils de l'assistant

`get_cash_flow_forecast` et `get_cash_flow_lines`, rattachés au scope `Treasury` existant
(persona « Expert Trésorerie »). Chaîne complète : `AiToolRegistry` → `AiAgentScopeCatalog`
(+ variante CPU) → `AiToolExecutor.Treasury.cs` → `AiToolFrenchLabels` → miroirs front
(`assistant-progress-display.ts`, `agent-scopes.config.ts`).

Analyse d'écran : `treasury-cash-forecast`, déclaré côté front
(`ai-screen-analysis-config.ts`, `ai-screen-labels.util.ts`, `ai-screen-analysis-prompts.ts`) et
côté back (`AiScreenTitleFormatter`, `AiScreenAnalysisToolHints`, `AiScreenAnalysisPromptBuilder`).

> `FirmDelegatedAiScopePolicy` refuse le scope `Treasury` en contexte cabinet délégué — comportement
> préexistant et voulu, laissé inchangé.

---

## 10. Tests

| Niveau | Fichier | Objet |
|---|---|---|
| Domaine | `Treasury/CashFlowForecastDomainTests.cs` | Invariants, chaînage des soldes, bornage IA |
| Moteur | `Treasury/CashFlowScenarioEngineTests.cs` | Découpage mensuel, intervalle, scénarios, reproductibilité |
| Collecteurs | `Treasury/CashFlowCollectorLogicTests.cs` | Occurrences récurrentes, dates de paie, médianes |
| IA | `Treasury/CashFlowAiAdjustmentApplierTests.cs` | Les cinq garde-fous |
| IA | `Treasury/CashFlowAiAdvisorParsingTests.cs` | Réponses malformées, prose autour du JSON |
| Comptes | `Treasury/TreasuryAccountRootsTests.cs` | Exclusion des 58x |
| API | `CashFlowForecastControllerContractTests.cs` | Policies, routes, sérialisation snake_case |
| Front | `cash-forecast.view-model.spec.ts` | Formatage millime, zones de jauge, tri des alertes |

```bash
dotnet test src/Backend/tests/FactuTrust.Infrastructure.Tests/FactuTrust.Infrastructure.Tests.csproj --filter "FullyQualifiedName~Treasury"
```

```bash
npx ng test --watch=false --browsers=ChromeHeadless --include='**/treasury/**/*.spec.ts'
```
