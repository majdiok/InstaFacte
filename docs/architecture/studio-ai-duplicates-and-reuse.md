# Studio IA — doublons de tables et réutilisation (`existingKey`)

**Date :** 11 septembre 2026 (PR 1.3)
**Périmètre :** détection de tables proposées équivalentes à des tables Studio existantes du tenant
(indice d'aperçu `summary.duplicates[]`) et réutilisation explicite d'une table existante dans un
système généré (`"existingKey"` dans la spec). Aucune clé de fonctionnalité nouvelle : tout se joue
sous `Ollama:EnableStudioAiPlanPreview` (création déjà livrée en P0).
**Documents liés :** [Contexte et modèle avancé](studio-ai-context-and-advanced-model.md) ·
[QA de l'assistant Studio](../developer/studio-ai-assistant-qa.md) (tests 49–50)

---

## 1. Le problème résolu

Le petit modèle local ne connaît pas toujours les tables déjà créées par le client : il propose
« Employés » alors qu'une table `employes` existe, et le plan confirmé crée `employes_2` — doublon
silencieux, données éclatées. Deux réponses complémentaires :

1. **Signaler** (passif) : l'aperçu du plan affiche les ressemblances avec l'existant — l'utilisateur
   décide en connaissance de cause. Un indice n'est **jamais** un blocage.
2. **Réutiliser** (actif) : la spec peut déclarer `"existingKey": "<clé>"` sur une entité pour
   s'appuyer sur une table existante **sans la créer ni la modifier** — et la règle 11 du prompt
   StudioBuilder enseigne ce mécanisme au modèle.

---

## 2. Détection (`StudioAiDuplicateDetector`)

Détecteur **pur** (aucune base) : les handlers lisent une fois les tables actives du tenant
(`ICustomEntityRepository.ListAsync(tenantId, includeInactive: false)`) et passent le résultat au
détecteur. Au plus **un** indice par entité proposée ; la raison la plus forte l'emporte :

| Raison | Égalité vérifiée | Exemple |
| --- | --- | --- |
| `same_key` | référence proposée == clé réelle | `employes` ≡ `employes` |
| `same_name` | slug accent-insensible des libellés ≡ clé/libellés existants | « Employés » ≡ clé `employes` |
| `singular_plural` | forme normalisée (mots vides FR écartés + singulier conservateur) | « Demande de congé » ≡ « Demandes de congés », `bureaux` ≡ `bureau` |

**Jamais de rapprochement par préfixe** : « Contrat » ne signale pas « Contrats cadres ». La
singularisation est volontairement conservative (`ss` et mots de ≤ 4 lettres intouchés) : un doublon
manqué coûte moins cher qu'un faux positif dans l'aperçu.

Chemins couverts (cinq) : outils LLM `studio_plan_app` / `studio_plan_system`
(`AiToolExecutor.StudioPlans`), validation en direct `validate`, création `from-spec` et
`from-template` (`StudioAiPlanCreationFeatures`). La lecture est unique par appel, en **lecture
seule**, et le résumé recalculé expose `duplicates` (toujours présent, tableau vide par défaut) plus
un avertissement en clair par indice. Note : le handler `validate` perd son caractère « aucune
lecture en base » — la seule lecture ajoutée est la liste des tables du tenant courant.

Une entité qui déclare déjà `existingKey` n'est jamais signalée : c'est un choix explicite.

## 3. Réutilisation (`existingKey`)

| Étape | Comportement |
| --- | --- |
| Parsing (`StudioAiSystemSpec`) | Alias `existingKey` / `existing` / `useExisting` / `reuse` ; chaîne = clé (slugifiée, validée `StudioKey.IsValidShape`, sinon **entité rejetée** avec message) ; `true` = la clé de l'entité ; `displayName` optionnel (repli sur la clé) ; `fields`/`form`/`report` ignorés avec avertissement |
| Bornes | `MaxEntities = 8` ne compte que les tables **nouvelles** ; `MaxExistingRefs = 8` borne les réutilisations (au-delà : entité **ignorée** avec avertissement, jamais de rejet franc) |
| Canonique | une entité réutilisée n'émet que `{ "ref", "existingKey", "displayName" }` — aller-retour stable à l'octet |
| Exécution (`StudioAiSystemOrchestrator`) | résolution **avant toute écriture** via `GetCustomEntitySchemaQuery` : clé inconnue **ou inactive** ⇒ échec franc sans même créer le système ; sinon la ref interne est mappée sur la clé réelle (relations et seed la visent) ; **aucune** création table/champ/formulaire/état pour elle ; un lot `seed` qui la vise est ignoré avec avertissement ; le quota ne compte que les tables créées ; l'annulation ne la supprime jamais (absente du journal) ; le payload distingue `createdCount` / `reusedCount` |
| Prompt (règle 11) | « Pour t'appuyer sur une table existante dans un système, déclare l'entité avec `"existingKey": "<clé>"` au lieu de ses champs. » — révision de cache du prompt passée de `v4` à `v5` |

## 4. Sécurité

- La détection ne lit que les `CustomEntityDefinitions` **du tenant courant** (une lecture) ; une
  clé réutilisée est revérifiée à l'exécution (existence + `IsActive`) — une spec forgée à la main
  ne peut ni lire ni écrire une table d'un autre tenant (le handler de schéma filtre par tenant).
- Le message d'échec ne cite que la clé **demandée**, jamais les clés existantes.
- La table réutilisée n'est l'objet d'**aucune** écriture : ni création, ni champ, ni formulaire,
  ni état, ni seed, ni suppression lors d'une annulation.

## 5. Ce que ça ne fait pas (suite du programme)

- **PR 2.2 / 4.3** réutilisent `ParsedSystemSpec.Warnings` (additif, `[]` par défaut) et pourront
  réutiliser le détecteur pour les modifications (`studio_plan_changes`).
- Le frontend (PR 1.4) rendra le bandeau doublon à partir de `summary.duplicates[]` (forme déjà
  stable côté backend : clé toujours présente).
- Pas de fusion de tables ni de migration de données : réutiliser une table ne touche pas à son
  schéma ; compléter une table existante reste du ressort de `studio_plan_changes`.
