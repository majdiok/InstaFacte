# Studio IA — contexte de conversation et modèle avancé

**Date :** 10 septembre 2026
**Périmètre :** réglage plateforme du modèle Studio « avancé » (PR 1.1) — le digest de contexte de
conversation et la bascule côté Studio sont couverts par la PR 1.2
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
