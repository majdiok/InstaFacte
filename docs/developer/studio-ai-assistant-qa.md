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

## Migrations

- `20260624181553_AddStudioSystems_Tenant` (systèmes multi-tables).
- `20260728001141_AddStudioAiBuildPlans_Tenant` (plans « aperçu → confirmation »).
  Jumeau idempotent : `docs/runbooks/sql/AddStudioAiBuildPlans_Tenant.idempotent.sql`.
- `20260912130000_AddStudioEntityKind_Tenant` (colonne `CustomEntityDefinitions.Kind`, défaut 0 = Standard,
  index `(TenantId, Kind)` — relations N-N). Jumeau idempotent : `docs/runbooks/sql/AddStudioEntityKind_Tenant.idempotent.sql`.

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
- Backend (relations N-N) : `--filter "FullyQualifiedName~CreateManyToManyRelation|FullyQualifiedName~ListEntityRelations|FullyQualifiedName~CustomRecordJunctionUniqueness|FullyQualifiedName~CustomRecordRepositoryFilterSql|FullyQualifiedName~GetStudioNavQuery|FullyQualifiedName~AddStudioEntityKind"`
  (composition de la jonction, résolution des relations, unicité de paire, filtre SQL paramétré — nécessite
  `FACTUTRUST_TEST_SQL_CONNECTION` ou LocalDB, sinon `Skipped` —, nav sans jonction, migration `Kind`) ;
  contrats API : `StudioEntityRelationsControllerContractTests` et `StudioRecordsControllerContractTests`
  dans le filtre `FactuTrust.API.Tests.Studio`.
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
    produit pas « (2) (2) ». Fermer le bandeau (croix) ⇒ le plan reste confirmable tel quel.
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
    `/studio/ai?plan={id}` (rouvre le plan puis nettoie l'URL ; plan expiré ⇒ toast « Ce plan n'est plus en
    attente… »), **Ouvrir le système** ⇒ `/studio/systems/{key}`. **Voir tous** (modèles) ⇒
    `/studio/ai/templates` : cartes groupées par catégorie (« Autres » pour les modèles sans catégorie),
    **Utiliser ce modèle** ⇒ `/studio/ai?template={key}` ⇒ plan ouvert. Avec `templatesEnabled = false`
    ⇒ carte Modèles absente et page `/studio/ai/templates` « La bibliothèque de modèles est désactivée par l’administrateur. » ;
    `planPreviewEnabled = false` ⇒ carte Historique absente, aucun appel `GET api/studio/ai/plans`.
    Sans `systemExportEnabled`, **Exporter le système (JSON)** et **Partager** sont grisés « Bientôt » ;
    Importer / Dupliquer n'apparaissent qu'avec l'export activé (toast « arrive dans une prochaine version »).
