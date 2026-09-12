# Vues enregistrées du Studio (PR 2.3)

> État : livré (backend). Frontend : PR 2.5. Outils IA : PR 2.4. Export : PR 3.3.
> Drapeau : `Ollama:EnableStudioRecordViews` (défaut C# `false`, `true` dans `appsettings*.json`).
> Migration tenant : `20260912140000_AddStudioRecordViews_Tenant` (additive, inerte drapeau coupé).

## Vue d'ensemble

Une **vue enregistrée** est une définition réutilisable d'affichage des enregistrements d'une table
Studio (`CustomEntityDefinition`) : **Liste**, **Kanban** ou **Calendrier**, avec colonnes, filtres, tri
et recherche. Elle est exécutée **côté serveur** en SQL paramétré sur la colonne `DataJson`
(`JSON_VALUE` / `TRY_CONVERT`, colonnes calculées indexées `jx_*`).

À ne pas confondre avec `CustomViewDefinition` (`StudioViewsController`, `api/studio/views`) : ce dernier
est une **fenêtre SQL en lecture seule sur une table ERP existante** — un concept différent, d'où le
nommage `RecordView` partout (entité, DTO, routes, audit).

## Modèle

`CustomRecordViewDefinition` (table `CustomRecordViewDefinitions`) :

| Colonne | Rôle |
|---|---|
| `Id`, `TenantId`, `EntityDefinitionId` | identité + isolation tenant + rattachement à la table (FK cascade) |
| `Key` (64), `DisplayName` (128) | clé machine unique par (tenant, table) parmi les vues non supprimées ; libellé |
| `Mode` (int) | `0 = List`, `1 = Kanban`, `2 = Calendar` (sérialisé en chaîne dans l'API) |
| `DefinitionJson` (nvarchar(max)) | définition typée camelCase (`RecordViewDefinition`) |
| `IsDefault`, `IsActive` | vue par défaut (exclusif) / activée |
| `IsDeleted`, `DeletedAt` | suppression logique (R12 : aucune promotion automatique d'une autre vue) |
| `RowVersion` | concurrence optimiste des mises à jour (409) |

Index : unique filtré `(TenantId, EntityDefinitionId, Key) WHERE IsDeleted = 0`, et
`(TenantId, EntityDefinitionId, IsDefault)`.

## Définition JSON (`RecordViewDefinition`)

```json
{
  "columns":  [{ "fieldKey": "nom", "width": 200, "hidden": false }],
  "filters":  [{ "fieldKey": "statut", "op": "eq", "value": "encours" }],
  "sort":     [{ "fieldKey": "debut", "descending": true }],
  "kanban":   { "groupByFieldKey": "statut", "titleFieldKey": "nom", "cardFieldKeys": ["montant"], "columnOrder": null, "showEmptyGroup": true },
  "calendar": { "startFieldKey": "debut", "endFieldKey": null, "titleFieldKey": "nom", "colorFieldKey": "statut" },
  "searchEnabled": true,
  "pageSize": 25
}
```

`op` ∈ `eq | neq | contains | gt | gte | lt | lte | in | is_empty | is_not_empty | between`.
`RecordViewDefinitionValidator` impose : bornes **25 colonnes / 10 filtres / 3 tris / pageSize 1..200** ;
chaque `fieldKey` ∈ champs actifs (ou `createdAt` / `updatedAt` en colonne/tri) ; compatibilité
opérateur / type (`contains` → textes/select ; `gt…between` → nombres/dates ; `in` → select/multi/
relations) ; `kanban.groupByFieldKey` → `Select` ; `calendar.startFieldKey`/`endFieldKey` →
`Date`/`DateTime` ; champs calculés (`Formula`/`Lookup`/`Rollup`/`AutoNumber`) autorisés en colonne,
**refusés en filtre/tri** (coût non borné).

## Exécution (`RecordQuerySql` + `CustomRecordRepository.QueryAsync`)

`RecordQuerySql.Build` est un **constructeur SQL pur** (testable sans base) : `WHERE` / `ORDER BY` /
paramètres. Garanties :

- `TenantId`, `EntityDefinitionId`, `IsDeleted = 0` **toujours** dans le `WHERE` ;
- chaque clé est revérifiée par `StudioKey.IsValidShape` (sinon `ArgumentException`) ; les chemins JSON
  ne contiennent que ces clés validées ; **aucune valeur utilisateur n'est concaténée** (paramètres
  `@pN` typés `SqlDbType`) ;
- expression par type : textes → `JSON_VALUE(DataJson, '$.<clé>')` ; numériques →
  `TRY_CONVERT(decimal(18,6), JSON_VALUE(...))` ; dates → `TRY_CONVERT(datetime2, JSON_VALUE(...), 127)`
  (ISO 8601) ; booléen `eq` → `IN ('true','1')` ; `in` MultiSelect → `EXISTS (SELECT 1 FROM OPENJSON(...))` ;
- égalité sur clé indexée (≤ 450 caractères) : seek `[jx_<clé>] = @pN` + revérification exacte par
  `JSON_VALUE` (résolution des colonnes présentes via `IJsonIndexManager`, best-effort) ;
- tri par défaut `[CreatedAt] DESC, [Id] ASC` (tie-breaker stable pour OFFSET/FETCH).

`QueryAsync` exécute ce SQL en `DbCommand` paramétré (`SELECT r.*, COUNT(*) OVER() AS [__total] …
OFFSET … FETCH NEXT …`) et matérialise les `CustomRecord`.

### Règles par mode

- **Liste** : pagination `page`/`pageSize` (défaut `definition.pageSize`, **borne 200** ⇒ 400 au-delà) ;
  `items` + `total` exact.
- **Kanban** : requête unique triée par le champ de regroupement puis le tri de la vue,
  `take = StudioRecordViewMaxKanbanCards` (500) ; regroupement **en mémoire** dans l'ordre
  `columnOrder` (filtré aux options) sinon l'ordre des options `Select` ; groupe `value = null`
  « Sans valeur » si `showEmptyGroup` ; `truncated = total > 500`.
- **Calendrier** : `rangeStart`/`rangeEnd` **obligatoires** (400 sinon), fenêtre **≤ 92 jours** ; un
  filtre `between` est ajouté sur `startFieldKey` ; `take = StudioRecordViewMaxCalendarEvents` (1000) ;
  projection en `events[]` (`recordId`, `title`, `start`, `end?`, `colorValue?`) ; `truncated`.
- `extraFilters` (≤ 5) validés comme les filtres de la vue ; `search` → `LIKE` paramétré sur les champs
  recherchables (Text/MultilineText/Select, ≤ 6) ou `DataJson LIKE` si aucun.

## API (`api/studio/records/{entityKey}/views`, gardée par le drapeau)

| Méthode | Route | Policy | Codes |
|---|---|---|---|
| GET | `…/views` | `CustomRecordsRead` | 200 · 404 entité |
| GET | `…/views/{id}` | `CustomRecordsRead` | 200 · 404 |
| POST | `…/views` | `StudioDesignForms` | 201 · 400 `Validation.key` · 409 clé prise |
| PUT | `…/views/{id}` | `StudioDesignForms` | 200 · 400 · 404 · 409 RowVersion |
| DELETE | `…/views/{id}` | `StudioDesignForms` | 204 · 404 (soft delete) |
| POST | `…/views/{id}/default` | `StudioDesignForms` | 204 · 404 (exclusif) |
| POST | `…/views/{id}/run` | `CustomRecordsRead` | 200 · 400 · 404 |

Toutes les erreurs passent par `StudioErrorMapping` (`Conflict`/`record.duplicate_link` → 409,
`*.NotFound`/`NotFound` → 404, `Unauthorized` → 401, `Forbidden` → 403, sinon 400).

## PATCH partiel (R5)

`PATCH api/studio/records/{entityKey}/{id}` (`CustomRecordsWrite`, gardé par le drapeau) applique une
**fusion partielle** : `CustomRecordPatchMerger.MergePatch` applique les clés fournies sur le JSON
existant (`null` = effacement), refuse clés inconnues / réservées / calculées (`400 Validation.data`),
puis `CustomRecordValidator.ValidateAndCanonicalize` produit le document canonique complet (les clés
absentes sont conservées). Le flux réutilise les étapes de `Update` (AutoNumber immuable, unicité de
champ et de paire, `OnUpdate` publié). **`rowVersion` est obligatoire** (400 absent, 409 périmé via
pré-contrôle + `DbUpdateConcurrencyException` en secours).

## Quota, capability, audit

- Quota plan : `MaxCustomRecordViewsPerEntity` (`StudioQuotas.MaxRecordViewsKey`, fallback 20, Free = 20,
  Monthly/Annual ∞) → `Validation.Plan` (400) à la création.
- Capability : `StudioAiCapabilitiesDto.RecordViewsEnabled = EnableStudioRecordViews` (les autres
  drapeaux du programme restent `false`).
- Audit (best-effort, `StudioAudit.SafeLogAsync`, `entityType = "CustomRecordViewDefinition"`) :
  `Studio.RecordView.Created` / `Updated` / `Deleted` / `DefaultSet`. Le PATCH est audité par le cycle de
  vie existant (`OnUpdate`).

## Réversibilité

Drapeau coupé ⇒ toutes les routes répondent 404 sans effet de bord et `schema.views = []`. La table est
inerte. `Down` supprime la table (perte des vues acceptée en préprod ; en prod, désactiver le drapeau
sans `Down`).
