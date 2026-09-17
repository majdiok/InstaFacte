# Studio AI Assistant — QA checklist

## Feature flags

| Drapeau (config serveur `Ollama:*`) | Défaut code | Prod initiale | Rôle |
|---|---|---|---|
| `EnableMutationTools` | `true` (dev) | `false` | Doit être `true` pour toute génération de table/système. |
| `EnableStudioAiTools` | `true` | `true` | Interrupteur maître des outils `studio_*`. |
| `EnableStudioSystemGeneration` | `true` | `false` | `studio_generate_system` (multi-tables). |
| `EnableStudioAiPlanPreview` | **`false`** | `false` | Flux **plan → aperçu → confirmation**. Substitue `studio_plan_*` à `studio_generate_*`. |
| `EnableStudioAiModifyTools` | **`false`** | `false` | Modification de l'existant (`studio_plan_changes`, `studio_get_table_schema`). Sans effet si l'aperçu est off. |
| `EnableStudioAiViewTools` | **`false`** | `false` | Fenêtres sur tables réelles (`studio_plan_view`, `studio_list_sql_tables`). Sans effet si l'aperçu est off. |
| `EnableStudioReportPdf` | `true` | `true` | Coupe-circuit de l'impression PDF des états Studio. |
| `EnableStudioSqlReportEngine` | **`false`** | `false` | Moteur d'ÉTATS sur les tables réelles (concepteur humain **et** IA). |
| `EnableStudioAiReportTools` | **`false`** | `false` | Outils `studio_*_report` de l'assistant. Sans effet si le moteur est off. |
| `EnableStudioReportShortcut` | **`false`** | `false` | Raccourci DÉTERMINISTE : une demande d'état non ambiguë est exécutée AVANT l'appel au modèle. Rend le résultat indépendant du modèle configuré. |
| `EnableStudioSqlSourceGuard` | **`false`** | `false` | Étend le classement par domaine aux FENÊTRES. Passer le runbook d'impact d'abord. |
| `EnableStudioAiAdvancedModel` | **`false`** | `true` | Bascule « Modèle avancé » par requête (`options.useAdvancedModel`). Sans effet tant qu'aucun modèle Studio avancé n'est configuré en back-office. |
| `EnableStudioAiSchemaDigest` | **`false`** | `true` | Digest du schéma existant + dernier plan dans le prompt StudioBuilder (vraies clés). Budgets : `StudioSchemaDigestMaxCharsCpu` 1200 / `StudioSchemaDigestMaxCharsAdvanced` 4000 / `StudioLastPlanDigestMaxChars` 600. |
| `EnableStudioManyToMany` | **`false`** | `true` | Relations plusieurs-à-plusieurs (entités de jonction `kind: "Junction"`) : `GET/POST api/studio/entities/{id}/relations[/many-to-many]`, `schema.relations[]`, unicité de la paire (`409 record.duplicate_link`), filtre serveur `filterField`/`filterValue`. Off ⇒ 404 sur les deux endpoints, `relations: []`, aucun autre changement. |

> Les drapeaux en gras sont **off par défaut** : sans eux, le comportement du Studio IA est
> strictement celui d'avant (gardes couvertes par `StudioAiPlanCatalogTests` et
> `StudioAiReportToolsTests`).

Bornes associées : `StudioReportMaxRows` (défaut 5 000, plafond dur 50 000) et
`StudioReportCommandTimeoutSeconds` (défaut 30).

## Smoke tests — comportement historique (tous drapeaux off)

1. `/studio/ai` — prompt sans « créer » : une table simple est créée.
2. Prompt multi-tables : système avec relations + données de référence.
3. La barre latérale affiche le système groupé avec ses tables.
4. `/studio/systems/{key}` : la page hub se charge.
5. `/ai-assistant` général : inchangé.

## Génération enrichie (aucun drapeau requis)

6. « Crée un système de contrats avec un formulaire sur deux colonnes et un état des contrats actifs
   trié par date » ⇒ vérifier dans le **concepteur de formulaire** que les champs courts sont en
   demi-largeur (`half`) et les libellés personnalisés repris ; dans le **concepteur d'état**, que
   les filtres et le tri sont présents (et pas seulement le regroupement).
7. Rétro-compatibilité : un `spec_json` de l'ancien format (champs de formulaire en simples chaînes,
   rapport avec `groupBy`/`measures` seulement) produit exactement le même résultat qu'avant.

## Aperçu et confirmation (`EnableStudioAiPlanPreview=true`)

8. Prompt de création ⇒ une **carte d'aperçu** s'affiche (Modèle de données / Formulaires / États /
   Données de référence) et **rien n'est créé** : vérifier en base que la table n'existe pas encore.
9. « Annuler » ⇒ aucun objet créé, le plan passe `Cancelled`, le compositeur est déverrouillé.
10. « Valider et créer » ⇒ les étapes de construction s'affichent **une par une** (progression live,
    et non d'un bloc en fin d'exécution), puis la barre latérale se rafraîchit.
11. Double-clic sur « Valider » ⇒ une seule création (verrou `RowVersion`), la seconde requête est
    rejetée proprement.
12. Attendre > 60 min puis valider ⇒ message « Ce plan a expiré », rien n'est créé.
13. Le modèle ne doit **jamais** annoncer que les tables sont créées avant validation (règle 7 du
    prompt) : relire le texte de la réponse.

## Modification de l'existant (`EnableStudioAiPlanPreview` + `EnableStudioAiModifyTools`)

14. « Ajoute un champ Motif de refus sur la table Contrats » ⇒ l'aperçu montre `Ajouter « Motif de
    refus »` avec son type ; après validation, le champ apparaît dans le formulaire.
15. « Retire le champ X de la table Contrats » ⇒ l'aperçu affiche un avertissement orange
    « les valeurs déjà saisies sont CONSERVÉES » ; après validation, **ouvrir un enregistrement
    existant et vérifier que sa donnée est toujours en base** (le champ est désactivé, pas supprimé).
16. Cibler un champ inexistant ⇒ l'opération disparaît de l'aperçu avec un avertissement, elle n'est
    pas annoncée puis échouée silencieusement.
17. Demander la suppression d'une table ou d'un système ⇒ impossible (hors liste blanche) : le modèle
    doit le dire, aucun outil ne le permet.
18. Échec partiel (ex. quota de champs atteint sur un ajout) ⇒ les autres opérations s'appliquent
    quand même et le rapport final liste ce qui a été ignoré.

## Fenêtres sur données existantes (`EnableStudioAiPlanPreview` + `EnableStudioAiViewTools`)

19. « Crée une fenêtre sur les factures avec date, client et total » ⇒ l'aperçu liste la table source
    et les colonnes ; après validation, la fenêtre est identique à ce que produirait le concepteur.
20. Demander une table de la liste de refus (`SqlSchemaGuard.DeniedTables`) ⇒ refus explicite.
21. Demander une colonne inexistante ⇒ elle est écartée avec un avertissement, jamais inventée.
22. Vérifier qu'une fenêtre reste en **lecture seule** : aucun bouton d'écriture sur le runner.

## États PDF (`EnableStudioReportPdf=true`)

23. Ouvrir un état **groupé** (`/studio/reports/{id}`) ⇒ « Imprimer (PDF) » ouvre un PDF avec
    en-tête société, filtres/tri résumés, colonnes mesure alignées à droite et bande de totaux.
24. Ouvrir un état de **détail** (sans regroupement) ⇒ colonnes projetées, dates au format `dd/MM/yyyy`,
    montants au format tunisien (`30 000.000`).
25. État **sans donnée** ⇒ PDF valide affichant « Aucune donnée pour cet état » (pas d'erreur 500).
26. État à plus de 6 colonnes ⇒ bascule automatique en paysage.
27. Drapeau à `false` ⇒ l'endpoint renvoie 404 et le bouton n'aboutit pas.

## États sur les tables réelles (`EnableStudioSqlReportEngine` + `EnableStudioAiReportTools`)

> Architecture et modèle de sécurité : [`docs/architecture/studio-ai-reports.md`](../architecture/studio-ai-reports.md).

### Non-régression (drapeaux à `false`)

28. `/studio/ai` — « Crée une table Fournisseurs avec nom, ville, téléphone » ⇒ comportement identique
    à avant. Le catalogue d'outils ne contient AUCUN `studio_*_report`.
29. `/studio/reports` — les états existants (tables personnalisées et sources historiques) se relancent
    et s'impriment à l'identique ; le sélecteur de source ne propose que « Mes tables » et
    « Données existantes ».

### Fonctionnalité (drapeaux à `true`)

30. « Crée un rapport avancé des ventes de produits pour ce trimestre » ⇒ un **tableau s'affiche dans
    la conversation** (produit, quantité, CA, TVA) avec bascule Graphique et export CSV/XLSX.
    **Plus aucun message « Je n'ai pas pu lancer la création du système ».**
31. **Contrôle croisé obligatoire** : comparer les montants au point 30 avec
    `/reports/sales-by-line` sur la même période ⇒ **égalité au millime**. C'est le test qui valide le
    moteur ; il ne se contourne pas. (Le préréglage reprend le prédicat de
    `GetSalesRevenueAggregatedAsync` : factures `Validated` ou `Paid`.)
32. « Enregistrer comme état » ⇒ carte d'aperçu montrant un **échantillon de vraies lignes** ⇒
    « Valider et enregistrer » ⇒ l'état apparaît dans `/studio/reports`, se relance et s'imprime en PDF.
33. Ouvrir cet état dans le concepteur (`/studio/reports/{id}`) ⇒ source, regroupement, mesures et
    filtres sont modifiables comme pour n'importe quel état Studio.
34. « Ventes par mois de cette année » ⇒ regroupement calendaire correct (une ligne par mois, triée
    chronologiquement).
35. Demander un état de **paie** avec un compte sans `payroll:read` ⇒ refus explicite, **aucune donnée
    affichée** ; le sélecteur de source du concepteur ne montre pas non plus le domaine « Paie & RH ».
36. Demander une colonne inexistante ⇒ écartée avec un avertissement dans l'aperçu, jamais inventée.
37. Demander à croiser deux tables sans clé étrangère (ex. lignes de facture × salariés) ⇒ refus
    explicite mentionnant l'absence de lien.
38. État de **détail** dépassant `StudioReportMaxRows` ⇒ bandeau orange de troncature dans l'interface
    **et** ligne d'avertissement dans le PDF ; l'état **agrégé** équivalent affiche des totaux exacts
    (revérifier contre le point 31).
39. Double-clic sur « Valider et enregistrer » ⇒ une seule création (verrou `RowVersion`).
40. Le modèle ne doit **jamais** annoncer que l'état est enregistré avant validation de l'aperçu.

### Raccourci déterministe (`EnableStudioReportShortcut`)

Le modèle Studio n'émet pas toujours d'appel d'outil — un modèle de *code* n'en émet presque jamais.
Le raccourci reconnaît la demande et exécute l'état lui-même. Vérifier :

42. « créer un rapport détaillé de ventes d'articles » (sans période) ⇒ tableau affiché, **période
    annoncée en clair** dans la réponse (défaut : année en cours).
43. « ventes par client ce trimestre » ⇒ bon préréglage **et** bonne période.
44. « crée une table Rapports avec les champs titre et date » ⇒ **aucun** raccourci d'état ; le flux
    de création de table est intact. C'est le faux positif à ne jamais accepter.
45. « fais-moi un rapport de licornes » ⇒ message adapté à l'intention (« Je n'ai pas pu préparer cet
    état… ») **et** des suggestions cliquables, jamais « créer ce système ».

**Signature de bon fonctionnement dans les logs** (`src/Backend/FactuTrust.API/logs/factutrust-<date>.log`) :

```bash
Select-String -Path src/Backend/FactuTrust.API/logs/factutrust-20260816.log -Pattern "phase=total_request" | Select-Object -Last 3
```

Attendu : `tools_executed_this_request=1` et `content_chars_persisted>0`. La signature d'échec —
`final_response_meaningful=true` **avec** `content_chars_persisted=0` et `content_chars_streamed=0` —
doit avoir disparu. Le chemin d'échec est désormais nommé : `studio_silence_fallback`.

## Contexte et modèle avancé (`EnableStudioAiSchemaDigest` + `EnableStudioAiAdvancedModel`)

> Architecture : [`docs/architecture/studio-ai-context-and-advanced-model.md`](../architecture/studio-ai-context-and-advanced-model.md) §6–7.
> Prérequis : au moins une table Studio existante (ex. `fournisseurs`) ; pour 47–48, un modèle Studio
> avancé configuré en back-office (Configuration IA) et distinct du modèle standard.

46. `EnableStudioAiSchemaDigest=true` — « ajoute un champ téléphone à la table Fournisseurs » ⇒ le plan
    proposé cible la **clé réelle** `fournisseurs` (jamais `fournisseur`, `suppliers` ni une nouvelle
    table). Point d'arrêt (ou trace temporaire) sur
    `AiContextBuilder.BuildStudioBuilderSystemPrompt` : le prompt contient `SCHÉMA EXISTANT (tables Studio de ce
    client) :` suivi d'une ligne `- fournisseurs « Fournisseurs » : …` et la règle **11**. Aucune
    valeur d'enregistrement n'apparaît. Après création d'une table, elle figure dans le digest au tour
    suivant (cache 30 s au plus).
47. `EnableStudioAiAdvancedModel=false`, requête envoyée avec `options.useAdvancedModel=true` (bascule
    active dans l'atelier ou corps forgé) ⇒ la réponse arrive **normalement** sur le modèle standard ;
    le flux SSE contient un événement `type:"meta"` avec `usedAdvancedModel:false` et
    `advancedModelFallbackReason:"disabled"` ; le log API contient `Studio advanced model requested but
    disabled`. **Jamais** de `400` ni d'événement `error`. Variante : drapeau `true` mais réglage
    back-office vide ⇒ `"not_configured"` ; modèle avancé Ollama non installé ⇒ `"unavailable"` et
    réponse sur le standard.
48. `EnableStudioAiAdvancedModel=true` + modèle avancé configuré, `options.useAdvancedModel=true` sur
    « crée un système de gestion de projets avec projets, tâches et jalons liés » ⇒ `meta` porte
    `usedAdvancedModel:true` et `model` = identifiant du modèle avancé ; le log `phase=provider_availability`
    cite la référence avancée ; la boucle d'outils dispose de **4** rounds
    (`StudioAdvancedMaxToolCallRounds`) — jusqu'à quatre phases `llm_stream_round` dans le flux et
    `tool_rounds_executed` dans la ligne `phase=total_request` du log — au lieu du plafond CPU ; la
    même demande sans la bascule repart sur le modèle standard (`usedAdvancedModel:false`, raison
    `null`). Cas limite : si le modèle avancé est un modèle **Ollama** et que le moteur tourne en
    **CPU seul**, le plafond CPU reste appliqué (le budget avancé est réservé au GPU / cloud).

## Doublons et réutilisation de tables (`existingKey`)

> Architecture : [`docs/architecture/studio-ai-duplicates-and-reuse.md`](../architecture/studio-ai-duplicates-and-reuse.md).
> Prérequis : au moins une table Studio existante (ex. `employes` « Employés »). Aucune clé de
> fonctionnalité nouvelle : tout se joue sous `Ollama:EnableStudioAiPlanPreview`.

49. **Bandeau doublon** — « crée un système RH avec une table Employés (nom, poste) et une table
    Demandes » ⇒ l'aperçu du plan affiche la table « Employés » signalée comme **doublon** de la
    table existante `employes` (`summary.duplicates[]` non vide, `reason` = `same_key` /
    `same_name` / `singular_plural`) et un avertissement en clair proposant la réutilisation. Le
    plan reste confirmable tel quel (la table serait recréée sous une clé suffixée) : le signalement
    n'est jamais un blocage. Contre-exemple : « crée une table Contrats » avec `contrats_cadres`
    existante ⇒ **aucun** bandeau (jamais de rapprochement par préfixe).
50. **Réutilisation via `existingKey`** — « crée un système de congés qui s'appuie sur la table
    employes existante et ajoute une table Demandes de congés liée » ⇒ l'aperçu affiche l'étape
    « Tables réutilisées : 1 » ; à la confirmation, l'exécution **ne crée ni ne modifie** la table
    `employes` (aucun appel de création de table/champ/formulaire/état pour elle), la relation de
    « Demandes de congés » pointe vers la clé réelle `employes`, et le payload final distingue
    `createdCount` / `reusedCount`. Cas d'échec : une spec forgée avec `existingKey` inconnu ou
    inactif ⇒ l'exécution échoue **avant** toute création (pas même le système), avec un message qui
    ne cite que la clé demandée. Un lot `seed` visant la table réutilisée est ignoré avec un
    avertissement — jamais d'écriture dans une table existante.



### Avant d'activer `EnableStudioSqlSourceGuard`

41. Exécuter [`docs/runbooks/sql/studio-views-affected-by-guard.sql`](../runbooks/sql/studio-views-affected-by-guard.sql)
    sur chaque base tenant. `FenetresActivesCassees = 0` ⇒ activation sans impact ; sinon, reclasser
    la table ou retirer la fenêtre avant de basculer.

## Relations plusieurs-à-plusieurs (`Ollama:EnableStudioManyToMany`)

> Architecture : [`docs/architecture/studio-many-to-many.md`](../architecture/studio-many-to-many.md).
> Prérequis : migration tenant `20260912130000_AddStudioEntityKind_Tenant` appliquée ; deux tables Studio
> existantes (ex. `employes` « Employés » et `projets` « Projets »), un compte avec la permission de
> conception (`StudioDesignEntities`). Tant qu'aucun écran n'existe (PR 2.5), les appels se font avec
> Swagger / `curl` (jeton porteur du compte).

51. **Création manuelle d'une relation N-N** — `POST api/studio/entities/{idEmployes}/relations/many-to-many`
    corps `{ "targetEntityId": "<idProjets>" }` ⇒ `200` avec `data.junction.key = "employes_projets"`,
    `data.junction.kind = "Junction"` (enum sérialisé en chaîne, stocké `1` en base), `data.sourceField.key = "employes"`, `data.targetField.key = "projets"`,
    les deux champs `isRequired: true`, `isUnique: false`, `relation.kind = "custom"`. Puis
    `GET api/studio/entities/{idEmployes}/relations` ⇒ une entrée `kind: "many_to_many"` dont
    `targetEntityKey = "projets"`, `junctionEntityKey = "employes_projets"` ; `GET api/studio/records/employes/schema`
    ⇒ la même entrée dans `data.relations[]` ; `GET api/studio/entities` liste la jonction avec `kind: "Junction"`.
    Table d'audit : une ligne `Studio.Relation.ManyToManyCreated`. Rejouer le même `POST` ⇒ `200` avec
    `employes_projets_2` ; le rejouer avec `"junctionKey": "employes_projets"` ⇒ `409`. Cas d'erreur :
    `targetEntityId` = source ⇒ `400` ; cible = une jonction ⇒ `400` ; drapeau `false` ⇒ `404` sur les deux
    endpoints et `data.relations = []` sur le schéma, sans autre effet.
52. **Doublon de paire** — `POST api/studio/records/employes_projets` avec
    `{ "data": { "employes": "<idEmp1>", "projets": "<idProj1>" } }` ⇒ `200` ; le **même** corps une seconde
    fois ⇒ **`409`** avec `error = "Ce lien existe déjà."` (code interne `record.duplicate_link` : le statut 409 est
    le discriminant côté client). Paire croisée (`<idEmp1>`, `<idProj2>`) ⇒ créée. `PUT` du second lien vers la
    paire du premier ⇒ `409` ; `PUT` du premier lien sur lui-même (mêmes valeurs) ⇒ `200` (auto-exclusion).
    Corps incomplet (`employes` seul) ⇒ `400` **du validateur** (`isRequired`), pas 409. Supprimer le premier lien
    puis rejouer sa paire ⇒ créée (les lignes supprimées ne comptent pas). Sur une table standard, aucun contrôle
    de paire.
53. **Filtre serveur et navigation** — `GET api/studio/records/employes_projets?filterField=employes&filterValue=<idEmp1>`
    ⇒ seuls les liens d'`<idEmp1>` (`totalCount` exact, cumulable avec `search` et `page`/`pageSize`) ;
    `pageSize=500` ⇒ 200 lignes au plus ; `filterField` seul ou `filterValue` seul ⇒ `400` ; `filterField=inexistant`
    ⇒ `400` ; `filterValue` de 451 caractères ⇒ `400`. Trace SQL (profiler ou log EF `CommandExecuted`) : le
    prédicat est `JSON_VALUE(DataJson, '$.employes') = @p0` — précédé de `[jx_employes] = @p0` si la colonne
    indexée existe — jamais la valeur en clair. `GET api/studio/nav` ⇒ `employes` et `projets` sont présents,
    `employes_projets` **absent** (dans un système comme à la racine) ; la sidebar Studio ne change pas.
54. **Génération IA d'un système avec relation N-N** (`Ollama:EnableStudioManyToMany=true`,
    `EnableStudioAiPlanPreview=true`) — demander à l'atelier IA « un système de formations : employés,
    formations, et un employé peut suivre plusieurs formations ». `studio_plan_system` ⇒ le `spec_json`
    contient `"relations": [{ "kind": "many_to_many", "from": "employes", "to": "formations" }]` et
    **aucun** champ `relationTo` many_to_many sur les entités ; l'aperçu (`GET .../plans/{id}/preview`
    ou SSE) affiche une étape « Relations plusieurs-à-plusieurs » nommant la table de liaison.
    Confirmer le plan ⇒ le système est créé en cinq passes (entités/champs simples, champs relation
    simple, jonctions N-N, formulaires/rapports, données) ; `GET api/studio/entities/{idEmployes}/relations`
    ⇒ une entrée `kind: "many_to_many"` vers `formations` ; le payload de fin de construction contient
    `relations: [{ from: "employes", to: "formations", junctionKey, openUrl }]`. Drapeau
    `EnableStudioManyToMany=false` ⇒ le même prompt ne propose jamais `relations[]` (règle 3e absente du
    prompt) ; si le spec en contient malgré tout (rejoué depuis un aperçu antérieur), la passe jonctions
    est marquée « ignorée » et un avertissement `"Relations N-N non activées (Ollama:EnableStudioManyToMany)."`
    apparaît dans le résultat, sans bloquer la création des tables/champs simples.
55. **Référence en avant et tolérance des relations invalides** — un spec où l'entité `contrats`
    déclare un champ `relation` vers `clients` alors que `clients` est décrite **après** `contrats` dans
    `entities[]` ⇒ la relation résout la vraie clé de `clients` (pas de dégradation en `Text`, grâce aux
    passes 1→2 de l'orchestrateur). Une relation `relations[]` vers une entité inconnue, un auto-lien
    (`from == to`), une paire dupliquée (peu importe l'ordre), ou une 7ᵉ relation au-delà du maximum de 6
    ⇒ ignorés avec un avertissement dans le résultat, **jamais** un rejet franc de tout le spec (à la
    différence d'un champ invalide, qui rejette l'entité).

## Vues enregistrées (`Ollama:EnableStudioRecordViews`)

> Architecture : [`docs/architecture/studio-record-views.md`](../architecture/studio-record-views.md).
> Les cas 60–62 (vues **proposées par l'IA**) exigent en plus `Ollama:EnableStudioAiRecordViewTools: true`
> et `Ollama:EnableStudioAiPlanPreview: true` — capability `recordViewToolsEnabled`.
> Prérequis : migration tenant `20260912140000_AddStudioRecordViews_Tenant` appliquée ; une table Studio
> existante `interventions` (champs `nom` Text, `statut` Select avec options `encours`/`termine`, `debut` Date,
> `montant` Money) alimentée d'enregistrements, un compte avec `StudioDesignForms` (conception) et
> `CustomRecordsRead` (lecture/exécution). Les appels se font avec Swagger / `curl`.

56. **Vue Liste filtrée et triée côté serveur** — `POST api/studio/records/interventions/views` corps
    `{ "key": "en_cours", "displayName": "En cours", "mode": "List", "definition": { "columns": [{ "fieldKey": "nom" }], "filters": [{ "fieldKey": "statut", "op": "eq", "value": "encours" }], "sort": [{ "fieldKey": "debut", "descending": true }], "searchEnabled": true, "pageSize": 25 } }` ⇒ `201` avec
    `data.key = "en_cours"`, `data.mode = "List"` (enum sérialisé en chaîne), `data.isDefault = true` (première
    vue de la table), un `rowVersion` base64 ; la définition est re-validée (filtre `gt` sur `nom` Text ⇒ `400
    Validation.filters`). Puis `POST api/studio/records/interventions/views/{id}/run` corps `{ "page": 1 }` ⇒
    `200` : `data.items[]` ne contient que les `statut = "encours"`, triés par `debut` décroissant,
    `data.total` exact, `data.truncated = false`. `pageSize > 200` ⇒ `400 Validation.pageSize`.
    `GET api/studio/records/interventions/views` ⇒ la vue en tête (défaut d'abord) ;
    `GET api/studio/records/interventions/schema` ⇒ `data.views[]` la contient. Trace SQL : le `WHERE` est
    paramétré (`@t`, `@e`, `@p0`), jamais la valeur en clair, et `SELECT r.*, COUNT(*) OVER()`.
57. **Kanban groupé + borne 500** — `POST …/views` avec `"mode": "Kanban"`, `"kanban": { "groupByFieldKey": "statut", "titleFieldKey": "nom", "showEmptyGroup": true }` ⇒ `201` (un kanban sur un
    champ non-`Select` ⇒ `400 Validation.kanban`). `POST …/views/{id}/run` ⇒ `200` avec `data.groups[]`
    **ordonnés** selon les options (`encours`, `termine`), puis un groupe `{ "value": null, "label": "Sans valeur" }`
    pour les lignes sans statut ; chaque groupe a `count` et `items[]`. Avec plus de 500 enregistrements ⇒
    `data.truncated = true` (au plus 500 cartes chargées, `total` exact). `showEmptyGroup: false` masque les
    colonnes vides.
58. **PATCH partiel avec RowVersion** — `PATCH api/studio/records/interventions/{id}` corps
    `{ "data": { "statut": "termine" }, "rowVersion": "<rowVersion de l'enregistrement>" }` ⇒ `200` : seules
    les clés fournies changent (les autres, dont l'`AutoNumber`, sont conservées), `data.statut = "termine"`.
    Rejouer le **même** corps avec l'ancien `rowVersion` ⇒ `409` (`Conflict`). Corps sans `rowVersion` ⇒
    `400 Validation.rowVersion`. Clé inconnue ou calculée (`Formula`/`Lookup`/`Rollup`/`AutoNumber`) dans
    `data` ⇒ `400 Validation.data` ; `null` sur une clé optionnelle l'efface. La mise à jour déclenche
    l'événement d'automatisation `OnUpdate` existant.
59. **Drapeau coupé** — `EnableStudioRecordViews: false` ⇒ `GET/POST …/views`, `GET/PUT/DELETE …/views/{id}`,
    `POST …/views/{id}/default`, `POST …/views/{id}/run` et `PATCH …/records/{entityKey}/{id}` répondent tous
    `404` **sans effet de bord** ; `GET api/studio/records/interventions/schema` ⇒ `data.views = []` ;
    `GET api/ai/studio/capabilities` ⇒ `recordViewsEnabled: false`. La table `CustomRecordViewDefinitions`
    reste inerte (migration additive).

60. **Vue kanban proposée par l'IA** — `POST api/studio/ai/plans/from-spec` corps
    `{ "kind": "RecordView", "specJson": "{\"entity\":\"interventions\",\"name\":\"Par statut\",\"mode\":\"kanban\",\"groupBy\":\"statut\"}" }`
    ⇒ `200`, `summary.kind = "RecordView"`, étape `mode` = « Kanban », table cible « Interventions » ;
    `POST api/studio/ai/plans/{id}/confirm` ⇒ le résultat porte `openUrl = "/studio/d/interventions?view=<id>"` ;
    `GET api/studio/records/interventions/views` liste la clé `vue_par_statut` en mode `Kanban`. Un compte
    sans `StudioDesignForms` ⇒ `403` à la confirmation (la vue relève de la conception des affichages).
61. **Calendrier dégradé en liste** — même appel avec
    `"{\"entity\":\"interventions\",\"name\":\"Agenda\",\"mode\":\"calendrier\",\"start\":\"nom\"}"`
    (champ `nom` Text) ⇒ `200` avec résumé étape `mode` = « **Liste** » et `warnings[]` contenant
    « Calendrier impossible … » ; la confirmation crée une vue **Liste** (jamais un échec, jamais une
    promesse de calendrier impossible).
62. **Trois vues max par table dans un système** — spec système (`studio_plan_system` ou workbench)
    avec 4 `views` sur une même entité ⇒ résumé `viewCount = 3` + avertissement « Au plus 3 vues par
    table » ; la confirmation montre la progression `creating_views` ×3. Avec
    `EnableStudioAiRecordViewTools: false` ⇒ étape `creating_views` `skipped` + avertissement,
    `GET api/ai/studio/capabilities` ⇒ `recordViewToolsEnabled: false`, l'outil
    `studio_plan_record_view` est absent du catalogue, et `from-spec` avec `kind: "RecordView"` ⇒
    `400 Validation.kind` (« Les vues enregistrées par l'IA ne sont pas activées. »).

## Migrations

- `20260624181553_AddStudioSystems_Tenant` (systèmes multi-tables).
- `20260728001141_AddStudioAiBuildPlans_Tenant` (plans « aperçu → confirmation »).
  Jumeau idempotent : `docs/runbooks/sql/AddStudioAiBuildPlans_Tenant.idempotent.sql`.
- `20260912130000_AddStudioEntityKind_Tenant` (colonne `CustomEntityDefinitions.Kind`, défaut 0 = Standard,
  index `(TenantId, Kind)` — relations N-N). Jumeau idempotent : `docs/runbooks/sql/AddStudioEntityKind_Tenant.idempotent.sql`.
- `20260912140000_AddStudioRecordViews_Tenant` (table `CustomRecordViewDefinitions`, FK cascade vers
  `CustomEntityDefinitions`, index unique filtré `(TenantId, EntityDefinitionId, Key) WHERE IsDeleted = 0`,
  index `(TenantId, EntityDefinitionId, IsDefault)` — vues enregistrées). Jumeau idempotent :
  `docs/runbooks/sql/AddStudioRecordViews_Tenant.idempotent.sql`.
- `20260912150000_AddStudioWorkflows_Tenant` (4 tables autonomes `StudioWorkflowDefinitions`,
  `StudioWorkflowInstances`, `StudioWorkflowStepRuns`, `StudioWorkflowApprovals`, 9 index dont la clé
  unique filtrée `(TenantId, EntityDefinitionId, Key) WHERE IsDeleted = 0`, aucune FK — workflows
  Studio). Jumeau idempotent :
  `docs/runbooks/sql/AddStudioWorkflows_Tenant.idempotent.sql`.

## Portée automatisée

- Backend : `dotnet test src\Backend\tests\FactuTrust.Infrastructure.Tests --filter "FullyQualifiedName~.Studio"`
  (parsers, planificateur de diff, exécuteurs, cycle de vie des plans, catalogue d'outils, rendu PDF,
  politique d'accès aux tables, constructeur SQL des états, préréglages, digest de contexte).
- Backend (doublons + réutilisation) : `--filter "FullyQualifiedName~StudioAiDuplicateDetector|FullyQualifiedName~StudioAiSystemSpec|FullyQualifiedName~StudioAiSystemOrchestrator"`
  (rapprochements, parsing `existingKey`, exécution sans écriture sur les tables réutilisées).
- Backend (contexte + modèle avancé) : `--filter "FullyQualifiedName~SendChatMessageHandlerStudioAdvancedModel|FullyQualifiedName~AiContextBuilderStudioDigest|FullyQualifiedName~StudioContextDigestService"`
  et `dotnet test src\Backend\tests\FactuTrust.API.Tests --filter "FullyQualifiedName~FactuTrust.API.Tests.Studio"`
  (`AiChatOptionsContractTests` + contrats des contrôleurs Studio ; c'est ce filtre qu'exécute `azure-pipelines.yml`
  sous Linux — le projet complet, qui exige LocalDB, tourne dans le workflow GitHub `CI` sous Windows).
- Frontend (runtime vues + relations N-N, PR 2.5) : `npx ng test --include='src/app/features/studio/**/*.spec.ts'`
  (specs Karma `views/`, `shared/`, `relations/`, `studio-record-form`, `studio-entity-list`,
  `studio-entity-designer`) ; E2E API mockée : `npx playwright test e2e/studio-runtime-views.spec.ts
  e2e/studio-many-to-many.spec.ts --project=chromium`.
- Backend (relations N-N) : `--filter "FullyQualifiedName~CreateManyToManyRelation|FullyQualifiedName~ListEntityRelations|FullyQualifiedName~CustomRecordJunctionUniqueness|FullyQualifiedName~CustomRecordRepositoryFilterSql|FullyQualifiedName~GetStudioNavQuery|FullyQualifiedName~AddStudioEntityKind"`
  (composition de la jonction, résolution des relations, unicité de paire, filtre SQL paramétré — nécessite
  `FACTUTRUST_TEST_SQL_CONNECTION` ou LocalDB, sinon `Skipped` —, nav sans jonction, migration `Kind`) ;
  contrats API : `StudioEntityRelationsControllerContractTests` et `StudioRecordsControllerContractTests`
  dans le filtre `FactuTrust.API.Tests.Studio`.
- Backend (relations N-N dans la spec IA + orchestrateur multi-passes, PR 2.2) :
  `--filter "FullyQualifiedName~StudioAiSystemOrchestrator|FullyQualifiedName~StudioAiSystemSpec|FullyQualifiedName~StudioAiSpecCanonical|FullyQualifiedName~AiContextBuilderStudio|FullyQualifiedName~StudioAiPlanCatalogTests"`
  (alias de `kind`, promotion de champ, cap de 6 relations, dédoublonnage de paire, cinq passes de
  l'orchestrateur, forme canonique de `relations[]`, règle 3e du prompt derrière le drapeau).
- Backend (vues enregistrées proposées par l'IA, PR 2.4) :
  `--filter "FullyQualifiedName~StudioAiRecordViewSpec|FullyQualifiedName~StudioAiPlanExecutor|FullyQualifiedName~StudioAiSystemOrchestrator|FullyQualifiedName~StudioAiPlanCreationFeatures|FullyQualifiedName~StudioSilentFailureGuards|FullyQualifiedName~AiToolRegistryStudioRecordViewTools"`
  (parsing/alias et résolution contre le schéma réel avec avertissements, borne 3 vues/entité, passe 4
  de l'orchestrateur et son ordonnancement, exécution du plan `RecordView`, garde des trois drapeaux,
  catalogue d'outils et `StudioPlanEmittingTools`) ; contrats API `validate`/`from-spec` kind
  `RecordView` dans `StudioAiPlansControllerContractTests` (filtre `FactuTrust.API.Tests.Studio`).
- Historique/aperçu/rejeu (PR 3.2) : `--filter "FullyQualifiedName~StudioAiSeedSampler|FullyQualifiedName~StudioAiPlanPreviewBuilder|FullyQualifiedName~StudioAiPlanPreviewFeatures"`,
  contrat `StudioAiPlansControllerContractTests` (filtre `FactuTrust.API.Tests.Studio`) et
  compteurs de liste dans `StudioAiPlanWorkbenchFeaturesTests` / `StudioAiPlanFeaturesTests`.
- Export / duplication / import + modèles enrichis (PR 3.3) : `--filter "FullyQualifiedName~StudioSystemSpecExporter|FullyQualifiedName~CustomSystemExportFeatures|FullyQualifiedName~StudioTemplateCatalog|FullyQualifiedName~StudioAiSpecCanonical|FullyQualifiedName~StudioKey|FullyQualifiedName~CustomSystemFeatures"`
  (aller-retour export → `TryParse`, dégradations et warnings, bornes de seed, gardes des deux drapeaux, nom
  « (copie) », import chaîne/objet/`specVersion`, 10 modèles et `StudioTemplateStats`, test d'or, clé réservée
  `import`) ; contrats API `StudioSystemsControllerContractTests` et `StudioTemplatesControllerTests`
  (filtre `FactuTrust.API.Tests.Studio`).
- Workflows Studio (PR 4.1) : `--filter "FullyQualifiedName~StudioWorkflow"` (`StudioWorkflow*Tests` :
  entités, spec / validation / lint des étapes, contexte et gabarits, handlers d'étapes, moteur segmenté,
  déclencheur, dépôt et migration — les tests SQL exigent `FACTUTRUST_TEST_SQL_CONNECTION`, sinon `Skipped` —,
  `StudioWorkflowFeaturesTests` pour les handlers de conception) ; contrat API
  `StudioWorkflowsControllerContractTests` (filtre `FactuTrust.API.Tests.Studio`).
- Frontend : `ng test --watch=false --browsers=ChromeHeadless` (service de plans + flux SSE de confirmation).
- Gate complet : `powershell -File scripts\verify-all.ps1`.

## Atelier IA frontend — coquille, modèle avancé, doublons, rail (PR 1.4)

> Prérequis : `EnableStudioAiPlanPreview=true`, `EnableStudioAiAdvancedModel=true` avec un modèle Studio
> avancé configuré en back-office, `EnableStudioTemplates=true` ; une table Studio `employes` « Employés »
> existante. Navigateur ≥ 1280 px de large pour la disposition 3 colonnes (rail à droite) ; réduire à
> 1024 px pour vérifier l'empilement (rail sous la colonne principale). Documentation utilisateur :
> [`docs/utilisateur/13-studio-ia.md`](../utilisateur/13-studio-ia.md) (chapitre `studio-ia`).
> Automatisé : `ng test --include='src/app/features/studio/**/*.spec.ts'` et
> `npx playwright test e2e/studio-ai-atelier.spec.ts` (backend entièrement mocké — `e2e/helpers/studio-mock.helpers.ts`).

91. **Thème indigo scopé et modèle avancé** — `/studio/ai` : l'en-tête, les boutons primaires et les
    overlays PrimeNG (menu déroulant d'un `p-select` dans `/studio/forms`, dialogue de confirmation) sont
    **indigo** ; `/dashboard` et `/invoices` restent **bleu** FactuTrust (aucun `.studio-theme` hors
    `/studio/**`, mode sombre inclus). Sous la zone de saisie, l'interrupteur **Modèle avancé** affiche le
    libellé du modèle configuré ; l'activer puis envoyer « crée un système de gestion des congés » ⇒ la
    requête `POST api/ai/chat` (flux SSE) porte `options.useAdvancedModel: true` et `options.studioIntent`
    (`system` si la carte « Système complet » a été choisie) ; l'événement SSE `meta` renvoie
    `usedAdvancedModel: true` ⇒ bandeau vert « Généré avec le modèle avancé » (`data-testid="model-advanced"`). Recharger la page ⇒ l'interrupteur
    est **toujours actif** (mémorisé en `localStorage`). Désactiver le modèle avancé en back-office
    (`EnableStudioAiAdvancedModel=false` ou modèle retiré) et renvoyer ⇒ `meta.usedAdvancedModel: false`
    + `advancedModelFallbackReason` (`disabled` / `not_configured` / `unavailable`) ⇒ bandeau
    **« Modèle standard utilisé : … »** (`data-testid="model-fallback"`, `role="status"`) avec la raison
    traduite ; la proposition reste exploitable. Si `capabilities.advancedModelAvailable = false`, l'interrupteur
    n'apparaît pas et aucune option `useAdvancedModel` n'est envoyée.
92. **Bandeau doublons — Réutiliser / Créer quand même** — « crée un système RH avec une table Employés
    (nom, poste) et une table Contrats » ⇒ `studio_plan.summary.duplicates[]` non vide ⇒ au-dessus de
    l'aperçu, le bandeau **« La table « Employés » existe déjà »** (`.sai-dup`) cite la table existante et la
    raison (`same_key` / `same_name` / `singular_plural`). **Réutiliser la table existante** ⇒
    `PUT api/studio/ai/plans/{id}/spec` avec `entities[0].existingKey = "employes"`, le bandeau disparaît et
    l'onglet Vue d'ensemble affiche « Tables réutilisées : 1 » (la confirmation ne recrée ni champ ni
    formulaire pour `employes`, smoke 50). Rejouer la demande puis **Créer quand même** ⇒ l'entité est renommée
    **« Employé (2) » / « Employés (2) »** (clé suffixée), mise en surbrillance dans l'onglet Tables, le bandeau
    disparaît **sans** réapparaître au prochain rendu (`dismissedDuplicateRefs`) ; cliquer une seconde fois ne
    produit pas « (2) (2) ». Le bandeau n'a **pas** de croix de fermeture : il disparaît dès que chaque doublon
    a été tranché (Réutiliser / Créer quand même) ; tant qu'un doublon est affiché, le plan reste confirmable
    tel quel.
93. **Message pendant un plan en attente, Réinitialiser, Nouvelle demande** — avec une proposition
    **À valider** affichée, saisir « ajoute une table Formations » dans le composeur situé **sous l'aperçu** et
    envoyer ⇒ dialogue **« Une proposition est en attente »** ; **Annuler** ⇒ rien n'est envoyé, le plan reste
    affiché ; **Abandonner le plan et envoyer** ⇒ `POST api/studio/ai/plans/{id}/cancel` **puis** un nouveau
    `POST api/ai/chat` (jamais l'inverse), l'ancien plan passe **Annulé** dans l'historique. Rail →
    **Réinitialiser la conversation** ⇒ confirmation, puis `POST api/studio/ai/plans/cancel-pending` (toujours,
    même sans plan affiché), `DELETE api/ai/conversations/{id}` seulement si une conversation existe, toast
    « 1 plan(s) en attente annulé(s). » (ou « Conversation réinitialisée. » sans plan), composeur vide, cartes d'intention
    de nouveau visibles, historique rafraîchi. En-tête → **Nouvelle demande** ⇒ même réinitialisation ; la
    confirmation n'est demandée que si un plan est affiché. Une carte grisée « Bientôt » (Page) ne fait rien
    au clic ; **Workflow** n'est active que si `capabilities.workflowToolsEnabled = true`.
94. **Rail, modèles, historique et pages dédiées** — le rail affiche **Modèles de systèmes** (3 max, badge
    « Intégré » / « Votre espace »), **Actions rapides**, **Historique** (5 dernières générations, statuts
    À valider / En cours / Terminé / Échec / Annulé / Expiré, dates relatives) et la carte de suggestion.
    **Utiliser** sur un modèle ⇒ `POST api/studio/ai/plans/from-template` `{ templateKey }`, la proposition s'ouvre dans l'aperçu
    sans appel au modèle ; avec un plan déjà affiché ⇒ confirmation « Remplacer la proposition en cours ? ».
    Cliquer une ligne **À valider** de l'historique ⇒ `GET api/studio/ai/plans/{id}` et reprise dans l'aperçu ;
    une ligne Terminé/Annulé n'est pas cliquable. **Voir tout** ⇒ `/studio/ai/projects` : tableau paginé
    **20 par page**, filtres Statut / Genre (retour page 1), compteur « N projet(s) », **Reprendre** ⇒
    `/studio/ai?plan={id}` (rouvre le plan puis nettoie l'URL ; plan expiré ⇒ bandeau d'erreur en ligne
    « Ce plan n'est plus en attente… » — `store.error`, pas un toast), **Ouvrir le système** ⇒
    `/studio/systems/{key}`. **Voir tous** (modèles) ⇒
    `/studio/ai/templates` : cartes groupées par catégorie (« Autres » pour les modèles sans catégorie),
    **Utiliser ce modèle** ⇒ `/studio/ai?template={key}` ⇒ plan ouvert. Avec `templatesEnabled = false`
    ⇒ carte Modèles absente et page `/studio/ai/templates` « La bibliothèque de modèles est désactivée par l’administrateur. » ;
    `planPreviewEnabled = false` ⇒ carte Historique absente, aucun appel `GET api/studio/ai/plans`.
    Sans `systemExportEnabled`, **Exporter le système (JSON)** et **Partager** sont grisés « Bientôt » ;
    Importer / Dupliquer n'apparaissent qu'avec l'export activé (toast « arrive dans une prochaine version »).

## Runtime des vues et relations N-N — frontend (PR 2.5)

95. **Sélecteur de vues** : `/studio/d/<table>` avec vues dans le schéma ⇒ onglets « Liste » +
    vues enregistrées, vue par défaut active (`POST …/views/{id}/run`) ; `?view=<id>` force une vue
    (Kanban rendu groupé) ; schéma sans vues ⇒ liste brute d'avant 2.5, aucun appel `/run`.
96. **Concepteur de vue** : `/studio/d/<table>/views/new` — clé auto-slug immuable en édition, bornes
    (25 colonnes / 10 filtres / 3 tris / 6 champs carte) ; 409 création ⇒ « Une vue porte déjà cette
    clé. », 409 édition ⇒ « La vue a été modifiée ailleurs ; rechargez. » + « Recharger », 400 « Limite
    du plan… » ⇒ bandeau quota ; suppression confirmée ⇒ retour liste ; aperçu R3 en édition
    (`previewLimit=20`, « Actualiser l'aperçu »).
97. **Dialog N-N** : concepteur de table, capability `manyToManyEnabled` seule ⇒ bouton « Ajouter une
    relation plusieurs-à-plusieurs » (après « Pont ERP ») + section Relations ; dialog : cible sans la
    source ni les jonctions, clé `{source}_{cible}` par défaut, bloc « Attribut de liaison — Bientôt » ;
    409 ⇒ « Une table de liaison porte déjà cette clé. », succès ⇒ toast + rechargement.
98. **Onglet « Liés » + page Relations** : fiche en édition avec N-N ⇒ onglets Fiche / Liés — ⟨cible⟩ ;
    ajout ⇒ POST jonction, 409 `record.duplicate_link` ⇒ « Lien déjà existant. » en ligne, liste
    inchangée ; retrait ⇒ DELETE jonction/{id} ; bandeau tronqué si `totalCount > pageSize`. Sans
    `custom_records:write`, Ajouter/Retirer masqués. `/studio/relations` ⇒ tableau dédoublonné +
    diagramme SVG (`role="img"`) ; `/studio` ⇒ jonctions masquées par défaut, badge « Jonction » après
    décoche.

## Amendements enrichis et changement de type (PR 3.1)

> Architecture : [`docs/architecture/studio-ai-amendments.md`](../architecture/studio-ai-amendments.md).
> Prérequis : une table Studio `interventions` (champs `nom` Text, `statut` Select avec options,
> `debut` Date, `montant` Money) alimentée d'enregistrements, un compte avec `studio:design_entities` ;
> pour les cas IA : `Ollama:EnableStudioAiPlanPreview=true` + `Ollama:EnableStudioAiModifyTools=true`
> (+ `EnableStudioManyToMany` / `EnableStudioRecordViews` selon le cas).
> **Smoke chat non exécuté** : le parcours conversationnel complet exige un modèle Ollama (absent de
> l'environnement de vérification) — les cas 64–66 vérifient donc la chaîne serveur via
> `POST api/studio/ai/plans/validate` (kind `Amendment` : parsing + canonicalisation, alias absorbés)
> et renvoient aux tests automatisés (`StudioAiAmendmentSpecTests` / `StudioAiAmendmentPlannerTests` /
> `StudioAiSpecCanonicalTests` / `StudioAiAmendmentExecutorTests`) pour le contenu de l'aperçu
> (étapes, sévérités, avertissements), `studio_plan_changes` partageant le même parseur et le même
> planificateur. Avec un modèle branché, rejouer ces demandes en langage naturel dans l'atelier.

63. **Changement de type : vérification puis application** — avec 3 enregistrements dans
    `interventions`, `GET api/studio/entities/{idEntite}/fields/{idMontant}/type-check?to=Number` ⇒
    `200` `{ from: "Money", to: "Number", policy: "requires_empty_table", recordCount: 3, allowed: false }`
    avec le message « Ce changement exige une table vide (3 enregistrement(s))… » ;
    `PATCH …/type` corps `{ "fieldType": "Number" }` ⇒ `400 Validation.fieldType` (même message, aucune
    écriture). Vider la table puis rejouer ⇒ `200`, `data.fieldType = "Number"` ; audit
    `Studio.Field.TypeChanged` présent. `to=wizard` ou `to=42` ⇒ `400 Validation.to` ;
    `?to=Formula` ⇒ `200` `policy: "forbidden"` (« Ce type se crée comme un nouveau champ… ») ;
    `PATCH` vers le même type ⇒ `400` « Le champ est déjà de ce type. ». Cas sans perte :
    `type-check?to=Decimal` ⇒ `policy: "lossless", allowed: true`, et le `PATCH` réussit même table
    non vide ; la colonne calculée `jx_montant` est conservée (partagée, indépendante du type).
64. **Réordonnancement et changement de type (DSL d'amendement)** — `POST api/studio/ai/plans/validate`
    corps `{ "kind": "Amendment", "specJson": "{\"target\":{\"entityKey\":\"Interventions\"},\"operations\":[{\"op\":\"reordonner_champs\",\"fields\":[\"montant\",\"Nom\",\"fantome\"]},{\"op\":\"change_field_type\",\"key\":\"montant\",\"type\":\"decimal\"}]}" }`
    ⇒ `200`, spec canonique retournée avec `target.entityKey = "interventions"` (slugifié), l'alias
    `reordonner_champs` absorbé en `reorder_fields` et le type en `"decimal"` ; à l'aperçu (outil chat
    / tests) : étape « Réordonner les champs » promettant l'ordre effectif complet (`montant, nom,
    statut, debut` — « Nom » résolu par libellé, champs non cités conservés à la suite) et avertissement
    « Champ « fantome » introuvable : retiré de la réorganisation. » ; l'étape `change_field_type`
    annonce `montant` → `décimal` « sans perte ». `"type": "number"` sur une table non vide ⇒ étape en
    **avertissement** « table vide » SANS nombre fabriqué (le compte est revérifié à l'application) ;
    `"type": "formula"` ⇒ étape **en erreur** « Ce type se crée comme un nouveau champ… » ;
    `"type": "3"` (valeur numérique d'énumération) ⇒ op écartée « type inconnu ».
65. **Relation, système et vue (DSL d'amendement), drapeaux fonctionnels** — même appel avec
    `{ "op": "add_relation", "kind": "many_to_many", "target": "Compétences", "label": "Compétences requises" }`,
    `{ "op": "assign_system", "system": "Gestion Interventions" }` et
    `{ "op": "set_view", "mode": "kanban", "displayName": "Par statut", "groupBy": "statut" }` ⇒ `200`,
    forme canonique `kind: "many_to_many"`, `target: "competences"` (slugifiée),
    `system: "gestion_interventions"`, vue sans clé `entity` (table du plan implicite). Aperçu (flag
    `EnableStudioManyToMany` on) : étape « Relier à « Compétences requises » » ; `"target":
    "interventions"` (la table elle-même) ⇒ étape **en erreur** « La table cible doit être différente
    de la table source. ». Flag off ⇒ l'op est écartée avec avertissement « … relations
    plusieurs-à-plusieurs ne sont pas activées » (plan refusé si c'était la seule op) — idem
    `set_view` avec `EnableStudioRecordViews=false`. Flag views on : la vue est résolue contre le
    schéma réel — `"groupBy": "nom"` (Text) ⇒ l'étape annonce « Liste » + avertissement « Kanban
    impossible… » (dégradation, jamais d'échec). Détachement : `{ "op": "assign_system", "system":
    "none" }` ⇒ aperçu « Détacher la table de son système ».
66. **Exécution réelle des amendements (3.1c/3.1d) — chaque op laisse une étape** — à l'application
    d'un plan `Amendment` confirmé, le suivi affiche une étape par opération au statut `done`,
    `skipped` ou `error` (jamais de succès muet) : `reorder_fields` applique l'ordre promis à
    l'aperçu (les champs de `interventions` sont réordonnés en base, visibles au rechargement du
    concepteur) ; `change_field_type` applique les conversions permises et remonte une étape en
    erreur non bloquante quand le handler refuse (`Forbidden` / table non vide) ; `assign_system`
    rattache la table (rechargée sous le bon système) et `system: "none"` la détache ; `add_relation`
    `many_to_one` crée le champ relation, `many_to_many` crée la table de jonction — avec
    `EnableStudioManyToMany=false` l'étape est `skipped` et **aucune** écriture n'a lieu (vérifier
    l'absence de jonction en base) ; `set_view` crée la vue enregistrée (clé dédupliquée si le nom
    existe déjà) — avec `EnableStudioRecordViews=false` ⇒ `skipped`, aucune écriture ; une cible de
    relation inconnue ou de type jonction ⇒ `skipped` + avertissement. `set_automation` reste valide
    au parsing (`validate` ⇒ `200`), est présenté « Automatisation (non appliquée) » à l'aperçu et
    remonte `skipped_automation` à l'exécution ; un plan sans rien d'applicable échoue explicitement
    (« Aucune modification appliquée »). Une op inconnue de l'exécuteur lève (défaut bruyant, couvert
    par `StudioSilentFailureGuardsTests`). Le prompt système StudioBuilder porte la règle 8 enrichie
    (cinq opérations actionnables listées, révision de cache « v7 » —
    `AiContextBuilderStudioDigestTests`).

## Historique, aperçu structuré et rejeu des plans (PR 3.2)

> Architecture : [`docs/architecture/studio-ai-plans.md`](../architecture/studio-ai-plans.md).
> Prérequis : `Ollama:EnableStudioAiPlanPreview=true`, un compte avec `studio:design_entities`,
> au moins un plan système **confirmé** (ex. `conges` créé) et un plan **échoué** (ex. spec
> validée puis table cible supprimée avant exécution, ou plan annulé/expiré). AUCUN appel LLM :
> l'historique, l'aperçu et le rejeu ne lisent que la spec persistée — ces cas s'exécutent contre
> l'API seule (ou l'atelier une fois le frontend 3.4 livré).

67. **Historique : compteurs relations/vues et lien Ouvrir** — `GET api/studio/ai/plans` ⇒ `200`,
    chaque élément porte les 14 clés du contrat (dont `errorMessage`, `openUrl`, `relationCount`,
    `replayable` ; `viewCount` est omis du JSON tant qu'il vaut 0). Le plan système confirmé
    affiche `relationCount`/`viewCount` cohérents avec son résumé et `openUrl` vers le système
    créé (`/studio/systems/conges`) — le lien **Ouvrir** de Mes projets l'utilise ; le plan échoué
    porte `errorMessage` lisible. `replayable` est `true` sur les quatre états terminaux (et sur
    un plan « À valider » dont l'heure d'expiration est dépassée), `false` sinon. Jamais de clé
    `spec`/`specJson` dans la réponse. Flag `EnableStudioAiPlanPreview` coupé ⇒ `404`
    « Le flux d'aperçu Studio n'est pas activé. » sur la liste comme sur `cancel-pending`.
68. **Aperçu structuré d'un plan système et d'un amendement (dégradé)** —
    `GET api/studio/ai/plans/{id}/preview` sur le plan confirmé ⇒ `200`, `workflows: []` toujours,
    `entities[]` avec champs (`required`/`unique`, options de liste), `formLayout.sections[].fields[]`
    en objets `{ key, width, labelOverride }` (jamais des chaînes), vues proposées, `seedCount`
    réel et `seedSample` borné à 3 lignes / 80 caractères par valeur (troncature « … »),
    `relations[]` typées, `warnings[]`/`duplicates[]` relus du résumé. Amendement dont la table
    cible existe ⇒ diff « avant → après » par opération ; table cible **supprimée** entre-temps ⇒
    `200` DÉGRADÉ (`amendment.degraded: true`, un item par opération demandée, avertissement
    « Table introuvable : aperçu limité aux opérations demandées. »), jamais d'erreur. Plan d'un
    AUTRE utilisateur (ou tenant) ⇒ `404` ; flag coupé ⇒ `404` même message qu'au cas 67 ;
    permission retirée ⇒ `401`.
69. **Rejeu d'un plan échoué ⇒ nouveau plan en attente** — `POST api/studio/ai/plans/{id}/replay`
    sur le plan échoué ⇒ `201 Created` avec en-tête `Location: /api/studio/ai/plans/{nouvelId}` ;
    le corps porte un plan `Pending` NEUF dont le résumé contient `replayedFromPlanId` = id du
    plan d'origine. Le plan d'origine est INCHANGÉ (statut `Failed`, `errorMessage` intact — le
    recharger pour preuve). Le nouveau plan apparaît en tête de l'historique et se confirme par
    le flux SSE habituel (création réelle au clic Valider, jamais au rejeu). Rejeu d'un plan
    « À valider » non échu ou « En cours » ⇒ `409` « Seul un plan terminé, échoué, annulé ou
    expiré peut être rejoué. » ; plan d'un autre utilisateur ⇒ `404` ; flag coupé ⇒ `404`
    « Le flux d'aperçu Studio n'est pas activé. ». Rejouer deux fois le même plan crée deux plans
    distincts (idempotence : aucune écriture de schéma au rejeu).

## Export, duplication et import de systèmes ; modèles enrichis (PR 3.3)

> Architecture : [`docs/architecture/studio-system-export.md`](../architecture/studio-system-export.md).
> Prérequis : `Ollama:EnableStudioSystemExport=true`, `Ollama:EnableStudioAiPlanPreview=true` ; un
> système Studio existant (ex. créé depuis le modèle `gestion-conges`) ; un compte avec
> `studio:design_entities`. AUCUN appel LLM : export, duplication et import ne lisent que le schéma
> persisté — ces cas s'exécutent contre l'API seule (ou l'atelier une fois le frontend 3.4 livré).

70. **Export sans données de départ** — `GET api/studio/systems/{key}/export` ⇒ `200` avec
    `specVersion: 1`, `includesSeed: false`, `entityCount`/`relationCount`/`viewCount` cohérents avec
    le concepteur ; la `spec` porte `specVersion`, `exportedFrom { tenantSystemKey, exportedAt }`,
    `system`, `entities[]`, `relations[]` (présent seulement si le système porte des relations
    N-N — ex. `gestion_de_projets`, clé du système créé depuis le modèle « Gestion de projets ») et ne contient ni `seed`, ni identifiant (`Guid`), ni
    `tenantId`. Avec `?download=true`, le navigateur télécharge `studio-system-{key}.json` (spec
    seule, indentée, accents lisibles). Drapeau `EnableStudioSystemExport=false` ⇒ `404`
    « L'export de systèmes Studio n'est pas activé. » sans appel côté application ; clé avec tiret
    ⇒ `400` « Clé système invalide. » ; clé inconnue ⇒ `404` `CustomSystem.NotFound`.
71. **Export avec données de départ bornées et anonymisées** — `?includeSeed=true` sur un système
    dont une table compte plus de `StudioExportMaxSeedRows` lignes (baisser le réglage à `2` pour le
    test) : `includesSeed: true`, au plus 2 lignes par table, **aucun** avertissement de troncature
    par table (la borne est appliquée à la lecture, avant l'exporteur — l'avertissement « Données
    de départ de « {key} » tronquées à {n} ligne(s). » n'apparaît que si le total de 200 lignes est
    atteint), valeurs des champs relation / pièce jointe / signature / formule à `null`, total
    ≤ 200 lignes. Réglage à `0` ⇒ `includesSeed: false` et aucune clé `seed`.
72. **Duplication ⇒ plan ⇒ système copié** — `POST api/studio/systems/{key}/duplicate` (corps vide)
    ⇒ `201 Created` avec `Location: /api/studio/ai/plans/{id}` et un corps `{ plan, spec }` ; l'aperçu
    du plan (QA 68) montre « <Nom> (copie) », les tables homonymes dans `duplicates[]` et les
    warnings d'export dans `warnings[]` ; la confirmation crée un système dont les clés de tables déjà
    prises sont suffixées `_2` et les relations N-N recréées ; le système source est INCHANGÉ ;
    audits `Studio.System.Exported` puis `Studio.System.DuplicateRequested` présents. Avec
    `{ "displayName": "Congés 2027" }`, le nom est repris tel quel ; un nom de 129 caractères ⇒ `400`
    « Le nom affiché dépasse 128 caractères. ». `EnableStudioAiPlanPreview=false` ⇒ `404`
    « Le flux d'aperçu Studio n'est pas activé. ».
73. **Import d'un export** — `POST api/studio/systems/import` avec `{ "spec": <corps de la spec du
    cas 70> }` ⇒ `201` et plan Pending équivalent (mêmes compteurs) ; `{ "spec": "<le même JSON sous
    forme de chaîne>" }` accepté ; `"specVersion": 2` ⇒ `400` « Version de spécification non prise en
    charge : 2. » ; spec > 256 Ko ⇒ `400` « La spec dépasse 256 Ko. » ; corps > 512 Ko ⇒ `413` ;
    `"spec": "pas du json"` ⇒ `400` « La spécification n'est pas un JSON valide. » ;
    `"includeSeed": false` sur un export avec seed ⇒ aperçu sans données de départ ;
    `"displayNameOverride": "Congés importés"` renomme le système du plan ; audit
    `Studio.System.ImportRequested` présent. Clé réservée par la route : un système « Import »
    créé via l'assistant ou un modèle reçoit la clé `import_2` ; `POST api/studio/systems` avec
    `key: "import"` ⇒ `400` « Clé système réservée. ».
74. **Modèle `gestion-projets` de bout en bout** — `GET api/studio/templates` liste **10** modèles
    avec `relationCount`/`viewModes` (`gestion-projets` : 4 tables, 1 relation,
    `["list","kanban","calendar"]`) ; créer un système depuis `gestion-projets` : 4 tables + jonction
    « Équipe projet », vues kanban « Par statut » et liste « Mes tâches » (tâches), calendrier
    « Jalons », liste « Projets actifs » filtrée `statut = actif`, rapport « Tâches par statut »,
    5 lignes de départ ; l'export de ce système (cas 70) redonne `relationCount: 1` et
    `viewCount: 4`, et sa duplication (cas 72) recrée la relation N-N.

## Aperçu IA enrichi — frontend (PR 3.4)

> Architecture : [`docs/architecture/studio-ai-preview-frontend.md`](../architecture/studio-ai-preview-frontend.md).
> Prérequis : `EnableStudioAiPlanPreview` et `EnableStudioSystemExport` activés (`planPreviewEnabled` /
> `systemExportEnabled` dans `GET api/studio/ai/capabilities`), un compte avec `studio:design_entities`,
> une proposition « Système complet » ouverte dans l'atelier (`/studio/ai`). Suite Playwright miroir :
> `e2e/studio-ai-preview.spec.ts` (7 cas, backend mocké). Les numéros 75–90 (backend 4.x) et 103–106 (4.4)
> restent réservés.

99. **Tester sans écriture** : barre de modes de l'aperçu ⇒ **Tester** ⇒ un seul `GET api/studio/ai/plans/{id}/preview`
    (jamais rejoué tant que le plan est le même), puis le formulaire de la première table rendu par le
    formulaire dynamique réel ; saisir les champs requis puis **Enregistrer** ⇒ message de simulation en
    ligne, **aucune** requête `POST`/`PUT`/`PATCH`/`DELETE` sur `api/studio/*` (vérifier dans l'onglet
    Réseau) ; carte « Rapport » alimentée par `summary.sample` sinon par l'agrégation locale des données
    de départ, bouton Exporter du rapport inactif (`aria-disabled`). `GET …/preview` ⇒ `404` (flag
    serveur absent) ⇒ pilule « Disponible après mise à jour du serveur. » sur le bouton Tester, panneau
    toujours fonctionnel depuis la spec seule, aucun bandeau d'erreur global. Revenir à **Aperçu** ne
    déclenche aucun appel.
100. **Personnaliser : brouillon 409 + import/export aller-retour** : **Personnaliser** ⇒ renommer un champ
    dans l'onglet Tables ⇒ badge « 1 » sur le bouton Personnaliser ⇒ **Enregistrer le brouillon** ⇒
    `PUT api/studio/ai/plans/{id}/spec` avec le `rowVersion` courant ⇒ fil « Modifications enregistrées
    dans le plan. », badge à 0, nouveau `rowVersion` mémorisé. Rejouer avec un `rowVersion` périmé (second
    onglet qui a enregistré entre-temps) ⇒ `409` ⇒ bandeau d'erreur `role="alert"` « Ce plan a été modifié
    entre-temps. Rechargez l’aperçu. » au-dessus de l'aperçu, brouillon **conservé** (badge et bouton
    toujours actifs), rien n'est écrasé. Puis, sur un système créé : carte résultat ou rail ⇒ **Exporter
    (JSON)** ⇒ dialog « Exporter le système » (`p-select` des systèmes quand aucune clé n'est préréglée),
    `GET api/studio/systems/{key}/export` ⇒ compteurs « n tables · n relations · n vues », **Télécharger**
    ⇒ fichier `studio-system-<clé>.json` (spec seule, identique à `?download=true`) ; rail ⇒ **Importer un
    modèle (JSON)** ⇒ déposer ce fichier ⇒ « Spécification reconnue » + compteurs identiques ⇒ **Importer**
    ⇒ `POST api/studio/systems/import` (`displayNameOverride` optionnel, `includeSeed`) ⇒ `201` ⇒ la
    proposition s'ouvre dans l'aperçu avec les mêmes compteurs. Fichier > 256 Ko ⇒ « Fichier trop
    volumineux (256 Ko maximum). » **sans** lecture ni appel réseau ; JSON invalide ⇒ « JSON invalide. » ;
    `specVersion: 2` ⇒ « specVersion non pris en charge (1 attendu). ».
101. **Expiration ⇒ Régénérer ⇒ `POST …/replay`** : laisser le compte à rebours de la barre de modes
    atteindre 0 (ou ouvrir un plan dont `expiresAt` est passé) ⇒ pilule **Expiré**, **Créer maintenant**
    désactivé, boutons de mode désactivés, bouton **Régénérer** visible ⇒ clic ⇒
    `POST api/studio/ai/plans/{id}/replay` ⇒ `201` ⇒ nouvelle proposition **À valider** ouverte à la place
    (fil « Plan rejoué : une nouvelle proposition est ouverte. »), historique du rail rafraîchi. Même
    action depuis le rail (**Rejouer** sur une ligne `replayable`, confirmation « Remplacer la proposition
    en cours ? » si un plan est affiché) et depuis **Mes projets** (`/studio/ai/projects`, bouton Rejouer ⇒
    `201` ⇒ redirection `/studio/ai?plan=<nouvel id>`). `409` ⇒ « Ce plan ne peut pas être rejoué
    maintenant. » (bandeau dans l'atelier, toast dans Mes projets), plan source inchangé.
102. **Changement de type refusé désactive Enregistrer ; `lossless` ⇒ PATCH `…/type`** : concepteur de
    table (`/studio/<entityId>`) ⇒ modifier un champ existant ⇒ changer le type dans la liste (plus
    grisée en édition) ⇒ « Vérification… » puis
    `GET api/studio/entities/{entityId}/fields/{fieldId}/type-check?to=<Type>` (anti-rebond, dernière
    valeur seule) : `lossless` ⇒ `p-message` `info` avec le message serveur verbatim, **Enregistrer**
    actif ⇒ clic ⇒ `PATCH api/studio/entities/{entityId}/fields/{fieldId}/type` puis `PUT` du champ ⇒
    dialog fermé, liste des champs rechargée avec le nouveau type ; `requires_empty_table` ⇒ message
    `warn` avec le nombre d'enregistrements bloquants,
    **Enregistrer désactivé** ; `forbidden` ⇒ message `error`, **Enregistrer désactivé** ; revenir au type
    d'origine ⇒ message effacé, Enregistrer actif, aucun `PATCH` émis. Pendant la vérification le bouton
    reste désactivé (pas de double soumission).

## Workflows Studio — moteur, déclencheur et API de conception (PR 4.1)

> Architecture : [`docs/architecture/studio-workflows.md`](../architecture/studio-workflows.md).
> Prérequis : migration tenant `20260912150000_AddStudioWorkflows_Tenant` appliquée ; drapeau activé **par
> variable d'environnement** `Ollama__EnableStudioWorkflows=true` (les deux `appsettings*.json` restent à
> `false`) ; une table Studio `commandes` (champs `statut` Select `brouillon`/`valide`, `montant` Money,
> `traite_le` DateTime, `ref` AutoNumber) et une table `taches` (champ `titre` Text) alimentées ; un compte
> avec `studio:design_entities` (conception) et `custom_records:write` (déclenchement). Appels Swagger /
> `curl` — le frontend arrive en PR 4.4. Les numéros 75–80 sont ceux du registre du plan maître ; l'ordre du
> fichier suit les PR.

75. **Drapeau éteint** — sans `Ollama__EnableStudioWorkflows`, appeler les 11 routes de
    `StudioWorkflowsController` (`GET api/studio/workflows/step-catalog`, `GET/POST api/studio/entities/{entityId}/workflows`,
    `POST …/workflows/validate`, `GET/PUT/DELETE api/studio/workflows/{id}`, `POST …/toggle`, `POST …/duplicate`,
    `GET …/instances`, `GET api/studio/workflows/instances/{instanceId}`) ⇒ `404`
    « Les workflows Studio ne sont pas activés. » partout, aucune trace côté application ;
    `GET api/ai/studio/capabilities` ⇒ `workflowsEnabled=false`, `workflowToolsEnabled=false` ; créer puis
    modifier un enregistrement de `commandes` ⇒ `201` / `200` habituels et **aucune ligne** dans
    `StudioWorkflowInstances`.
76. **Création, quota, clé** — drapeau activé : `POST api/studio/entities/{entityId}/workflows` corps
    `{ "key": "relance", "name": "Relance", "trigger": "field_changed", "triggerConfig": { "field": "statut", "to": "valide" }, "steps": { "version": 1, "steps": [ { "key": "verif", "type": "condition", "filters": [{ "field": "montant", "op": "gt", "value": 100 }] }, { "key": "maj", "type": "update_field", "set": { "traite_le": "{{ _now }}" } } ] }, "isActive": true }`
    ⇒ `201`, en-tête `Location` vers `GET api/studio/workflows/{id}`, `version=1`, `stepCount=2`,
    `openInstances=0`, `rowVersion` base64 ; ligne d'audit `Studio.Workflow.Created`. Re-`POST` avec la même
    clé (ou `RELANCE`) ⇒ `409` « Un workflow avec la clé « relance » existe déjà pour cette table. ». Créer
    ensuite jusqu'à 20 workflows puis un 21ᵉ ⇒ `400` message de quota (`Validation.Plan`). `GET …/entities/{entityId}/workflows`
    liste les 20, actifs et inactifs. Modifier un enregistrement de `commandes` en passant `statut` à `valide` avec
    `montant > 100` ⇒ `GET api/studio/workflows/{id}/instances` montre une instance `completed`, `trigger=field_changed`,
    `depth=0` ; `GET api/studio/workflows/instances/{instanceId}` ⇒ deux step runs `succeeded` (`verif` puis `maj`),
    `context.previous = null`, `traite_le` renseigné sur l'enregistrement.
77. **Validation des étapes** — `POST api/studio/entities/{entityId}/workflows/validate` avec, tour à tour :
    `type: "teleport"` ; `update_field` sur un champ `inconnu` ; `condition` avec `onFalse: "goto"` et `gotoKey`
    vers une étape **antérieure** ; 31 étapes ⇒ toujours `200` avec `isValid=false` et `errors[].path` =
    `steps[0].type`, `steps[0].set` (« Champ « inconnu » inconnu, inactif ou calculé. »), `steps[1].gotoKey`
    (« doit référencer une étape postérieure »), `steps` (31 > 30) ;
    `warnings[]` peut lister les étapes jamais atteintes. `PUT api/studio/workflows/{id}` du workflow du cas 76
    avec les mêmes étapes et son `rowVersion` ⇒ `400` dont le message est celui de la **première** issue,
    suivi de « (+n autre(s) erreur(s) — utilisez la validation pour la liste complète.) » s'il y en a
    plusieurs ; `GET` ⇒ définition inchangée (`version` identique, mêmes étapes).
78. **Déclencheur planifié refusé** — `POST` et `PUT` avec `"trigger": "scheduled"` ⇒ `400`
    « Déclencheur planifié : bientôt disponible. » ; `POST …/validate` avec le même corps ⇒ `200`
    `isValid=false`, une issue `path="trigger"` avec ce message ; aucune définition créée ni modifiée.
79. **Anti-boucle** — workflow **A** sur `commandes`, `trigger: "on_update"`, une étape `update_field`
    (`set: { "montant": "{{ montant }}" }`) : modifier un enregistrement ⇒ `GET workflows/{A}/instances` ⇒
    **une seule** instance `completed`, `depth=0`, pas de relance par sa propre écriture. Workflow **B** sur
    `taches`, `trigger: "on_create"`, étape `create_record` vers `commandes` (`set: { "statut": "brouillon" }`)
    et workflow **C** sur `commandes`, `on_create`, étape `create_record` vers `taches` : créer une tâche ⇒ B
    (`depth=0`) crée une commande ⇒ C (`depth=1`, `originInstanceId` = instance de B) crée une tâche ⇒ B
    (`depth=2`, `originInstanceId` renseigné) **s'arrête** (aucune instance de profondeur 3, aucun
    enregistrement supplémentaire) ; les instances au-delà de la borne n'existent pas, celles créées sont
    `completed` ou `failed` avec un message explicite — jamais `running` bloquée.
80. **`erp_action` via le Pont ERP** — `POST …/validate` avec une étape `erp_action` dont `action` n'est
    pas dans `StudioBridgeActionCatalog.List()` ⇒ `isValid=false`, `errors[].path = steps[i].action`. Avec une
    action bridgeable et `saveResultAs: "res"` sur un workflow `on_update` (le lancement `manual` arrive en
    PR 4.2) : modifier l'enregistrement ⇒ instance `completed`, `GET workflows/instances/{id}`
    montre le step run `erp_action` `succeeded` avec `result` non nul, `context.results.res` renseigné ; les
    journaux applicatifs portent la corrélation `studio-workflow:<instanceId>:<clé d'étape>` **sans** valeurs
    d'enregistrement. Simuler un échec (action bridgeable sur un enregistrement incomplet) : `onFailure`
    absent ⇒ instance `failed`, step run `failed`, notification `StudioWorkflowStepFailed` (type 17) au
    lanceur avec lien `/studio/d/commandes/{recordId}/edit` ; `onFailure: "continue"` ⇒ step run `failed`,
    instance poursuivie jusqu'à `completed`.
