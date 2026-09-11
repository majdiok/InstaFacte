# Studio IA — contexte de conversation et modèle avancé

**Date :** 10 septembre 2026 · mise à jour 11 septembre 2026 (PR 1.2)
**Périmètre :** réglage plateforme du modèle Studio « avancé » (PR 1.1, §1–5) ; digest de contexte
injecté dans le prompt StudioBuilder et bascule « modèle avancé » par requête (PR 1.2, §6–7)
**Documents liés :** [États du Studio IA](studio-ai-reports.md) · [QA de l'assistant Studio](../developer/studio-ai-assistant-qa.md) · [Migrations backend](../backend-tenant-migrations.md)

---

## 1. Le problème résolu

L'assistant Studio tourne sur le modèle Ollama local de la plateforme (`qwen2.5:7b-instruct` en
pratique). Ce modèle suffit pour un tour d'outil unique — « crée une table Fournisseurs » — mais
décroche dès qu'une demande exige **plusieurs tours** : lire le schéma existant, puis proposer un
plan, puis le confirmer. Les installations disposant d'un GPU distant ou d'un accès cloud
(OpenRouter, Modal, Cursor) n'avaient aucun moyen de réserver ce modèle plus capable aux seules
demandes complexes du Studio : le modèle Studio est un réglage unique, global.

La PR 1.1 ajoute un **second** réglage plateforme, le « modèle Studio avancé », que l'utilisateur
pourra activer à la demande (« Modèle avancé ») depuis le Studio. Rien n'est routé vers ce modèle
tant que la PR 1.2 n'a pas livré la bascule : cette PR se limite au réglage, à son exposition dans
les capacités et à son administration en back-office.

---

## 2. Réglage plateforme

### 2.1 Stockage

| Élément | Valeur |
| --- | --- |
| Table (base **master**) | `PlatformAiSettings` — ligne unique partagée par toutes les entreprises |
| Colonne | `StudioAiAdvancedModelRef` `nvarchar(500) NULL` |
| Sémantique de `NULL` | aucun modèle avancé proposé ⇒ `AdvancedModelAvailable = false` |
| Entité | `PlatformAiSettings.SetStudioAiAdvancedModel(string?)` (trim, vide ⇒ `NULL`) |
| Service | `IPlatformAiSettingsService.GetStudioAiAdvancedModelRefAsync` / `SetStudioAiAdvancedModelRefAsync` |
| Cache | clé `platform:ai:studio-advanced-model`, TTL 60 s, invalidée par toute écriture de la ligne |

La colonne stocke une **référence canonique** de modèle (`ModelRef.NormalizeStored`) : le fournisseur
est donc libre — `ollama:` (GPU distant), `openrouter:`, `modal:` ou `cursor:`.

> **Lectures séquentielles obligatoires.** Les getters de `PlatformAiSettingsService` partagent le
> `MasterDbContext` scoped : un `Task.WhenAll` sur deux getters déclenche
> « A second operation was started on this context ». Le garde-fou
> `DbContextConcurrencyGuardrailTests` liste `GetStudioAiAdvancedModelRefAsync` pour bloquer toute
> régression.

### 2.2 Migration

| Élément | Valeur |
| --- | --- |
| Migration EF (master) | `20260910120000_AddStudioAiAdvancedModelRef_Master` |
| Jumeau SQL (ops) | `docs/runbooks/sql/AddStudioAiAdvancedModelRef_Master.idempotent.sql` |
| Rattrapage | `docs/runbooks/sql/AddStudioAiModelRef_Master.idempotent.sql` (colonne `StudioAiModelRef`, migration `20260729020000`, dont le script manquait) |

Les deux sens (`Up` / `Down`) sont du SQL écrit à la main gardé par `IF COL_LENGTH(...) IS NULL` /
`IS NOT NULL` : la migration est rejouable, et le script SQL insère lui-même la ligne
`__EFMigrationsHistory` pour qu'un `dotnet ef database update` ultérieur ne rejoue pas l'opération.
Le `ModelSnapshot` master est mis à jour à la main (aucun fichier `.Designer.cs`, comme
`AddTenantRegistrationProfile_Master`).

### 2.3 Flag de configuration

`Ollama:EnableStudioAiAdvancedModel` — défaut C# **false**, `true` dans `appsettings.json` et
`appsettings.Production.json`. Baissé, la colonne n'est même pas lue.

---

## 3. Exposition aux clients

`GET /api/studio/ai/capabilities` (`StudioAiCapabilitiesQuery`) :

```
AdvancedModelAvailable = Ollama:EnableStudioAiAdvancedModel
                         && PlatformAiSettings.StudioAiAdvancedModelRef non vide

AdvancedModelLabel     = nom humain du modèle (préfixe fournisseur retiré)
                         null si AdvancedModelAvailable == false
```

Le libellé est **toujours** un nom de modèle, jamais une référence de connexion ou une clé. Contrairement
au libellé standard, il n'existe **aucun repli** pour l'avancé : un modèle avancé non configuré reste
`null` plutôt que de retomber sur le modèle standard, sinon le client afficherait une bascule
« Modèle avancé » qui ne change rien.

---

## 4. Administration (back-office plateforme)

`GET`/`PUT /api/platform/ai-settings` (policies inchangées : `PlatformAdmin` +
`perm:platform.ai.manage`) transportent `studioAiAdvancedModelRef`. La page
**Configuration IA → Modèle IA — Assistant Studio** propose un second sélecteur
« Modèle Studio avancé (GPU / cloud) », alimenté par le même catalogue de modèles que le modèle
standard.

Contrôles appliqués au `PUT`, mutualisés avec le modèle standard
(`PlatformAiSettingsController.ValidateStudioModelAsync`) :

1. référence analysable (`ModelRef.Parse`) ;
2. modèle capable de chat (pas d'embedding) ;
3. sélection Cursor valide, fournisseur cloud (Cursor / Modal) réellement configuré ;
4. modèle Ollama réellement installé sur le moteur IA (`EnsureModelInstalledAsync`) ;
5. **spécifique à l'avancé** : la référence doit **différer** du modèle Studio standard — comparaison
   sur la forme canonique, y compris quand le même `PUT` modifie les deux champs. Sinon `400` avec un
   message français explicite. Le back-office désactive l'enregistrement en amont pour éviter
   l'aller-retour.

Une valeur vide (`""`) efface le réglage et masque la bascule côté Studio ; `null` (champ absent du
`PUT`) signifie « inchangé ».

---

## 5. Cutover ops

1. Appliquer la migration master (ou le script idempotent sur les bases migrées à la main).
2. Back-office → Configuration IA → renseigner « Modèle Studio avancé (GPU / cloud) ». Si le modèle
   est cloud, vérifier que la clé du fournisseur est enregistrée dans la même page.
3. Activer `Ollama:EnableStudioAiAdvancedModel`, puis redémarrer l'API.
4. Vérifier `GET /api/studio/ai/capabilities` : `advancedModelAvailable: true` et
   `advancedModelLabel` renseigné.

Repli : baisser le flag, ou vider le réglage en back-office. Aucune donnée n'est perdue et la
résolution du modèle Studio standard reste inchangée.

---

## 6. Digest de contexte (PR 1.2)

### 6.1 Le problème résolu

Le prompt StudioBuilder ne savait **rien** des tables déjà créées par le client ni du plan qu'il
venait de valider. Sur « ajoute un champ téléphone aux fournisseurs », le modèle inventait une clé
(`fournisseur`, `suppliers`…) ou proposait de **recréer** la table. La PR 1.2 injecte dans le prompt
un digest borné du schéma existant et du dernier plan de l'utilisateur, avec les **vraies clés**.

### 6.2 Composants

| Composant | Rôle |
|---|---|
| `IStudioContextDigestService` / `StudioContextDigestService` (`Infrastructure/Services/Studio`) | Construit les deux digests. Lectures **séquentielles** (chaque dépôt ouvre son propre DbContext tenant ; le séquentiel évite une rafale de connexions sur le chemin critique d'un tour) : entités actives → systèmes → champs actifs par entité (≤ 50 tables). Cache mémoire `studio:schema-digest:{tenantId}`, TTL **30 s**, sur la liste non tronquée. Aucun digest si le tenant n'est pas résolu (`Guid.Empty`). |
| `StudioPromptOptions` (`Application/Features/AI/DTOs`) | `UseAdvancedModel`, `StudioIntent` (normalisé : `system · table · relations · form · reference_data · report · workflow · page`, sinon ignoré), `TenantId`, `UserId`. Passé par le handler à `IAiContextBuilder.BuildSystemPromptAsync(...)`. |
| `AiContextBuilder` | `SystemPromptCacheRevision = "v4"`. En StudioBuilder, si `Ollama:EnableStudioAiSchemaDigest` : appelle le service puis ajoute au prompt le préambule d'intention, les règles **11** (réutiliser les vraies clés, ne jamais recréer) et **12** (le dernier plan est la cible de « ajoute / complète / continue »), puis les sections `SCHÉMA EXISTANT (tables Studio de ce client) :` et `DERNIER PLAN :`. Toute exception du service est **journalisée** (`LogWarning`) et le prompt est produit sans ces sections. |

### 6.3 Format du digest

```
SCHÉMA EXISTANT (tables Studio de ce client) :
- employes « Employés » : nom:text, poste:select, salaire:money
- conges « Congés » (systeme:gestion_conges) : employe:relation, debut:date, statut:select
… (+3 tables)

DERNIER PLAN :
- [En attente 09:41] Système « Gestion des congés » : 2 tables (Employés, Congés)
- [Terminé 08:12] Table « Fournisseurs » : 1 table (fournisseurs)
```

Règles : métadonnées **uniquement** (jamais une valeur d'enregistrement) ; tables et champs
inactifs exclus ; une ligne est conservée entière ou omise et comptée dans `… (+N tables)` ; une
ligne ne dépasse jamais `MaxLineChars` (320) — au-delà, la liste des champs est coupée avec
`, … (+N champs)` pour qu'une seule table très large ne consomme pas le budget des autres clés ; les plans
terminés utilisent les clés réelles de `ResultJson`, les plans en attente les libellés de `SummaryJson`
(les clés ne sont dérivées qu'à l'exécution) ; un plan terminé depuis plus de **24 h** n'est plus « le
dernier plan » ; aucun plan ⇒ section omise ; aucune table ⇒ « Aucune table Studio pour l'instant. ».

### 6.4 Réglages (`Ollama:*`)

| Clé | Défaut C# | `appsettings*.json` | Rôle |
|---|---|---|---|
| `EnableStudioAiSchemaDigest` | `false` | `true` | Interrupteur du digest. Baissé : prompt strictement identique à la révision précédente (hors préambule d'intention). |
| `StudioSchemaDigestMaxCharsCpu` | 1200 | 1200 | Budget du schéma avec le modèle standard (CPU). |
| `StudioSchemaDigestMaxCharsAdvanced` | 4000 | 4000 | Budget du schéma avec le modèle avancé. |
| `StudioLastPlanDigestMaxChars` | 600 | 600 | Budget du dernier plan. |
| `StudioTemperature` | 0.1 | 0.1 | Température dédiée aux tours StudioBuilder (specs JSON déterministes). |
| `StudioAdvancedMaxToolCallRounds` | 4 | 4 | Rounds d'outils quand le modèle avancé est retenu (borné 1..20), en remplacement du plafond CPU — sauf si le modèle avancé est lui-même un modèle Ollama sur un hôte **CPU seul**, cas où le plafond CPU s'applique comme avant. |

En StudioBuilder, la graine Ollama vaut `Ollama:Seed` si renseignée, sinon **7** — specs reproductibles
d'un tour à l'autre.

---

## 7. Bascule par requête (PR 1.2, décision D3)

### 7.1 Contrat HTTP

`POST /api/ai/chat` — `options.useAdvancedModel: boolean` (défaut `false`) et
`options.studioIntent: string | null`. Les deux champs sont **ignorés hors** `assistantMode:
"StudioBuilder"`. Un corps historique sans ces champs reste valide (`AiChatOptionsContractTests`).

### 7.2 Résolution dans `SendChatMessageHandler`

```
demandé   = StudioBuilder && options.useAdvancedModel == true
configuré = demandé && Ollama:EnableStudioAiAdvancedModel
            ? PlatformAiSettings.GetStudioAiAdvancedModelRefAsync()   // lecture Master SÉQUENTIELLE
            : null
useAdvanced = demandé && flag && configuré non vide
```

Ordre de résolution du modèle : **avancé si retenu** → `StudioAiModelRef` → `Ollama:StudioAiModel`
→ `DefaultModelRef` → `DefaultModel`. La vérification de disponibilité du fournisseur tourne en boucle
à deux tours au plus : si le modèle **avancé** est indisponible (moteur arrêté, modèle non installé,
clé cloud absente…), le handler retombe sur la chaîne standard, **rebâtit le prompt au budget CPU** et
revérifie. Un fournisseur standard indisponible reste une erreur, comme avant.

### 7.3 Repli silencieux — jamais une erreur

| Situation | `usedAdvancedModel` | `advancedModelFallbackReason` | Log |
|---|---|---|---|
| Avancé retenu | `true` | `null` | — |
| Non demandé | `false` | `null` | — |
| `EnableStudioAiAdvancedModel=false` | `false` | `"disabled"` | `LogWarning` (réf. avancée **non lue**) |
| Référence avancée vide | `false` | `"not_configured"` | `LogWarning` |
| Fournisseur avancé indisponible | `false` | `"unavailable"` | `LogWarning` avec le motif |

Le résultat est émis **une fois**, avant le premier token, par l'événement SSE
`{"type":"meta","content":"{\"usedAdvancedModel\":…,\"advancedModelFallbackReason\":…,\"model\":\"…\"}"}`
(`ChatStreamEvent.StudioMetaEvent`), StudioBuilder uniquement. `model` est le libellé lisible du
modèle effectivement utilisé — `ModelRef.HumanLabel`, la même fonction que `standardModelLabel` /
`advancedModelLabel` des capacités, pour que l'atelier affiche le même nom partout. L'atelier (PR 1.4) affiche « Modèle standard utilisé » dès que `usedAdvancedModel=false`
alors que la bascule était active. Aucun `400`, aucun événement `error` pour un repli.

### 7.4 Effets du modèle avancé sur le tour

- Budget de rounds d'outils : `StudioAdvancedMaxToolCallRounds` (4) au lieu du plafond CPU (fournisseur
  cloud ou Ollama sur GPU ; un modèle Ollama « avancé » exécuté sur CPU seul garde le plafond CPU).
- Budget de digest : `StudioSchemaDigestMaxCharsAdvanced` (4000) au lieu de 1200.
- Température `StudioTemperature` et graine fixe : communs à tous les tours Studio, avancé ou non.

### 7.5 Tests

`SendChatMessageHandlerStudioAdvancedModelTests` (bascule, trois raisons de repli, mode Default,
lectures Master séquentielles), `ResolveMaxToolCallRoundsTests` (budget avancé, bornes),
`AiContextBuilderStudioDigestTests` (sections, budgets, intention, dégradation, `v4`),
`StudioContextDigestServiceTests` (format, cache, bornes, isolation tenant, dernier plan),
`AiChatOptionsContractTests` (contrat HTTP + trame SSE `meta`). Smoke tests manuels : QA **46–48**.
