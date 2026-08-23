# Plan de correction et d'amélioration — Sous-module « Réapprovisionnement » (Prévisions IA)

> **Projet** : InstaFact / FactuTrust — `C:\Solution\FactuTrustCopy`
> **Date de l'analyse** : 16/08/2026
> **Périmètre** : onglet « Réapprovisionnement » du module Prévisions IA (`/forecasting/replenishment`), frontend Angular + backend .NET, sans toucher aux autres onglets (Prévision CA, Promotions, ABC/XYZ, Calendrier TN, Trésorerie) sauf dépendances explicites.
> **Objectif** : corriger les bugs constatés (captures d'écran à l'appui), améliorer la fiabilité métier et l'UX, **sans aucune régression** sur le code et les fonctionnalités existantes.

---

## 0. Résumé exécutif

Le sous-module est globalement bien architecturé (domaine riche, audit trail, permissions, tests existants, feature flags). L'analyse approfondie a néanmoins mis en évidence :

- **4 bugs critiques** qui produisent des **doublons de recommandations et un risque réel de double commande fournisseur** (visibles sur la capture 3 : mêmes produits en statuts `EN ATTENTE` / `COMMANDÉE` / `REMPLACÉE` simultanément) ;
- **6 bugs majeurs** (stock réservé ignoré, fournisseur manuel invisible, filtre « Urgence » qui fausse la pagination, KPI double-comptés, numérotation des BC non atomique, export CSV mal formaté) ;
- **une douzaine de points mineurs / UX** (dont deux fonctionnalités câblées de bout en bout mais sans interface : les notes utilisateur et les filtres par date) ;
- **des anomalies visuelles** sur les captures (barre de filtres empilée pleine largeur au lieu de la grille horizontale prévue, boîte vide sous « Recherche », « Non renseigné » sur toutes les lignes fournisseur).

Le plan est découpé en **6 phases (0 à 5)**, chaque tâche précise les fichiers, la modification exacte, les tests à ajouter/mettre à jour, les critères d'acceptation, les risques de régression et la marche arrière. Estimation globale : **8 à 10 jours de développement** hors recette métier.

**Principes directeurs anti-régression** : contrats API et schéma SQL **uniquement additifs**, un commit par constat, suite de tests existante comme filet de sécurité, aucune modification des 95 fichiers déjà modifiés dans l'arbre de travail (hors périmètre).

---

## 1. Méthode d'analyse

1. Lecture des 3 captures d'écran fournies (board, filtres, doublons de statuts).
2. Lecture intégrale du code frontend du module (`src/Frontend/factutrust-web/src/app/features/forecasting/`) : page `replenishment-board` (composant 579 lignes + template + SCSS), sous-composants `replenishment-kpi-bar` et `replenishment-filters`, 5 modales, 2 pipes, 2 services, modèles TypeScript, routes.
3. Lecture intégrale du backend : `ReplenishmentService` (773 lignes), `ForecastingController` (544 lignes), entité domaine `ReplenishmentRecommendation`, `PurchaseOrderDraftFactory`, `ForecastRecomputeOrchestrator`, `StatisticalForecasting.ComputeReplenishment`, configuration EF `TenantDbContext.Forecasting.cs`, options `ForecastingOptions`.
4. Vérifications croisées : entité `StockItem` (stock réservé), audit `ForecastRecomputeAudit` (casse `TriggerType`), contrôleur POS (les ventes caisse passent par les factures → couvertes par l'historique de demande), état Git (module forecasting propre, 95 fichiers modifiés hors périmètre), inventaire des tests existants.

---

## 2. Cartographie du sous-module

### 2.1 Frontend (Angular 18, standalone, signals, OnPush)

| Fichier | Rôle |
|---|---|
| `features/forecasting/pages/replenishment-board/replenishment-board.component.ts/.html/.scss` | Page principale : tableau, sélection, actions unitaires et en masse, modales |
| `…/replenishment-kpi-bar/replenishment-kpi-bar.component.ts` | 5 tuiles KPI (En attente, Urgent, Rupture, À commander, Taux de service) |
| `…/replenishment-filters/replenishment-filters.component.ts` | Filtres : statut, entrepôt, fournisseur, urgence, recherche (persistés en `localStorage`) |
| `components/prepare-po-confirm-modal/…` | Modale de création des BC : regroupement par fournisseur, assignation manuelle |
| `components/override-quantity-modal/…` | Modale de modification quantité / fournisseur |
| `components/dismiss-reason-modal/…` | Modale de rejet avec raison structurée |
| `components/decision-history-modal/…` | Frise chronologique des décisions (audit) |
| `components/product-demand-modal/…` | Prévision de demande produit + régénération ciblée |
| `services/forecasting.service.ts` | Passerelle REST unique ; normalise la casse des énumérations (`Pending` → `pending`) |
| `services/replenishment-export.service.ts` | Téléchargement CSV (Blob → ancre temporaire) |
| `pipes/quantity-format.pipe.ts` (`qtyFmt`) | Formatage fr-FR, 0 ou 2 décimales selon l'unité produit |
| `pipes/reason-code.pipe.ts` (`reasonCode`) | Traduction FR des codes raison (`OutOfStock`, `DailyDemand:x`…) |

### 2.2 Backend (.NET, Clean Architecture)

| Fichier | Rôle |
|---|---|
| `API/Controllers/ForecastingController.cs` | 11 endpoints réappro : liste paginée, approve, dismiss, override, undo, notes, create-purchase-orders, generate, history, kpi, export |
| `Infrastructure/Services/Forecasting/ReplenishmentService.cs` | Génération (ROP = d×L + SS, saisonnalité TN, MOQ, colisage), lectures, décisions, création des BC, export |
| `Infrastructure/Services/Forecasting/Statistics/StatisticalForecasting.cs` | Moteur déterministe (`ComputeReplenishment` : SS = Z·σ·√L) |
| `Infrastructure/Services/PurchaseOrders/PurchaseOrderDraftFactory.cs` | Construction des BC brouillons groupés par fournisseur (sans persistance) |
| `Domain/Entities/Forecasting/ReplenishmentRecommendation.cs` | Machine à états : Pending → Approved/Dismissed → Ordered/Superseded (+ Revert) |
| `Domain/Entities/Forecasting/ReplenishmentDecisionAudit.cs` | Audit de chaque décision |
| `Infrastructure/Persistence/TenantDbContext.Forecasting.cs` | Mapping EF + index |
| `Application/Configuration/ForecastingOptions.cs` | Flags et seuils (`UrgencyThresholdDays=3`, `UndoWindowHours=24`, `MaxPrepareBatchSize=200`…) |

### 2.3 Flux de données

```
Factures (lignes, 90 j) ─┐
StockItems (min/max) ────┤
Produits (fourn. préféré, MOQ, colisage, lead time) ─┼─► GenerateRecommendationsAsync
BC ouverts (reste à recevoir) ───────────────────────┘        │
                                                              ▼
                                    ReplenishmentRecommendations (Pending)
                                                              │
        approve / dismiss / override / undo / notes ◄─────────┤
                                                              ▼
                              create-purchase-orders ─► PurchaseOrder (Draft) + statut Ordered
```

---

## 3. Constats détaillés

Convention : **C** = critique, **M** = majeur, **m** = mineur, **V** = visuel (constaté sur captures). Chaque constat porte sa preuve (fichier:ligne) et son impact.

### 3.1 Bugs critiques

---

#### C1 — Les BC créés depuis le board n'ont **aucun entrepôt** → double commande et doublons

**Preuve** :
- `PurchaseOrderDraftFactory.cs:91` : `PurchaseOrder.Create(…, warehouseId: null)`.
- `ReplenishmentService.cs:122` : le calcul du « en commande » ne retient que les BC dont `WarehouseId.HasValue` — les BC créés par le module sont donc **exclus** du stock en commande.

**Impact** : après « Créer les BC », la régénération suivante (manuelle ou job nocturne) **recrée une recommandation identique** puisque le stock en commande n'est pas déduit → doublons exactement visibles sur la capture 3 (même produit en `EN ATTENTE` + `COMMANDÉE` + `REMPLACÉE`), **risque de double approvisionnement**, et BC sans entrepôt de réception (impact en cascade sur la réception marchandise).

**Cause racine** : regroupement des lignes par fournisseur seul, sans tenir compte de l'entrepôt de chaque recommandation.

**Correctif** : regrouper par **(fournisseur, entrepôt)** et passer `warehouseId` du groupe à `PurchaseOrder.Create`. Un BC par couple fournisseur × entrepôt.

---

#### C2 — La quantité recommandée ignore les quantités déjà en commande

**Preuve** :
- `ReplenishmentService.cs:170-186` : le **déclenchement** utilise `effectiveQty = onHand + onOrder`, mais la **quantité** vient de `StatisticalForecasting.ComputeReplenishment` qui ne reçoit que `quantityOnHand` (`StatisticalForecasting.cs:449-456` : `qty = maxStock − onHand`, `minQty = rop − onHand`).

**Impact** : quand un BC est en cours mais que le stock effectif reste ≤ ROP, la quantité proposée **ne déduit pas l'en-commande** → sur-approvisionnement. Aggrave C1.

**Correctif** : paramètre **additif** `quantityOnOrder = 0` sur `ComputeReplenishment` ; calculer `qty` et `minQty` sur la base `onHand + onOrder`. Signature existante conservée (compatibilité des appels et tests actuels).

---

#### C3 — Le filtre « Urgence » est appliqué **après** la pagination → compteurs et pages faux

**Preuve** : `ReplenishmentService.cs:287-295` — `TotalCount` est le compte SQL **non filtré**, puis le filtre `UrgencyLevel` est appliqué en mémoire sur la page courante (le commentaire du code l'admet).

**Impact** : pages partielles ou vides, « 0 / 34 sélectionnée(s) » incohérent, export filtré par urgence tronqué, tri + filtre combinés imprévisibles.

**Correctif (option B retenue, sans migration)** : traduire `ComputeUrgency` en SQL — la requête joindra `StockItems` (déjà jointe pour la recherche… à ajouter proprement) et appliquera : `onHand <= 0 → OutOfStock`, `days < seuil → Urgent`, `days < leadTime×2 → Warning`, sinon `Normal`, **avant** `CountAsync` et la pagination. *Option A (avec migration) : persister `UrgencyLevel` à la génération — à retenir seulement si le métier veut figer l'urgence (voir Q2, §9).*

---

#### C4 — Génération non protégée contre la concurrence → doublons structurels

**Preuve** : `GenerateRecommendationsAsync` supersède puis insère sans verrou ; aucun index unique ne garantit « 1 seule recommandation Pending par (produit, entrepôt) » (`TenantDbContext.Forecasting.cs:99-110`). Le job nocturne (`ForecastRecomputeOrchestrator`) peut chevaucher un clic « Régénérer » ou « Recalculer tout ».

**Impact** : deux exécutions concurrentes insèrent chacune leurs recommandations → doublons `EN ATTENTE` stricts (pas seulement multi-statuts).

**Correctif** :
1. **Index unique filtré** SQL Server : `(ProductId, WarehouseId) WHERE Status = 'Pending'` (migration additive) + **script de déduplication préalable** des données existantes (conserver la plus récente, superséder les autres).
2. Côté service : intercepter la violation d'unicité (2601/2627) → reprise idempotente (relecture + mise à jour) au lieu d'un 500.
3. Verrou applicatif par tenant (sémaphore en mémoire) autour de la génération, à l'image du throttle déjà présent sur le recalcul.

---

### 3.2 Bugs majeurs

---

#### M1 — Le stock **réservé** est ignoré par le calcul

**Preuve** : `ReplenishmentService.cs:82` utilise `s.QuantityOnHand` ; le domaine expose `StockItem.QuantityAvailable => QuantityOnHand − QuantityReserved` (`StockItem.cs:19`), jamais utilisé par la génération.

**Impact** : un stock entièrement réservé pour des clients paraît « disponible » → **sous-approvisionnement**.

**Correctif** : sélectionner `QuantityOnHand − QuantityReserved` (borné ≥ 0) comme stock de référence de la génération, derrière une option `Replenishment:UseAvailableStock` (défaut proposé `true` après validation métier — voir Q1, §9). Champ `currentStockOnHand` du DTO inchangé (affichage du stock physique) ; la logique de déclenchement utilise le disponible.

---

#### M2 — Le fournisseur **manuel** (override) est invisible partout

**Preuve** :
- Le DTO expose `ManualSupplierOverride` (Guid) mais **pas son nom** ; `MapRowsAsync` (`ReplenishmentService.cs:700-723`) ne résout que `PreferredSupplierName`.
- Le template (`replenishment-board.component.html:161-169`) n'affiche que `preferredSupplierName` → après une assignation manuelle, la ligne affiche toujours « — Non renseigné — » (ou l'ancien fournisseur).

**Impact** : utilisateur induit en erreur ; capture 1-2 : « Non renseigné » sur 100 % des lignes (données manquantes **et** override invisible).

**Correctif** : ajout **additif** de `manualSupplierName` au DTO (résolution par dictionnaire en une requête), affichage du **fournisseur effectif** (manuel > préféré) avec icône « modifié » ; recherche étendue au fournisseur manuel (m8).

---

#### M3 — Numérotation des BC non atomique

**Preuve** : `PurchaseOrderDraftFactory.cs:53-55` : lecture du dernier numéro puis `Next()` en mémoire, hors de toute transaction de réservation.

**Impact** : deux utilisateurs « Créer les BC » en simultané → collision de numéro (échec ou doublon selon la contrainte d'unicité).

**Correctif** : investiguer le service de numérotation documentaire existant (`NumberingController`, scripts `write-numbering-backend.ps1`) et l'utiliser avec réservation transactionnelle ; à défaut, retry sur violation d'unicité. **Ne pas** réinventer un compteur.

---

#### M4 — KPI « Urgent » et « Rupture » se chevauchent

**Preuve** : `ReplenishmentService.cs:327-332` — `urgent` compte `DaysOfStockRemaining < seuil` **sans exclure** `EffectiveQty == 0` ; une rupture avec demande est comptée deux fois (capture 1 : Urgent 2 + Rupture 1).

**Correctif** : segments exclusifs — `rupture = Pending ∧ EffectiveQty = 0` ; `urgent = Pending ∧ EffectiveQty > 0 ∧ days < seuil`. Mêmes champs DTO, valeurs corrigées (additif, documenté en notes de version).

---

#### M5 — Bandeau « Dernier calcul » : toujours « Automatique »

**Preuve** : backend stocke `TriggerType = "Manual"` (`ForecastRecomputeAudit.cs:40`) ; le hub compare `a.triggerType === 'manual'` (`forecasting-hub.component.ts:77`) — sensible à la casse → jamais vrai.

**Correctif** : normaliser `triggerType` dans `ForecastingService` (pattern `lowerFirst` déjà utilisé pour les statuts) ou comparer sans casse. Étendre `RecomputeAudit` côté front en conséquence.

---

#### M6 — Export CSV : culture et limite

**Preuve** : `ReplenishmentService.cs:631` (cap 1000 lignes silencieux), `:750-758` (nombres en culture invariante → `10.59` avec point, mal parsé par Excel FR qui attend la virgule), colonne fournisseur = préféré seul.

**Correctif** : décimaux en `fr-FR` (séparateur `;` déjà correct pour Excel FR/TN), avertissement explicite si > 1000 lignes (ou export paginé), colonne « Fournisseur effectif ».

---

### 3.3 Points mineurs et dette

| ID | Constat | Preuve | Correctif proposé |
|---|---|---|---|
| m1 | `ProcessedAt` est muté par override **et** notes → la fenêtre d'annulation de 24 h (`UndoLastDecisionAsync`, service `:471-474`) se prolonge à chaque note | entité `ApplyManualOverride`/`AttachNotes` | Baser la fenêtre sur le **dernier audit** `Approve`/`Dismiss` (`ActedAt`) — zéro migration |
| m2 | Endpoint `POST …/notes` + méthode front `attachNotesReplenishment` **sans aucune UI** | service front `:131-135` | Bouton « note » par ligne + modale (pattern de la modale de rejet) |
| m3 | Filtres `fromGeneratedAt`/`toGeneratedAt` câblés back + front service, **absents de l'UI filtres** | `forecasting.service.ts:85-86` | Deux champs date dans `replenishment-filters` |
| m4 | Montant « À commander » affiché via `qtyFmt` (2 déc.) alors que le TND se facture au millime (3 déc.) ; vérifier le séparateur de milliers (« 21491,83 » sur capture) | kpi-bar `:37` | Pipe montant dédié (fr-FR, 3 décimales, devise) |
| m5 | « 0 / 34 sélectionnée(s) » mélange sélection **page courante** et total global | board `.html:18` | Libellé « X sélectionnée(s) sur Y affichées · Z au total » |
| m6 | Listes fournisseurs plafonnées à 200 (filtres + 2 modales) | filters `:176`, modales | Recherche serveur (autocomplete) si > 200 — **optionnel** |
| m7 | Jours de stock calculés sur `onHand` seul (pas l'effectif) | service `:216` | Décision métier (Q3) ; si oui : `effectiveQty / dailyDemand` |
| m8 | La recherche SQL ne matche que `PreferredSupplierName`, pas le fournisseur manuel | service `:269-273` | Inclure le nom du fournisseur manuel (lié à M2) |
| m9 | `MarkSuperseded` n'écrit **aucun audit** → perte de traçabilité des « Remplacée » | entité `:197-201` | Ligne d'audit `Generate`/`Supersede` (l'enum front connaît déjà `Generate`) |
| m10 | `approve()` appelle `unselect` deux fois | board `.ts:286,295` | Nettoyage cosmétique |
| m11 | « Régénérer » supersède **toutes** les recommandations en attente du périmètre sans confirmation | board `.ts:492-514` | Modale de confirmation + résumé d'impact (X remplacées, Y créées) |
| m12 | Demande = factures uniquement ; vérifier que les **avoirs** ne gonflent pas la demande (signe des quantités) | service `:99-107` | Test dédié ; exclure/soustraire si besoin |

### 3.4 Anomalies visuelles (captures)

| ID | Constat | Analyse |
|---|---|---|
| V1 | Filtres **empilés pleine largeur** (Statut/Entrepôt/Fournisseur/Urgence/Recherche l'un sous l'autre) alors que le composant prévoit une grille horizontale (`grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)) auto`) | Le rendu ne correspond pas aux styles embarqués du composant → divergence probable entre l'application servie (localhost:4200) et ce checkout, ou styles non appliqués. **Phase 0 : inspecter le DOM** ; namespacer les classes génériques (`.filters`, `.field`, `.btn-reset`) en `.repl-*` (précédent interne : le namespacing `.po-*` de la modale BC suite à un bug de collision) |
| V2 | **Boîte vide** sous le champ « Recherche » | Probablement le bouton reset (icône `pi-filter-slash` non rendue) étiré pleine largeur, ou un champ d'un build différent. Même plan d'action que V1 (inspection DOM puis correctif) |
| V3 | Doublons multi-statuts par produit (capture 3) | Conséquence directe de **C1 + C4** — disparaît avec les correctifs Phase 1 |
| V4 | « Non renseigné » sur toutes les lignes fournisseur | Données : produits sans `PreferredSupplierId` → campagne de saisie + raccourci « Définir le fournisseur préféré » (lien fiche produit) ; code : M2 |

---

## 4. Matrice de priorisation

| Priorité | Constats | Justification |
|---|---|---|
| **P0 — Phase 1** | C1, C2, C4, M3 | Risque financier direct (double commande), intégrité des données |
| **P1 — Phase 2** | C3, M1, M2, M4, M5(back), M6, m1, m8, m9 | Exactitude métier et traçabilité |
| **P2 — Phase 3** | M5(front), m2, m3, m4, m5, m10, m11, V1, V2, V4 | UX et fiabilité perçue |
| **P3 — Phase 4** | m6, m7, m12, verrous/perf | Robustesse long terme |

---

## 5. Plan d'exécution détaillé

> **Règles communes à toutes les phases** :
> - Branche dédiée `fix/replenishment-hardening` créée depuis `HEAD` ; **ne jamais inclure** les 95 fichiers déjà modifiés hors périmètre (`git add` ciblé, fichier par fichier).
> - **Un commit par constat** (message : `fix(forecasting): C1 — BC rattaché à l'entrepôt`), pour rollback individuel par `git revert`.
> - Contrats API et schéma : **strictement additifs** (nouveaux champs nullables, nouveaux index, aucun renommage/retrait).
> - Après chaque tâche : build + tests ciblés verts avant de passer à la suivante.

### Phase 0 — Cadrage et garde-fous (0,5 j)

| # | Tâche | Détail | Critère d'acceptation |
|---|---|---|---|
| 0.1 | Baseline verte | `dotnet build FactuTrust.sln` ; `dotnet test` filtré `Forecasting|Replenishment|PurchaseOrderDraft` ; `npm run build` ; `ng test` ciblé forecasting. Consigner les résultats dans `tmp_verify_pr/baseline-replenishment.txt` | 0 échec ; tout échec préexistant est **documenté** et exclu explicitement du périmètre |
| 0.2 | Vérifier app servie = checkout | Comparer le DOM de la barre de filtres (localhost:4200) avec `replenishment-filters.component.ts` ; trancher V1/V2 | Cause de V1/V2 identifiée et journalisée |
| 0.3 | Arbitrages métier | Réponses aux questions Q1-Q4 (§9) ou application des défauts documentés | Décisions consignées en tête de PR |
| 0.4 | Procédure migration EF | Identifier la commande du projet (tenant migrations sous `Infrastructure/Migrations/Tenant`) | Commande validée sur une migration vide de test, puis annulée |

### Phase 1 — Correctifs critiques backend (2 à 3 j)

#### Tâche 1.1 — C1 : BC rattaché à l'entrepôt (groupement fournisseur × entrepôt)

- **Fichiers** : `PurchaseOrderDraftFactory.cs`, `IPurchaseOrderDraftFactory.cs` (si signature), tests `PurchaseOrderDraftFactoryTests.cs`, `ForecastingControllerCreatePurchaseOrdersTests.cs`.
- **Modification** : `recommendations.GroupBy(r => new { SupplierId = r.GetEffectiveSupplierId(), r.WarehouseId })` ; passer `warehouseId: group.Key.WarehouseId` à `PurchaseOrder.Create` ; adapter le libellé des warnings.
- **Tests** : (a) 2 entrepôts × 1 fournisseur → **2 BC** avec le bon `WarehouseId` chacun ; (b) régression : 1 fournisseur × 1 entrepôt → 1 BC (comportement actuel préservé) ; (c) les warnings « sans fournisseur » inchangés.
- **Risque de régression** : consommateurs du nombre de BC créés (toast front `${createdPurchaseOrdersCount} BC créé(s)`) — reste exact, le compte augmente simplement ; vérifier le test d'intégration API existant.
- **Rollback** : `git revert` du commit.

#### Tâche 1.2 — C2 : quantité tenant compte de l'en-commande

- **Fichiers** : `StatisticalForecasting.cs` (`ComputeReplenishment`), `ReplenishmentService.cs:170-186`, `StatisticalForecastingTests.cs`.
- **Modification** : surcharge **additif** `decimal quantityOnOrder = 0` ; `available = quantityOnHand + quantityOnOrder` ; `qty = maxStock > 0 ? max(0, maxStock − available) : max(0, 2·d·L)` ; `minQty = max(0, rop − available)`. L'appel existant à 6 arguments continue de compiler (valeur par défaut).
- **Tests** : (a) cas actuels inchangés quand `onOrder = 0` ; (b) `onHand=2, onOrder=5, ROP=8` → qty déduit les 5 ; (c) garde-fous domaine (pas de négatif).
- **Risque** : valeurs de recommandation différentes après déploiement — **comportement voulu**, à annoncer en notes de version ; aucun test existant ne doit casser si `onOrder=0`.

#### Tâche 1.3 — C4 : unicité « Pending » + concurrence

- **Fichiers** : `TenantDbContext.Forecasting.cs` (index filtré), nouvelle migration `Tenant`, `ReplenishmentService.cs` (reprise sur violation), script SQL de déduplication joint à la migration.
- **Modification** :
  1. `entity.HasIndex(e => new { e.ProductId, e.WarehouseId }).HasFilter("[Status] = 'Pending'").IsUnique()` — vérifier la sérialisation exacte de `Status` (string vs int) avant d'écrire le filtre.
  2. Migration : avant `CreateIndex`, SQL de dédup (garder la ligne Pending la plus récente par couple, passer les autres à `Superseded`).
  3. Service : `catch (DbUpdateException) when (IsUniqueViolation)` → relecture de la ligne existante, mise à jour des champs calculés, pas d'insert.
  4. `SemaphoreSlim` statique par tenant autour de `GenerateRecommendationsAsync`.
- **Tests** : (a) génération ×2 concurrente (tasks parallèles) → 1 seule Pending par couple ; (b) dédup idempotente ; (c) migration up/down sur base de test.
- **Risque** : la dédup modifie des données existantes → script relu, exécuté dans la transaction de migration, sauvegarde base recommandée avant déploiement.

#### Tâche 1.4 — M3 : numérotation atomique des BC

- **Investigation préalable** (0,25 j) : cartographier le service de numérotation (`NumberingController`, scripts `write_numbering*`) ; décider réutilisation vs retry sur unicité.
- **Critère** : 2 appels concurrents `create-purchase-orders` → numéros distincts garantis, test d'intégration à l'appui.

### Phase 2 — Correctifs majeurs backend (2 j)

| # | Constat | Modification (fichiers) | Tests |
|---|---|---|---|
| 2.1 | C3 | Filtre urgence traduit en SQL dans `GetRecommendationsAsync` **avant** comptage/pagination (jointure `StockItems` pour `onHand`, paramètres seuil/leadTime) ; supprimer le post-filtre mémoire. `ReplenishmentService.cs:254-304` | Intégration : filtre `Urgent` → `TotalCount` exact, pages pleines ; régression : sans filtre urgence, résultats identiques à l'actuel |
| 2.2 | M1 | `QuantityAvailable` (borné ≥ 0) dans le snapshot de génération + option `Replenishment:UseAvailableStock` (défaut selon Q1). `ReplenishmentService.cs:73-89` | Génération avec réservation → déclenchement anticipé ; option `false` → comportement strictement actuel |
| 2.3 | M2 | `manualSupplierName` additif au DTO + résolution dans `MapRowsAsync` (un dictionnaire, une requête). `ForecastingDtos.cs`, `ReplenishmentService.cs`, `ReplenishmentDtoSerializationTests.cs` | Sérialisation : nouveau champ présent, anciens inchangés ; mapping override → nom |
| 2.4 | M4 | KPI segments exclusifs (`urgent` exclut `EffectiveQty=0`). `ReplenishmentService.cs:327-332` | Jeu de données contrôlé : rupture comptée une seule fois |
| 2.5 | M6 | Export : décimaux `fr-FR`, colonne « Fournisseur effectif », message si > 1000 lignes. `ReplenishmentService.cs:626-670` | Test contenu CSV (en-tête BOM, quoting, culture) |
| 2.6 | m1 | Fenêtre d'undo basée sur le dernier audit `Approve`/`Dismiss` (`ActedAt`) au lieu de `ProcessedAt`. `ReplenishmentService.cs:465-486` | Override récent + approve ancien → undo refusé ; approve récent → undo accepté |
| 2.7 | m8 | Recherche étendue au nom du fournisseur manuel (jointure `Suppliers` sur `ManualSupplierOverride`). `ReplenishmentService.cs:266-273` | Recherche par nom de fournisseur manuel → ligne trouvée |
| 2.8 | m9 | Audit `Generate`/`Supersede` écrit lors de la supersession (boucle déjà en place service `:66-70`). | Historique d'une ligne remplacée → trace visible dans la modale d'historique |

### Phase 3 — Frontend : affichage et UX (2 à 3 j)

| # | Constat | Modification (fichiers) | Tests / validation |
|---|---|---|---|
| 3.1 | M2/V4 | Colonne « Fournisseur » = effectif (manuel > préféré) + icône override ; bouton/lien « Définir un fournisseur préféré » (modale override ou fiche produit). `replenishment-board.component.html:160-170`, modèle TS | Spec composant : rendu des 3 cas (préféré / manuel / absent) |
| 3.2 | M5 | Normalisation `triggerType` (lowerFirst) dans `forecasting.service.ts` ; typage `RecomputeAudit`. | Spec service : `"Manual"` → `'manual'` ; hub affiche « Manuel » |
| 3.3 | m2 | Bouton « note » (icône) par ligne + modale d'édition réutilisant `attachNotesReplenishment` ; badge si note présente. Nouveau composant + board | Spec modale (valide/vide/annule) ; appel API moqué |
| 3.4 | m3 | Champs « Générées du / au » dans `replenishment-filters` (déjà supportés de bout en bout) ; inclus dans la persistance `localStorage`. | Spec filtres : émission du patch ; requête HTTP contient les paramètres |
| 3.5 | m4 | Pipe `tndAmount` (fr-FR, 3 décimales) pour « À commander » ; vérifier le groupement des milliers (V « 21491,83 »). kpi-bar | Spec pipe : `21491,83` → `21 491,830` |
| 3.6 | m5 | Libellé sélection : « X sélectionnée(s) sur Y affichées · Z au total ». board `.html:17-19` | Revue visuelle |
| 3.7 | m11 | Confirmation avant « Régénérer » (modale maison, focus trap existant) + toast détaillé (créées/remplacées — nécessite le compte de supersédées, additif côté API si retenu). | Spec : annulation = aucun appel HTTP |
| 3.8 | V1/V2 | Namespacing des classes de `replenishment-filters` (`.repl-filters`, `.repl-field`, `.repl-reset`) ; vérifier le chargement PrimeIcons ; corriger la boîte vide selon le diagnostic 0.2. | Validation visuelle 1920 px + 768 px ; aucune classe générique résiduelle |
| 3.9 | m10 | Suppression du `unselect` redondant. board `.ts` | Spec existante verte |
| 3.10 | m6 (optionnel) | Autocomplete fournisseur avec recherche serveur si > 200. | Décision en phase 0 |

### Phase 4 — Robustesse et performance (1 j)

| # | Objet | Détail |
|---|---|---|
| 4.1 | Verrou de génération | Si non livré en 1.3 : sémaphore par tenant + test de concurrence |
| 4.2 | Perf lectures | KPI « top urgencies » : filtrer le chargement `StockItems` par entrepôt (`ReplenishmentService.cs:356-358`) ; revue des requêtes générées (logs EF) sur le board |
| 4.3 | m7 | Jours de stock sur effectif — selon réponse Q3 |
| 4.4 | m12 | Test « avoir » : quantités négatives ou exclusion → demande non gonflée |
| 4.5 | Données | Backfill léger des `PreferredSupplierName` obsolètes (ou acceptation : rafraîchi à la prochaine génération) |

### Phase 5 — Qualité, documentation, recette (1 j)

1. **Suites complètes** : `dotnet test` (tout) + `ng test` (tout) + `npm run lint` + build de production ; e2e Playwright ciblés forecasting si existants.
2. **Revue de code** avec la checklist §6.4.
3. **Documentation** : mise à jour `docs/utilisateur/06-stock.md` (ou page dédiée Prévisions IA) : cycle de vie d'une recommandation, signification des raisons/KPI, règle « 1 BC par fournisseur × entrepôt » ; notes de version (changement de calcul des quantités C2, KPI M4).
4. **Recette manuelle** : scénarios §7 exécutés et consignés (captures).
5. **PR** : description liant chaque commit à son constat (C1…m12), risques et rollbacks.

---

## 6. Stratégie anti-régression

### 6.1 Contrats et schéma
- API : champs **ajoutés** uniquement (`manualSupplierName`, éventuellement compteurs de génération) ; `ReplenishmentDtoSerializationTests` doit passer sans modification de ses attentes existantes.
- EF : migration **additive** (index filtré uniquement) ; pas de renommage de colonne ; `TenantDbContextModelSnapshot` régénéré proprement.
- Flags existants (`Forecasting:Enabled`, `AutoCreatePurchaseOrders`…) : **non modifiés** ; la nouvelle option `UseAvailableStock` est livrée avec sa valeur par défaut documentée.

### 6.2 Filet de tests
- Back (existants, doivent rester verts) : `ReplenishmentRecommendationTests`, `ReplenishmentDecisionAuditTests`, `StatisticalForecastingTests`, `PurchaseOrderDraftFactoryTests`, `ProductReplenishmentTests`, `ForecastingControllerCreatePurchaseOrdersTests`, `ReplenishmentDtoSerializationTests`.
- Front (existants) : `replenishment-board.component.spec.ts`, `forecasting.service.spec.ts`, `prepare-po-confirm-modal.component.spec.ts`, `dismiss-reason-modal.component.spec.ts`, `quantity-format.pipe.spec.ts`, `reason-code.pipe.spec.ts`, `forecasting.routes.spec.ts`, `focus-trap.directive.spec.ts`.
- Chaque correctif ajoute **au moins un test** qui échoue avant et passe après (preuve de non-régression incluse dans le commit).

### 6.3 Données et déploiement
- Script de déduplication relu à deux ; sauvegarde de la base tenant avant migration ; migration testée up **et** down sur une copie.
- Comportements modifiés **volontairement** (quantités C2, KPI M4, disponible M1) : listés en notes de version avec exemple avant/après.

### 6.4 Checklist revue de code (par commit)
- [ ] Aucun fichier hors périmètre dans le diff
- [ ] Aucun changement de signature publique non additif
- [ ] Casse des énumérations cohérente (normalisation centralisée)
- [ ] Nouveaux textes en français, accessibilité (aria) préservée
- [ ] Aucun `console.log` / code commenté mort
- [ ] Tests : nouveau(x) + existants verts

---

## 7. Checklist de validation finale (recette)

| # | Scénario | Attendu |
|---|---|---|
| 1 | Régénérer deux fois de suite | Aucun doublon `EN ATTENTE` ; anciennes lignes en « Remplacée » avec audit |
| 2 | Créer les BC (2 entrepôts, 1 fournisseur) | 2 BC brouillons, chacun avec son entrepôt ; lignes liées en « Commandée » ; régénération suivante : **pas** de nouvelle recommandation pour ces produits |
| 3 | Filtre « Urgent » | Compteur total exact, pagination pleine, export cohérent |
| 4 | Assigner un fournisseur manuel | Colonne fournisseur affiche le nom + icône ; recherche par ce nom ; BC créé chez ce fournisseur |
| 5 | KPI | Rupture et Urgent exclusifs ; montant TND à 3 décimales avec séparateur de milliers |
| 6 | Undo | Possible < 24 h après approve ; refusé après override récent sur décision ancienne (règle m1) |
| 7 | Notes | Ajout/édition/effacement ; visible dans l'historique |
| 8 | Export CSV | Ouverture Excel FR correcte (virgules, BOM) ; avertissement si > 1000 |
| 9 | Hub | « Manuel » après « Recalculer tout » ; 429 au-delà du quota quotidien |
| 10 | Responsive 768 px | Tableau en cartes, filtres utilisables |
| 11 | Permissions | Rôle sans `forecasting:manage` : aucune action d'écriture (boutons masqués, API 403) |
| 12 | Régression globale | `dotnet test` + `ng test` + builds : 100 % verts |

---

## 8. Estimation

| Phase | Contenu | Charge |
|---|---|---|
| 0 | Cadrage, baseline, arbitrages | 0,5 j |
| 1 | C1, C2, C4, M3 (back) | 2,5 j |
| 2 | C3, M1, M2, M4, M6, m1, m8, m9 (back) | 2 j |
| 3 | UX front (10 tâches) | 2,5 j |
| 4 | Robustesse, perf, données | 1 j |
| 5 | Qualité, docs, recette | 1 j |
| **Total** | | **≈ 9,5 j** |

---

## 9. Questions ouvertes au métier (défauts appliqués si pas de réponse)

- **Q1 — Stock réservé** : la génération doit-elle se baser sur le stock **disponible** (physique − réservé) ? *Défaut proposé : oui* (option `UseAvailableStock=true`).
- **Q2 — Urgence figée ou live** : le niveau d'urgence doit-il être figé à la génération (option A, migration) ou recalculé à la lecture (option B retenue, sans migration) ? *Défaut : option B.*
- **Q3 — Jours de stock** : faut-il inclure l'en-commande dans la couverture affichée ? *Défaut : non (conserver l'actuel), l'en-commande reste visible dans la modale de modification.*
- **Q4 — Avoirs** : confirmer que les avoirs ne doivent pas alimenter la demande (ils seraient soustraits/exclus). *Défaut : vérification par test, exclusion si gonflage avéré.*

---

## 10. Annexes — commandes de référence

```powershell
# Backend — build et tests ciblés puis complets
cd C:\Solution\FactuTrustCopy\src\Backend
dotnet build FactuTrust.sln
dotnet test tests/FactuTrust.Infrastructure.Tests --filter "FullyQualifiedName~Forecasting|FullyQualifiedName~Replenishment|FullyQualifiedName~PurchaseOrderDraft"
dotnet test

# Frontend — build, lint, tests
cd C:\Solution\FactuTrustCopy\src\Frontend\factutrust-web
npm run build
npm run lint
ng test --include "**/forecasting/**"

# Migration EF (à confirmer en phase 0.4 — projet Infrastructure, contexte Tenant)
dotnet ef migrations add <Nom> --project src/Backend/FactuTrust.Infrastructure --startup-project src/Backend/FactuTrust.API --context TenantDbContext
```

*Fin du plan.*
