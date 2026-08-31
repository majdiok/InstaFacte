# Studio IA — états sur les tables réelles de la solution

**Date :** 16 août 2026
**Périmètre :** moteur d'états SQL du Studio + outils d'états de l'assistant Studio
**Documents liés :** [QA de l'assistant Studio](../developer/studio-ai-assistant-qa.md) · [Rapports (utilisateur)](../utilisateur/08-rapports.md)

---

## 1. Le problème résolu

Avant ce chantier, la demande « crée un rapport avancé des ventes de produits » sur `/studio/ai`
répondait invariablement « Je n'ai pas pu lancer la création du système ». Trois causes cumulées :

1. **Aucun outil d'état n'était exposé au modèle.** `studio_build_report`, `studio_extract_record`,
   `studio_list_custom_tables` et `studio_query_records` figuraient dans les catalogues StudioBuilder
   mais n'avaient **aucune définition** dans `AiToolRegistry.All`. Comme la sélection s'écrit
   `All.Where(t => set.Contains(t.Name))`, ces quatre noms filtraient dans le vide.
2. **Le message d'échec était inadapté et trop facile à déclencher** : `LooksLikeStudioSpecText`
   renvoyait `true` dès qu'un bloc ` ```json ` apparaissait dans la prose.
3. **Le catalogue de sources était famélique** : 3 sources (`clients`, `products`, `invoices`) de
   5 champs, chargées en mémoire et plafonnées à 5 000 lignes **sans le dire**.

---

## 2. Principe directeur

> **Le modèle ne produit jamais de SQL. Il nomme un état ou une source ; le SQL est écrit par du code.**

C'est la généralisation de la frontière déterministe déjà appliquée à la trésorerie prévisionnelle :
le chiffre est calculé par la base, le mot est écrit par le modèle.

---

## 3. Chaîne d'exécution

```
Spécification (préréglage OU source + champs)
        │
        ▼
SqlReportAccessPolicy      table classée ? permission détenue ? colonne autorisée ?
        │  deny-by-default
        ▼
SqlReportEngine            photographie du schéma réel (colonnes + clés étrangères, 2 sauts max)
        │
        ▼
SqlReportSqlBuilder        pur, sans dépendance → SELECT paramétré + requête de comptage
        │
        ▼
ReportResultDto            colonnes, lignes, TotalRows exact, Truncated
```

### 3.1 Les trois verrous

| Verrou | Mise en œuvre |
|---|---|
| Aucun SQL ne vient du modèle | `SqlReportSqlBuilder` n'insère que des identifiants présents dans la photographie du schéma, bracket-quotés. Toute valeur de filtre est un paramètre `@pN`. Un identifiant inconnu est **écarté avec un avertissement**, jamais deviné. |
| Aucune jointure libre | Les jointures sont issues d'un parcours en largeur sur les **clés étrangères réelles** (`sys.foreign_key_columns`), 2 sauts et 3 tables jointes au maximum. Sans chemin de clé étrangère : refus explicite, jamais de produit cartésien. |
| Aucun accès non autorisé | `SqlReportAccessPolicy` classe chaque table par domaine et exige `reports:view` **plus** la permission du domaine. Une table non classée est `Inconnu` ⇒ **refusée**. La règle s'applique aussi aux tables **traversées** par une jointure. |

### 3.2 Clés de champ

Format `Table_Colonne` (`InvoiceLines_Quantity`, `Clients_Name`). La levée d'ambiguïté teste le
préfixe contre les tables réellement présentes dans la photographie : une colonne contenant un
souligné (`UnitPrice_Amount`) reste donc correctement résolue sur la table de faits.

Suffixes de **granularité de date** : `__day`, `__month`, `__quarter`, `__year`. L'expression SQL
correspondante (`CONVERT(char(7), …, 126)` pour le mois) est écrite dans le code — « par mois » est
une valeur d'un ensemble fermé, pas un fragment de requête.

### 3.3 Deux niveaux d'usage

- **Préréglages** (`SqlReportPresetCatalog`) — une vingtaine d'états métier écrits en dur. Le modèle
  n'a qu'à en nommer un et donner une période : cela tient en **un seul tour d'outil**, seule option
  viable sur CPU (`CpuMaxToolCallRounds = 1`). Un préréglage dont une colonne n'existe pas est
  **masqué** (`ResolvesAgainst`) plutôt qu'exécuté à moitié.
- **Spécification libre** — `studio_describe_report_source` puis une spec source/champs, confrontée
  au schéma vivant. Réservée aux configurations à plusieurs tours d'outil (GPU).

---

## 4. Exactitude des chiffres

L'agrégation est faite **par SQL** : les totaux sont exacts quel que soit le volume, contrairement au
chemin historique en mémoire. Seul le **détail** reste plafonné (`StudioReportMaxRows`), et il est
alors signalé : `ReportResultDto.Truncated` alimente un bandeau dans l'interface **et** une ligne dans
le PDF. Le même signalement a été rétrofité sur les deux sources historiques, dont la troncature à
5 000 lignes était jusqu'ici silencieuse.

Le préréglage `ventes_par_produit` reproduit exactement le prédicat de
`InvoiceRepository.GetSalesRevenueAggregatedAsync` (factures `Validated` ou `Paid`, période sur
`Invoices.IssueDate`, mesure `SUM(InvoiceLines.Total)`) — d'où le contrôle croisé au millime exigé en
recette contre `/reports/sales-by-line`.

---

## 4 bis. Le raccourci déterministe — pourquoi il existe

Les premiers essais en conditions réelles ont montré que le modèle Studio **n'émettait aucun appel
d'outil** (`had_tool_calls=false` dans les logs), quel que soit le soin porté au prompt : le modèle
configuré était un modèle de *code*, qui écrit du JSON en prose plutôt que d'appeler une fonction.
La réponse tombait alors dans le filet anti-silence, dont le libellé parlait de création de système.

`StudioReportIntentRouter` reconnaît la demande et **exécute l'état avant d'appeler le modèle**, sur
le patron du raccourci de contrôle de conformité déjà en place. Le modèle ne fait plus que rédiger.

Trois propriétés à préserver :

1. **En cas de doute, on n'exécute rien.** Score insuffisant, ou message décrivant une structure à
   créer (« table X avec les champs… ») ⇒ aucun raccourci, le flux normal reprend la main.
2. **La période retenue est annoncée.** Sans période exprimée, le défaut est l'année en cours, et le
   modèle a consigne de l'énoncer — l'utilisateur corrige d'un message.
3. **`toolsExecutedThisRequest++` réactive la synthèse forcée.** Avec
   `ForceFinalSynthesisOnlyAfterTools: true`, c'est ce qui rend la bulle vide structurellement
   impossible sur ce chemin.

Le raccourci est piloté par `EnableStudioReportShortcut`. Il ne remplace pas les outils : quand la
demande est trop fine pour un préréglage, le modèle garde la main — avec un catalogue **focalisé**
(`StudioToolFocus.Report`) qui divise par trois le poids des schémas exposés.

## 4 ter. La panne CPU silencieuse — corrigée

Les sous-ensembles d'outils CPU (`AiToolIntentRouter.CpuCoreToolNames`) ont été conçus pour dégrossir
le catalogue **Default** (~90 outils) sur une machine sans GPU. Leur garde d'armement était
`!isScoped`, qui ne couvre que le mode Default : or **tous** les modes focalisés forcent l'intent
`Fallback` et ne sont jamais « scopés ». Ils armaient donc le sous-ensemble et se faisaient
intersecter avec une liste ne contenant aucun de leurs outils propres.

Dégâts constatés sur une plateforme réglée en « CPU uniquement » :

| Mode | Effet |
|---|---|
| **StudioBuilder** | Catalogue réduit à **un seul** outil (`propose_follow_up_prompts`) — **aucun `studio_*`**. Panne totale et muette. |
| **Compliance** | Perte de `compliance_check_invoice`, le contrôle qui définit le mode. |
| **ScreenAnalysis** | Perte de `resolve_reporting_period`, la garde anti-hallucination de date. |

La décision d'armement est désormais explicite et testable :
`AiToolIntentRouter.CpuSubsetApplies(mode, agentScope, isCpuOnly)` — vrai **uniquement** pour le
catalogue Default non scopé. Le dégrossissement voulu sur ce catalogue est conservé à l'identique.
Garde : `CpuToolSubsetScopeTests`, qui reproduit le pipeline complet de `BuildOllamaTools` — jusque-là
seul `GetDefinitionsForMode` était couvert, ce qui laissait passer le défaut.

## 5. Deux sorties, un seul moteur

| Outil | Nature | Effet |
|---|---|---|
| `studio_list_report_sources` | lecture seule | Préréglages + sources autorisées pour cet utilisateur |
| `studio_describe_report_source` | lecture seule | Champs réels d'une source |
| `studio_run_report` | lecture seule | Calcule et renvoie le tableau — événement `studio_report_result`, rendu dans la conversation. **N'enregistre rien.** |
| `studio_plan_report` | mutation | Prépare un plan `StudioAiPlanKind.Report` avec un **échantillon de vraies lignes** dans l'aperçu ; l'enregistrement passe par l'endpoint REST de confirmation, jamais par le LLM |

Le bouton « Enregistrer comme état » du résultat affiché **renvoie la demande à l'assistant** : il
n'existe aucun chemin d'écriture direct depuis le client.

L'exécution du plan appelle `UpsertCustomReportCommand` — la **même** commande que le concepteur
humain. Un état créé par l'IA est en tout point un état Studio ordinaire : réexécutable, imprimable,
modifiable dans le concepteur.

---

## 6. Non-régression

| Décision | Raison |
|---|---|
| `CustomReportDataSourceKind.SqlQuery = 2` et `StudioAiPlanKind.Report = 4` ajoutés **en fin** d'énumération | Valeurs persistées en `int` ⇒ aucune migration, aucune réécriture de données |
| Contrat `ReportDefinition` inchangé | Les états déjà enregistrés se relisent et s'exécutent à l'identique |
| `ReportResultDto.Truncated` et `ReportSourceDto.Domain` : paramètres optionnels en fin de record | Aucun appelant existant cassé |
| `CustomReportRunner` et les deux branches historiques de `ReportExecutor` non modifiés | `SqlQuery` est une **troisième** branche |
| Ajouts append-only dans `AiToolRegistry.All` | Règle inscrite dans le fichier |
| Tout derrière drapeaux, `false` par défaut | Drapeaux off ⇒ catalogue, prompt et messages strictement identiques à avant |

### Les drapeaux

| Drapeau | Défaut code | Prod initiale | Rôle |
|---|---|---|---|
| `EnableStudioSqlReportEngine` | `false` | `false` | Moteur d'états sur les tables réelles (concepteur humain **et** IA) |
| `EnableStudioAiReportTools` | `false` | `false` | Outils `studio_*_report` de l'assistant. Sans effet si le moteur est off |
| `EnableStudioSqlSourceGuard` | `false` | `false` | Étend le classement par domaine aux **fenêtres**. Passer le runbook d'impact avant activation |
| `StudioReportMaxRows` | `5000` | — | Lignes de détail renvoyées (plafond dur 50 000) |
| `StudioReportCommandTimeoutSeconds` | `30` | — | Protection de la base tenant |

---

## 7. Durcissement de la liste de refus

`SqlSchemaGuard.DeniedTables` ne couvrait que 6 des 11 tables internes du Studio. Les 5 manquantes
(`CustomFieldSequences`, `CustomSystemDefinitions`, `CustomEntityAutomations`,
`CustomAutomationRuns`, `StudioAiBuildPlans`) plus `DataProtectionKeys` ont été ajoutées — aucune n'a
d'usage métier légitime dans une fenêtre.

`SqlSchemaGuard.IsDeniedColumn` (nouveau) écarte secrets, empreintes et jeton de concurrence. Il est
appliqué **au moteur d'états** ; son extension aux fenêtres reste derrière
`EnableStudioSqlSourceGuard`, car durcir l'introspection historique pourrait rendre inutilisable une
fenêtre déjà enregistrée. Impact à mesurer avec
[`docs/runbooks/sql/studio-views-affected-by-guard.sql`](../runbooks/sql/studio-views-affected-by-guard.sql).

---

## 8. Points d'entrée du code

| Sujet | Emplacement |
|---|---|
| Politique d'accès (domaines, permissions, colonnes) | `Application/Features/Studio/Common/SqlReport/SqlReportAccessPolicy.cs` |
| Constructeur SQL (pur, testé) | `Application/Features/Studio/Common/SqlReport/SqlReportSqlBuilder.cs` |
| États prêts à l'emploi | `Application/Features/Studio/Common/SqlReport/SqlReportPresetCatalog.cs` |
| Moteur d'exécution | `Infrastructure/Services/Studio/SqlReportEngine.cs` |
| Spécification IA d'un état | `Application/Features/Studio/Ai/StudioAiReportSpec.cs` |
| Outils IA | `Infrastructure/Services/AI/AiToolExecutor.StudioReports.cs` |
| Exécution du plan confirmé | `Infrastructure/Services/Studio/StudioAiPlanExecutor.cs` (`ExecuteReportAsync`) |
| Branchement dans les états Studio | `Application/Features/Studio/Reports/CustomReportFeatures.cs` |
| Rendu (tableau, graphique, export, troncature) | `Frontend/.../shared/studio-runtime/dynamic-report.component.ts` |
