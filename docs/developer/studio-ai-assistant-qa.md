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

### Avant d'activer `EnableStudioSqlSourceGuard`

41. Exécuter [`docs/runbooks/sql/studio-views-affected-by-guard.sql`](../runbooks/sql/studio-views-affected-by-guard.sql)
    sur chaque base tenant. `FenetresActivesCassees = 0` ⇒ activation sans impact ; sinon, reclasser
    la table ou retirer la fenêtre avant de basculer.

## Migrations

- `20260624181553_AddStudioSystems_Tenant` (systèmes multi-tables).
- `20260728001141_AddStudioAiBuildPlans_Tenant` (plans « aperçu → confirmation »).
  Jumeau idempotent : `docs/runbooks/sql/AddStudioAiBuildPlans_Tenant.idempotent.sql`.

## Portée automatisée

- Backend : `dotnet test src\Backend\tests\FactuTrust.Infrastructure.Tests --filter "FullyQualifiedName~.Studio"`
  (parsers, planificateur de diff, exécuteurs, cycle de vie des plans, catalogue d'outils, rendu PDF,
  politique d'accès aux tables, constructeur SQL des états, préréglages, digest de contexte).
- Backend (contexte + modèle avancé) : `--filter "FullyQualifiedName~SendChatMessageHandlerStudioAdvancedModel|FullyQualifiedName~AiContextBuilderStudioDigest|FullyQualifiedName~StudioContextDigestService"`
  et `dotnet test src\Backend\tests\FactuTrust.API.Tests --filter "FullyQualifiedName~FactuTrust.API.Tests.Studio"`
  (`AiChatOptionsContractTests` + contrats des contrôleurs Studio ; c'est ce filtre qu'exécute `azure-pipelines.yml`
  sous Linux — le projet complet, qui exige LocalDB, tourne dans le workflow GitHub `CI` sous Windows).
- Frontend : `ng test --watch=false --browsers=ChromeHeadless` (service de plans + flux SSE de confirmation).
- Gate complet : `powershell -File scripts\verify-all.ps1`.
