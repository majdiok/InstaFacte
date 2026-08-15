# Stratégie IA FactuTrust — feuille de route de différenciation

**Version :** 1.0
**Date :** 14 août 2026
**Périmètre :** capacités IA de la plateforme FactuTrust (.NET 8 / Angular, multi-tenant, marché Tunisie — PME + cabinets comptables)
**Objectif :** définir un portefeuille de fonctionnalités IA **innovantes, réalistes et défendables**, ancrées dans l'architecture existante, qui créent un écart durable face à Odoo, Sage, Dynamics BC et aux solutions locales.
**Document lié :** [Analyse des écarts ERP](erp-gap-analysis.md)

---

## 1. Synthèse exécutive

### 1.1 Le constat

FactuTrust n'a **pas** de retard sur l'IA : la plateforme est déjà en avance sur la quasi-totalité du marché tunisien. L'inventaire (§2) recense **plus de 90 outils métier exposés au tool-calling**, un pipeline d'import de factures OCR + vision, une analyse d'écran multimodale, un moteur de prévisions, une trésorerie prévisionnelle assistée, un agent cabinet et une infrastructure multi-fournisseurs (Ollama local, OpenRouter, Cursor SDK).

Le problème n'est donc pas la quantité de fonctions IA — c'est que **ces fonctions sont, dans leur nature, réplicables**. Un concurrent qui branche un LLM sur son API arrive en 6 mois à un « assistant qui répond aux questions sur mes données ». Le chat conversationnel, la génération de texte et le résumé de tableau de bord sont devenus des **commodités** en 2026.

### 1.2 La thèse de différenciation

> **Ce qui ne se copie pas, ce n'est pas le modèle — c'est la boucle.**

Quatre sources de défendabilité sont accessibles à FactuTrust et à personne d'autre sur ce marché :

| Source de défendabilité | Pourquoi FactuTrust peut, et les autres non |
|---|---|
| **1. Boucle de données propriétaire** | Chaque proposition IA validée/corrigée par un comptable devient une donnée d'apprentissage. Au bout de 12 mois, l'IA « connaît la maison ». Un concurrent qui arrive n'a pas ces 12 mois. **C'est le seul moat qui se renforce avec le temps.** |
| **2. Boucle fermée sur l'action** | FactuTrust possède déjà l'écriture comptable, le paiement, le stock, la paie, la déclaration. Un copilote qui *observe* vaut peu ; un copilote qui *propose une écriture équilibrée, imputée, lettrée et prête à valider* vaut une demi-journée par dossier. Les copilotes greffés (Copilot, add-ons) n'ont pas accès à la transaction. |
| **3. Connaissance réglementaire tunisienne** | TVA, RS/TEJ, CNSS, NCT, liasse, formulaires DGI tamponnés : FactuTrust les modélise **déjà** en code. Un RAG réglementaire branché sur ces objets métier produit des réponses *vérifiables sur le dossier réel*. Odoo et Sage n'auront jamais cette profondeur locale. |
| **4. Effet cabinet (multi-dossiers)** | FactuTrust héberge simultanément la PME **et** son cabinet. Cela ouvre deux capacités structurellement inaccessibles à un ERP on-premise : la **supervision transversale de portefeuille** et le **benchmark sectoriel anonymisé**. |

### 1.3 Ce qui est proposé

- **4 briques de socle** (105 j/h) sans lesquelles aucune des fonctionnalités ci-dessous n'est réellement défendable : mémoire vectorielle, boucle d'apprentissage, orchestration avec garde-fous, évaluation + FinOps IA.
- **15 fonctionnalités** réparties en 4 vagues (≈ 610 j/h), chacune spécifiée : mécanique déterministe vs générative, points d'ancrage dans le code existant, garde-fous, KPI, effort.
- **Une doctrine d'autonomie** en 5 niveaux (§4.3) qui rend le déploiement progressif, mesurable et acceptable par un comptable — condition sine qua non d'adoption sur ce métier.

| Indicateur | Valeur |
|---|---|
| Effort socle (Vague 0) | **~105 j/h** — 2 à 3 mois, 2 devs |
| Effort portefeuille complet | **~610 j/h** |
| Total programme IA | **~715 j/h** — 14 à 18 mois, équipe 3–4 devs + 1 expert-comptable référent |
| Première mise en marché différenciante | **Vague 1, mois 5–6** (auto-comptabilisation apprenante) |

---

## 2. État des lieux — l'actif IA existant

Analyse du codebase au 14 août 2026. Cet inventaire n'est pas un exercice de style : **chaque fonctionnalité proposée en §5 s'y raccorde explicitement**, ce qui divise l'effort par rapport à un développement ex nihilo.

### 2.1 Socle d'inférence

| Brique | Emplacement | Maturité |
|---|---|---|
| Client Ollama local (GPU/CPU, gate de concurrence, keep-alive, profils d'inférence) | `Infrastructure/Services/AI/OllamaHttpClient.cs`, `OllamaGenerationGate.cs`, `OllamaInferenceProfileResolver.cs` | Production |
| Client OpenAI-compatible / OpenRouter (clés chiffrées, masquées) | `Infrastructure/Services/AI/OpenAiChatCompletionsClient.cs`, `PlatformAiSettingsService.cs` | Production |
| Pont Cursor SDK (process Node piloté, callbacks d'outils) | `Infrastructure/Services/AI/CursorSdkBridgeHost.cs` | Production |
| Détection de capacités modèle (vision, tool-calling, contexte) | `Application/Features/AI/AiModelCapabilityDetector.cs` | Production |
| Recommandation matérielle (choix du modèle selon la machine) | `Infrastructure/Services/AI/OllamaModelRecommender.cs` | Production |
| Réglages IA plateforme (modèle par usage : assistant / import / studio) | `API/Controllers/PlatformAiSettingsController.cs` | Production |

**Lecture :** l'abstraction multi-fournisseurs est déjà là. Le routage *local vs cloud* — pilier de l'argument souveraineté (§9) — est un réglage, pas un chantier.

### 2.2 Assistant métier (tool-calling)

- **~93 outils** déclarés dans `Application/Features/AI/Tools/AiToolRegistry.cs` (1 752 lignes) : lecture (CA, balances âgées, stock, marge, panier moyen, performances produit), écriture (créer client/produit/facture, encaisser, valider, signer, envoyer), prévisionnel (`forecast_revenue`, `get_cash_flow_forecast`, `forecast_product_demand`, `get_replenishment_recommendations`, `get_abc_xyz_classification`, `simulate_promotion_impact`), Studio low-code, et **outils cabinet** (`get_firm_portfolio_overview`, `get_firm_fiscal_deadlines`, `get_firm_dossier_health`, `get_firm_collaborator_workload`, `send_fiscal_deadline_reminder`).
- Contrôles d'accès : `AiAgentScopeCatalog.cs`, `FirmDelegatedAiScopePolicy.cs` — les outils sont filtrés par périmètre et par licence de module.
- Robustesse : cache d'outils déterministes (`AiDeterministicToolCache`), éligibilité au parallélisme (`AiToolParallelEligibility`), limiteur de classement (`AiRankingLimiter`), repli déterministe (`AssistantDeterministicFallback`).

### 2.3 Compréhension documentaire

| Capacité | Emplacement |
|---|---|
| Import facture (OCR Tesseract + rendu PDF→image + vision, qualité OCR, politique de bascule vision) | `Features/AI/Commands/ImportInvoiceFromFileCommand.cs`, `InvoiceImportOcrQuality.cs`, `InvoiceImportVisionPolicy.cs`, `Services/AI/TesseractOcrService.cs`, `PdfToImagePdfRenderer.cs`, `ImagePreprocessingService.cs` |
| Extraction structurée avec pipeline de réparation JSON | `Features/AI/AiStructuredExtractionPipeline.cs`, `Features/AI/Json/` |
| Import de relevé bancaire PDF (parseur TN, classification débit/crédit) | `Services/BankStatementPdfImportService.cs`, `TunisianBankStatementTextParser.cs`, `BankOperationClassifier.cs` |
| Proposition d'écriture comptable depuis un document | `Services/DocumentImport/AccountingEntryProposalService.cs` (687 lignes) |

### 2.4 Analyse d'écran multimodale

`Features/AI/AiScreenAnalysisPromptBuilder.cs`, `AiScreenAnalysisModeDetector.cs`, `AiScreenAnalysisEnricher.cs`, `AiScreenAnalysisPostProcessor.cs` + configuration fine par écran (`appsettings.json` → `ScreenAnalysis`) + **jeux de tests « golden »** (`docs/ai-screen-analysis/golden/*.json`). C'est une capacité rare et un embryon d'évaluation systématique déjà en place.

### 2.5 Analytique augmentée

- **Prévisions** : `Features/Forecasting/`, réappro V2 avec bons de commande automatiques, ABC/XYZ, saisonnalité, calendrier commercial tunisien.
- **Trésorerie prévisionnelle** : `Features/Treasury/` + `docs/architecture/treasury-cash-forecast.md`. **Architecture exemplaire à généraliser** : les montants et dates sont *strictement déterministes*, le LLM n'intervient qu'ensuite pour pondérer les probabilités dans une borne étroite (`MaxProbabilityShiftPoints: 15`) et rédiger alertes et recommandations. Voir §4.4.
- **Audit comptable continu** : `Services/AccountingAudit/AccountingAuditEngine.cs` + registre de règles (`Rules/`, codes : `drafts`, `unbalanced`, `suspense`, `unlettered`, `depreciation`, `vat`, `sequence-gaps`, `health-piece-duplicates`, `entry-missing-attachment`, `vat-deductible-no-proof`, `recon-bank-incomplete`…), avec cycle de vie d'anomalie (Open / Corrected / Ignored) et journal d'activité.
- **Ordonnancement** : Hangfire déjà utilisé pour `FiscalReminderJob`, `FirmMissionBriefingJob`, `AccountingAuditScheduledJob`, `CashFlowRecomputationJob`, `RecurringEntriesJob` (`API/Services/Background/HangfireRecurringJobsRegistrationService.cs`).

### 2.6 Canal conversationnel

Pont WhatsApp piloté par l'API (`Features/Channels/`, `Services/Channels/ChannelLinkService.cs`, `API/Services/Channels/ChannelInboundOrchestrator.cs`). Aujourd'hui : liaison de compte + questions libres routées vers l'IA en **lecture seule**.

### 2.7 Les trois manques structurants

L'analyse du code fait apparaître **trois absences** qui, à elles seules, expliquent pourquoi l'IA actuelle ne crée pas encore de barrière concurrentielle :

1. **Aucune représentation vectorielle.** Recherche `Embedding|cosine|vector` : aucun résultat fonctionnel dans le backend. Conséquence : pas de mémoire sémantique, pas de RAG, pas de « ce libellé ressemble à celui que vous aviez imputé en 6132 ».
2. **Aucune boucle d'apprentissage.** Les corrections humaines sur les propositions IA (import de facture, rapprochement, anomalies ignorées) **ne sont pas capitalisées**. Chaque proposition repart de zéro. C'est la fuite de valeur la plus coûteuse du produit.
3. **Aucune métrologie IA.** Recherche `TokenUsage|AiQuota|PromptTokens` : aucun résultat. Impossible aujourd'hui de répondre à « combien coûte l'IA par dossier ? », « quel est le taux d'acceptation des propositions ? », « le nouveau modèle a-t-il régressé ? ».

**Ces trois manques constituent la Vague 0 (§4).** Toute fonctionnalité ajoutée avant eux sera une fonctionnalité copiable.

---

## 3. Le paysage concurrentiel de l'IA — où gagner

| Capacité IA | FactuTrust (aujourd'hui) | Odoo | Sage | Dynamics BC | Verticaux (Pennylane/Dext-like) | Cible FactuTrust |
|---|---|---|---|---|---|---|
| Chat sur les données de gestion | ● | ◐ | ○ | ● (Copilot) | ◐ | Maintenir |
| Extraction de pièces (OCR/vision) | ● | ◐ | ◐ | ◐ | ● | **Dépasser** (§5.11) |
| **Imputation comptable apprenante** | ○ | ○ | ◐ | ○ | ● | **★ Conquérir** (§5.1) |
| **Rapprochement/lettrage prédictif** | ◐ (règles) | ○ | ◐ | ◐ | ● | **★ Conquérir** (§5.2) |
| **Copilote de clôture** | ◐ (règles) | ○ | ○ | ○ | ◐ | **★ Conquérir** (§5.3) |
| Détection de fraude / contrôle continu | ◐ | ○ | ○ | ◐ | ○ | **★ Conquérir** (§5.4) |
| Recouvrement intelligent multicanal | ○ | ◐ | ○ | ◐ | ◐ | **★ Conquérir** (§5.5) |
| Trésorerie prévisionnelle assistée | ● | ○ | ◐ (XRT) | ◐ | ◐ | Étendre (§5.6) |
| **Conformité fiscale TN sourcée** | ○ | ○ | ○ | ○ | ○ | **★★ Territoire vierge** (§5.8) |
| **Supervision de portefeuille cabinet** | ◐ | ○ | ○ | ○ | ● (FR) | **★ Conquérir** (§5.13) |
| **Benchmark sectoriel anonymisé** | ○ | ○ | ○ | ◐ | ◐ | **★ Conquérir** (§5.14) |
| ERP conversationnel WhatsApp / vocal | ◐ (lecture) | ○ | ○ | ○ | ○ | **★★ Territoire vierge** (§5.12) |
| Souveraineté (inférence 100 % locale) | ● | ○ | ○ | ○ | ○ | **★ Argument de vente** (§9) |

Légende : ● complet · ◐ partiel · ○ absent · ★ opportunité de différenciation · ★★ aucun acteur positionné sur le marché tunisien

**Trois territoires sont vierges ou quasi vierges** : la conformité fiscale tunisienne sourcée, le canal conversationnel/vocal en contexte tunisien, et la souveraineté d'inférence. Ce sont les trois axes du message produit recommandé en §10.

---

## 4. Vague 0 — le socle (≈ 105 j/h)

> Sans ces quatre briques, les fonctionnalités de la §5 sont des démonstrations. Avec elles, ce sont des systèmes qui s'améliorent seuls.

### 4.1 S1 — Mémoire vectorielle et récupération hybride *(25 j/h)*

**Objet.** Donner à la plateforme la capacité de répondre à « qu'est-ce qui *ressemble* à ça ? » — sur les libellés bancaires, les lignes de facture, les articles réglementaires, les descriptions produit, les questions récurrentes.

**Conception recommandée.**

```
Application/Common/Interfaces/IVectorIndex.cs      ← abstraction (Upsert, Search, Delete)
Application/Common/Interfaces/IEmbeddingGenerator.cs
Infrastructure/Services/AI/Embeddings/
    OllamaEmbeddingGenerator.cs                    ← /api/embeddings
    SqlServerVectorIndex.cs                        ← VARBINARY(MAX) + index mémoire par tenant
    HybridRetriever.cs                             ← fusion BM25 (full-text SQL) + cosinus (RRF)
```

- **Modèle d'embedding** : `nomic-embed-text` (768 dim, tronquable à 256 par Matryoshka) ou `multilingual-e5-small` (384 dim) pour la couverture FR + arabe. Les deux tournent en local sur CPU — pas de dépendance cloud, cohérent avec l'argument souveraineté.
- **Stockage** : table `VectorEntries` par base tenant (`OwnerType`, `OwnerId`, `Kind`, `Vector VARBINARY(MAX)`, `Model`, `Dim`, `ContentHash`, `UpdatedAtUtc`). Le partitionnement est gratuit : **une base par tenant existe déjà**.
- **Recherche** : chargement de l'index tenant en mémoire (cache invalidé à l'écriture) + cosinus vectorisé via `System.Numerics.Tensors.TensorPrimitives`. Dimensionnement réel : 100 000 vecteurs × 256 dim × 4 o ≈ **100 Mo**, recherche < 15 ms. Aucune dépendance externe (pas de FAISS, pas de pgvector, pas de service tiers).
- **Évolutivité** : l'interface `IVectorIndex` permet de basculer plus tard sur le type `VECTOR` natif de SQL Server 2025 sans toucher aux appelants.

**Garde-fou :** la génération d'embeddings passe par le même `OllamaGenerationGate` que le chat pour ne pas saturer le GPU pendant les heures ouvrées ; l'indexation de masse est un job Hangfire nocturne.

### 4.2 S2 — Boucle d'apprentissage : le journal de décision *(20 j/h)*

**Objet.** Capitaliser chaque interaction « l'IA propose / l'humain tranche ». C'est **la brique la plus rentable du programme** : elle coûte 20 j/h et conditionne le moat n°1.

**Table unique, tenant DB :**

| Colonne | Rôle |
|---|---|
| `Kind` | `InvoiceExtraction`, `AccountImputation`, `BankMatching`, `Lettering`, `AnomalyTriage`, `DunningMessage`, `PriceSuggestion`… |
| `SubjectType` / `SubjectId` | Objet métier concerné |
| `ProposedJson` / `FinalJson` | Proposition IA vs décision retenue (le **diff** est le signal d'apprentissage) |
| `Confidence` | Score calibré [0,1] |
| `Outcome` | `Accepted` \| `Edited` \| `Rejected` \| `AutoApplied` \| `AutoAppliedThenReverted` |
| `ModelRef`, `PromptVersion`, `RetrievalIds` | Traçabilité complète de la génération |
| `TokensIn`/`TokensOut`, `LatencyMs`, `CostMillimes` | FinOps (§4.4) |
| `DecidedByUserId`, `DecidedAtUtc`, `TraceId` | Piste d'audit |

**Quatre usages d'une seule table :**
1. **Few-shot dynamique** : les 5 corrections les plus proches (via S1) sont injectées dans le prompt → l'IA reproduit *les habitudes de ce dossier*, pas une moyenne mondiale.
2. **Seuils d'auto-validation calibrés** : `Confidence` × taux d'acceptation observé → seuil au-delà duquel l'application devient automatique, **par tenant et par type**.
3. **Évaluation continue** : le journal *est* le jeu de test. Changer de modèle devient mesurable au lieu d'être un pari.
4. **Argument commercial** : « votre comptabilité s'auto-impute à 87 % après 4 mois » est une phrase de vente qu'aucun concurrent ne peut prononcer sur ce marché.

### 4.3 S3 — Orchestration, garde-fous et doctrine d'autonomie *(30 j/h)*

**L'escalier d'autonomie** — chaque fonctionnalité IA déclare son niveau, réglable par tenant :

| Niveau | Comportement | Exemple |
|---|---|---|
| **N0 — Observe** | Mesure et journalise, n'affiche rien | Phase de calibration d'un nouveau modèle |
| **N1 — Suggère** | Propose, l'humain saisit | État actuel de l'import de facture |
| **N2 — Pré-remplit** | Le formulaire arrive rempli, l'humain valide en 1 clic | Cible de l'imputation (§5.1) |
| **N3 — Applique sous seuil** | Auto-appliqué si confiance ≥ seuil ET montant ≤ plafond ; **réversible en 1 clic** ; notifié | Lettrage, rapprochement (§5.2) |
| **N4 — Agit en autonomie bornée** | Exécute et rend compte a posteriori dans un domaine fermé | Relance de recouvrement niveau 1 (§5.5) |
| **N5 — Autonomie totale** | **Interdit par conception** | — |

**Règles non négociables (à inscrire dans l'architecture, pas dans la documentation) :**

1. **L'IA n'écrit jamais dans le comptable validé.** Elle produit des *brouillons* et des *propositions*. La correction d'un validé passe par l'extourne, comme aujourd'hui (cf. `erp-gap-analysis.md` §4.1).
2. **Toute action N3/N4 est réversible** et trace son `AiDecisionLog` avec l'identité du modèle.
3. **Aucune action IA ne sort de l'argent** : pas de virement, pas d'ordre de paiement, pas de dépôt de déclaration. L'IA prépare, l'humain signe.
4. **Abstention par défaut** : en dessous du seuil de confiance, la réponse correcte est « je ne sais pas, voici les éléments ». Le taux d'abstention pertinente est un **KVI mesuré**, pas un aveu de faiblesse.
5. **Séparation stricte déterministe / génératif** (§4.4).

**Composants :** `AiAutomationPolicy` (par tenant × Kind : niveau, seuil, plafond montant, comptes autorisés), `AiActionGuard` (validation avant application), `AiReversal` (annulation d'une action N3/N4).

### 4.4 S4 — Évaluation, FinOps et frontière déterministe *(30 j/h)*

**La frontière déterministe.** La règle appliquée à la trésorerie prévisionnelle doit devenir une **règle d'architecture générale** :

> **Le chiffre est calculé par du code. Le mot est écrit par le modèle.**

Un LLM ne produit jamais : un montant, une date d'échéance, un numéro de compte final, un taux de TVA, un solde. Il produit : un classement, une catégorie parmi un ensemble fermé, un score dans une borne, une explication, une rédaction. Cette frontière est ce qui rend l'IA comptable acceptable en audit — et elle est déjà implémentée dans `Features/Treasury/CashFlowAiAdjustmentApplier.cs`, qui borne l'ajustement IA à ±15 points. **Généraliser ce patron.**

**Harnais d'évaluation.** Le modèle `docs/ai-screen-analysis/golden/*.json` est étendu à chaque fonctionnalité : jeu doré versionné, exécution en CI (`dotnet test` sur modèle local léger), métriques par fonctionnalité (exactitude, taux d'abstention, taux d'hallucination sur faits vérifiables, latence p95). **Aucun changement de modèle par défaut sans passage du harnais.**

**FinOps IA.** Métrage par tenant/fonctionnalité alimenté par `AiDecisionLog` : coût en millimes, quotas, alerte de dérive. **Routage par sensibilité et complexité** : local (Ollama) pour le volume, la donnée sensible et les tenants en mode souverain ; cloud (OpenRouter) pour le raisonnement long et la vision haut de gamme. Cache sémantique (via S1) sur les questions récurrentes — sur un cabinet, 30 à 40 % des questions sont des variantes de 20 questions.

---

## 5. Portefeuille de fonctionnalités

Format de chaque fiche : **problème → fonctionnement → ancrage codebase → garde-fous → KPI → effort**.

---

### 5.1 ★ F1 — Moteur d'imputation apprenant (« le cerveau du grand livre »)

**Problème.** Un collaborateur de cabinet passe 40 à 60 % de son temps à décider *quel compte* pour *quelle ligne*. Les heuristiques actuelles (`BankOperationClassifier` : 25 mots-clés en dur, `AccountingEntryProposalService` : rapprochement par nom au seuil 0,9) ne s'améliorent jamais et ne connaissent pas les habitudes du dossier.

**Fonctionnement.**

1. **Candidats déterministes** — historique exact du tiers, mapping compte/tiers existant (`AccountMappingTable.cs`), règles du dossier.
2. **Candidats sémantiques** — k-NN sur les embeddings de libellés (S1) dans l'historique **validé** du tenant, pondéré par récence et par montant comparable.
3. **Arbitrage** — un LLM léger (qwen2.5:3b local suffit) choisit **parmi les candidats fournis**, avec obligation de citer la pièce historique de référence. Il ne peut pas inventer un compte hors du plan comptable du dossier.
4. **Calibration** — la confiance est calibrée sur l'historique réel d'acceptation (S2), pas déclarée par le modèle.
5. **Application** — N2 par défaut, N3 (auto) au-delà du seuil appris, par famille de comptes et sous plafond de montant.

**Ancrage codebase.** `Services/DocumentImport/AccountingEntryProposalService.cs` (remplacement du bloc de matching), `AccountMappingTable.cs`, `BankOperationClassifier.cs` (conservé comme candidat déterministe), `Features/Accounting/`.

**Garde-fous.** Comptes proposés restreints au plan du dossier et à la période ouverte ; jamais d'écriture validée directement ; le taux d'auto-application démarre à 0 % et monte quand les données le justifient.

**KPI.** Taux d'imputation acceptée sans modification (cible : **> 85 % à 90 jours**, > 92 % à 12 mois) · pièces traitées/heure/collaborateur (cible : **×2,5**) · taux d'auto-application N3 sans annulation (> 99,5 %).

**Effort : 55 j/h.** Dépend de S1, S2, S3.

---

### 5.2 ★ F2 — Rapprochement bancaire et lettrage prédictifs

**Problème.** Le rapprochement est la deuxième tâche la plus chronophage, et les paiements partiels, groupés ou avec escompte cassent tous les matchings par montant exact.

**Fonctionnement.** Génération de candidats par contraintes (montant ± tolérance, fenêtre de date, tiers, référence), puis **scoring composite** : distance de montant, écart de date vs délai de paiement habituel *du client*, similarité sémantique du libellé (S1), historique de comportement (« ce client paie toujours 3 factures groupées le 5 »). Les cas **1↔N et N↔1** sont résolus par recherche de sous-ensemble sous contrainte de somme (algorithme déterministe), le LLM ne servant qu'à *expliquer* le rapprochement retenu et à qualifier les écarts résiduels (escompte, frais bancaires, différence de change).

**Ancrage.** `Services/BankReconciliationService.cs`, `BankAccountMatcher.cs`, feature `Accounting/Lettrage` — respecter l'invariant de groupes de lettrage mono-compte déjà en vigueur, et le principe « le lettrage best-effort n'annule jamais un règlement ».

**KPI.** Taux de rapprochement automatique (cible : **> 75 %** des lignes de relevé, > 90 % sur les tenants matures) · temps moyen de rapprochement mensuel (cible : **−70 %**).

**Effort : 40 j/h.**

---

### 5.3 ★ F3 — Copilote de clôture

**Problème.** La clôture mensuelle/annuelle est un parcours d'obstacles connu de tous et documenté nulle part : ce qui manque, ce qui est anormal, ce qu'il reste à justifier. Les règles existent déjà (`PreClosingAuditRules`), mais elles produisent une **liste** — pas un **plan de travail**.

**Fonctionnement.**

- **Phase déterministe** : exécution du moteur d'audit existant + contrôles ajoutés (cut-off achats/ventes, charges constatées d'avance, TVA sur encaissement vs facturation, cohérence stock/comptable, provisions).
- **Phase de priorisation** : ordonnancement des anomalies par **impact chiffré** (impact sur le résultat, sur la TVA due, risque fiscal) — calculé, pas estimé par le LLM.
- **Phase générative** : rédaction du **dossier de révision** — pour chaque poste significatif, la justification, les pièces rattachées, les points d'attention, et les questions à poser au client. Sortie exportable (PDF via l'infrastructure QuestPDF/PDFsharp existante).
- **Phase de correction assistée** : pour chaque anomalie, une écriture corrective **proposée** (brouillon), jamais appliquée seule.

**Ancrage.** `Services/AccountingAudit/AccountingAuditEngine.cs` + `Rules/`, `AccountingHealthService.cs`, `AccountingPeriodService.cs`, `AccountingAuditScheduledJob`.

**KPI.** Durée de clôture mensuelle (cible : **−40 %**) · anomalies détectées avant clôture vs après (cible : > 90 % avant) · taux d'adoption du dossier de révision généré par les collaborateurs.

**Effort : 45 j/h.**

---

### 5.4 ★ F4 — Contrôle continu et détection de fraude

**Problème.** Les contrôles actuels sont des règles fixes. Ce qui échappe aux règles fixes : les schémas *inhabituels pour ce dossier précis*.

**Fonctionnement.** Couche statistique **par tenant** au-dessus du moteur de règles :

| Signal | Méthode |
|---|---|
| Facture fournisseur en doublon (montant proche, date proche, libellé variant) | Similarité sémantique (S1) + fenêtre temporelle |
| Changement de RIB fournisseur suivi d'un paiement | Détection d'événement + corrélation temporelle |
| Montant juste sous un seuil d'approbation (structuration) | Histogramme des montants, détection de pic sous seuil |
| Prix d'achat hors norme sur un article | Écart interquartile sur l'historique du couple article/fournisseur |
| Écriture hors horaires / week-end / rétroactive | Analyse temporelle sur la piste d'audit existante |
| Fournisseur créé et payé dans la même journée | Règle de séquence |
| Séquence de numérotation anormale | Existe (`sequence-gaps`) — enrichi du contexte |

Le LLM **n'a qu'un rôle** : rédiger l'explication compréhensible de l'anomalie et proposer l'action. La détection est statistique et reproductible — condition pour tenir devant un auditeur.

**Boucle d'apprentissage :** chaque anomalie marquée `Ignored` alimente `AiDecisionLog` → réduction progressive du bruit, spécifique au dossier. C'est ce qui différencie un outil utilisable d'un outil qu'on désactive au bout de trois semaines.

**Ancrage.** `AccountingAuditRuleRegistry.cs` (ajout de règles statistiques), `AccountingAnomaly` / `AccountingAnomalyActivity`, `AuditService.cs`, chaîne d'audit (`FactuTrust.AuditChainRepair`).

**KPI.** Précision des alertes (cible : **> 60 % d'alertes jugées pertinentes**, seuil au-delà duquel un outil de contrôle est réellement utilisé) · montant d'incidents détectés · taux de faux positifs en baisse mois après mois.

**Effort : 40 j/h.**

---

### 5.5 ★ F5 — Recouvrement intelligent multicanal

**Problème.** En Tunisie, le délai de paiement effectif est le premier facteur de mortalité des PME. Les balances âgées existent déjà (`GetClientAgingReportQuery`) mais **savoir qui est en retard ne fait pas rentrer l'argent**.

**Fonctionnement.**

1. **Score de risque de retard par client** — modèle déterministe sur l'historique interne : DPO réel vs contractuel, variance de paiement, incidents d'effets, saisonnalité, encours vs plafond, ancienneté. Sortie : probabilité de retard > 30 j et **montant à risque**.
2. **Priorisation** — file de recouvrement classée par `montant × probabilité de récupération` et non par ancienneté brute (une créance de 200 TND à 120 jours ne vaut pas un appel).
3. **Rédaction adaptative** — le LLM rédige la relance selon le niveau (rappel courtois → mise en demeure), l'historique relationnel, la langue du contact (FR/AR), et le canal. **Toujours soumise à validation** au premier déploiement (N1/N2), puis N4 possible sur le seul niveau 1.
4. **Canal** — email existant + **WhatsApp** via le pont déjà en place (le canal dominant en Tunisie ; un rappel WhatsApp est lu, un email non).
5. **Boucle** — quel message, quel canal, quel horaire ont effectivement produit un paiement → apprentissage par cohorte.

**Ancrage.** `ClientOutstandingService.cs`, `GetClientAgingReportQuery`, `Services/Email/`, `Features/Channels/`, `DunningExecutorJob` (le mécanisme de relance existe déjà côté facturation SaaS — le patron est à transposer côté tenant).

**Garde-fous.** Aucun message envoyé sans consentement du canal ; plafond de fréquence par client ; interdiction absolue de menace ou de mention juridique non validée par un modèle de texte approuvé ; journalisation intégrale.

**KPI.** **DSO (cible : −8 à −15 jours)** · taux de recouvrement à 60 j · encours > 90 j · taux de réponse par canal.

**Effort : 45 j/h.**

---

### 5.6 F6 — Arbitrage de trésorerie sous contrainte

**Problème.** La prévision existe (`docs/architecture/treasury-cash-forecast.md`) et répond à « vais-je manquer de cash ? ». Elle ne répond pas à « **que dois-je faire ?** ».

**Fonctionnement.** Deux ajouts au module existant :

- **Ordonnanceur de décaissements** : sous contrainte de solde minimum, d'échéances légales incompressibles (TVA, CNSS, paie) et de pénalités de retard, un solveur déterministe (glouton + amélioration locale) propose **quoi payer, quand, et ce qu'il faut décaler**. Sortie : un plan daté, chiffré, exportable.
- **Simulation en langage naturel** : « et si je décale la paie de 5 jours et que j'obtiens 100 000 TND d'escompte sur les effets de septembre ? » → le LLM **traduit la question en paramètres** du moteur déterministe, le moteur recalcule, le LLM restitue l'écart. Le modèle ne calcule rien : il traduit et raconte.

**Ancrage.** `Features/Treasury/CashFlowForecastCommands.cs`, `CashFlowAiAdjustmentApplier.cs`, `CashFlowRecomputationJob`. Le flag `TreasuryForecast:Ai` et ses bornes existent déjà.

**KPI.** Nombre de tensions de trésorerie anticipées > 30 j à l'avance · adoption du plan proposé · pénalités de retard évitées.

**Effort : 35 j/h.**

---

### 5.7 F7 — Copilote de marge et de prix

**Problème.** La fuite de marge est invisible ligne à ligne et évidente en agrégat : remises accordées sous le seuil de rentabilité, prix jamais réindexés, clients structurellement déficitaires une fois le coût de service intégré.

**Fonctionnement.** Détection déterministe (marge par ligne/client/produit après remise, dérive vs référence, élasticité estimée sur l'historique de volume/prix), puis recommandation encadrée : fourchette de prix conseillée par couple client/produit sous contrainte de marge minimale et de politique commerciale. Le LLM rédige l'argumentaire de négociation et la note interne ; **il ne fixe pas le prix**, il propose dans un intervalle calculé.

**Ancrage.** `Features/Pricing/`, `get_commercial_profit`, `simulate_promotion_impact`, `get_promotion_recommendations` (outils déjà exposés).

**KPI.** Marge brute (cible : **+1 à +3 points**) · % de lignes vendues sous marge minimale (en baisse) · adoption des prix recommandés.

**Effort : 35 j/h.**

---

### 5.8 ★★ F8 — Copilote fiscal tunisien sourcé (RAG réglementaire)

**Problème.** C'est le **territoire vierge le plus précieux**. Un dirigeant ou un collaborateur qui se demande « quel taux de retenue sur cette prestation d'un non-résident ? », « cette charge est-elle déductible ? », « quel régime pour cette opération ? » n'a aujourd'hui aucune réponse fiable dans son ERP. Odoo, Sage et Dynamics n'auront jamais cette localisation.

**Fonctionnement.**

1. **Corpus curé et versionné** : Code de l'IRPP/IS, Code de la TVA, lois de finances, notes communes DGI, conventions de non-double-imposition, textes CNSS. Chaque document : source, date d'effet, version, périmètre. **Sourcing et droits d'usage à valider juridiquement en amont — point de vigilance n°1.**
2. **Indexation hybride** (S1) : découpage par article, embeddings + recherche lexicale, fusion RRF.
3. **Réponse sourcée obligatoire** : toute affirmation est accompagnée de sa référence (texte, article, date d'effet). **Sans source récupérée au-dessus du seuil, la réponse est « je ne trouve pas de fondement — voici les textes voisins ».** L'abstention est un comportement produit, pas un échec.
4. **Contrôles croisés sur le dossier réel** — c'est ici que FactuTrust devient inimitable : confronter la règle aux données du tenant. Taux de TVA appliqués vs nature du produit · RS appliquée vs nature de prestation et résidence du fournisseur · seuils de régime · cohérence assiette CNSS vs bulletins · cohérence RS salaires vs déclaration.
5. **Périmètre encadré** : le copilote **informe et vérifie**, il ne conseille pas. Mention systématique : la validation par un professionnel inscrit reste requise.

**Ancrage.** `Features/Accounting/OfficialForm/`, `WithholdingTax`, `Features/Payroll/`, `FiscalObligationType.cs`, `AiToolRegistry` (nouveaux outils `fiscal_lookup`, `fiscal_crosscheck`).

**Garde-fous.** Versionnement du corpus avec date d'effet (une réponse ne doit jamais citer un texte abrogé) · relecture par un expert-comptable **avant activation** — cadence recommandée : revue trimestrielle · jeu doré de 200 questions fiscales validées par un professionnel, rejoué à chaque changement de modèle ou de corpus.

**KPI.** Exactitude sur le jeu doré (cible : **> 90 %**, taux d'affirmation non sourcée = **0 %**) · taux d'abstention pertinente · redressements évités (mesuré par témoignage client) · **taux de conversion des cabinets** — c'est l'argument de vente n°1 sur ce segment.

**Effort : 60 j/h** (dont ~15 j/h de constitution et de validation du corpus, hors temps expert-comptable).

---

### 5.9 F9 — Pré-contrôle déclaratif

**Problème.** L'écart entre le déclaré et le comptabilisé se découvre au contrôle fiscal, des mois plus tard.

**Fonctionnement.** Avant dépôt, confrontation systématique : TVA collectée déclarée vs comptabilisée (par taux), TVA déductible vs pièces justificatives présentes (règle `vat-deductible-no-proof` existante), RS déclarée vs écritures 432x, assiette CNSS vs bulletins, cohérence inter-périodes (variation anormale vs historique et vs saisonnalité du secteur). Sortie : rapport d'écarts **chiffrés et sourcés à la ligne comptable**, avec explication rédigée et action proposée.

Ce module doit **respecter strictement** le principe déjà acté : le dépôt fait foi, le recalcul reste une suggestion, l'écart est signalé **sans réalignement automatique**.

**Ancrage.** `Features/Accounting/OfficialForm/MonthlyDeclarationFormBinder.cs`, `GetVatDeclarationQuery.cs`, `DeclarationScheduleSynchronizer.cs`, `FiscalReminderJob`.

**KPI.** Écarts détectés avant dépôt · déclarations rectificatives évitées · temps de préparation déclarative (**−50 %**).

**Effort : 30 j/h.**

---

### 5.10 F10 — Veille réglementaire à impact (cabinets)

**Problème.** À chaque loi de finances, un cabinet passe des semaines à déterminer **quels dossiers** sont concernés et **comment**.

**Fonctionnement.** Ingestion d'un nouveau texte → extraction structurée des changements (taux, seuils, échéances, régimes) → **requêtage du portefeuille** pour identifier les dossiers impactés (secteur, régime, taille, opérations réalisées) → génération d'une note d'impact par dossier + d'un plan d'action daté. Le LLM extrait et rédige ; le ciblage des dossiers est une requête SQL déterministe.

**Ancrage.** `Features/FirmGovernance/`, `FirmDashboardService.cs`, `FirmFiscalScheduleService.cs`, corpus F8.

**KPI.** Délai entre publication d'un texte et notification des dossiers impactés (cible : **< 72 h**) · taux de couverture du portefeuille.

**Effort : 25 j/h.** Dépend de F8.

---

### 5.11 F11 — Zéro saisie omnicanale

**Problème.** La pièce justificative arrive par WhatsApp, par email, en photo, en PDF, en papier. Chaque canal non couvert est une ressaisie.

**Fonctionnement.** Généralisation du pipeline d'import existant à **tous les canaux et tous les types** :

- **Entrées** : WhatsApp (le client photographie sa facture → traitée), boîte mail dédiée par dossier (`dossier-xxx@…`, ingestion IMAP), dépôt web, scanner.
- **Types** : facture achat/vente (existe), reçu de caisse, relevé bancaire (existe), bon de livraison, contrat, note de frais, bulletin fournisseur.
- **Traitement** : OCR/vision existants → extraction structurée → **imputation via F1** → détection de doublon via F4 → mise en file de validation.
- **Apprentissage** : chaque correction alimente S2 → le fournisseur récurrent devient parfaitement extrait au bout de 3 pièces.

**Ancrage.** `ImportInvoiceFromFileCommand.cs`, `AccountingDocumentExtractor.cs`, `Services/Channels/`, `Services/Email/`.

**Garde-fous.** Aucune pièce reçue par canal externe n'est comptabilisée sans validation humaine dans les premiers mois (N2) ; contrôle anti-doublon systématique ; traçabilité de l'origine sur la pièce.

**KPI.** % de pièces sans saisie manuelle (cible : **> 80 %**) · délai pièce reçue → pièce comptabilisée (cible : < 24 h) · taux de champs corrigés à la main (en baisse continue).

**Effort : 50 j/h.**

---

### 5.12 ★★ F12 — ERP conversationnel et vocal (FR / arabe tunisien)

**Problème.** Le commerçant, le livreur, le gérant d'atelier ne se connecteront jamais à un ERP. Ils utilisent WhatsApp et parlent — souvent en darija, souvent en alternance français/arabe. **Aucun ERP ne les adresse.**

**Fonctionnement.**

1. **Entrée vocale** : message vocal WhatsApp → transcription locale (`faster-whisper` small/medium via un processus pont — **le patron existe déjà** avec le pont WhatsApp et le pont Cursor SDK).
2. **Compréhension d'intention** : le LLM traduit l'énoncé en **appel d'outil du registre existant** (`create_client`, `generate_invoice`, `record_invoice_payment`, `get_stock_snapshot`…). Les 93 outils sont déjà là — c'est un nouveau canal d'entrée, pas un nouveau moteur.
3. **Confirmation obligatoire** avant toute écriture : récapitulatif structuré, réponse « OUI » attendue. Non négociable en vocal, où le taux d'erreur de transcription est structurellement plus élevé.
4. **Réponse** : texte formaté WhatsApp (`WhatsAppTextFormatter.cs` existe) et, optionnellement, vocal.

**Réalisme — à dire clairement.** Whisper est **médiocre sur l'arabe tunisien pur**. Les mitigations sont connues et suffisantes : la population cible parle en réalité un français/arabe alterné truffé de termes métier français (« facture », « bon de livraison », « avance ») ; le vocabulaire est restreint (noms de clients, de produits, montants — tous connus et injectables en *biasing* de décodage) ; la confirmation obligatoire absorbe les erreurs résiduelles. **Recommandation : démarrer en français vocal + texte bilingue, mesurer le WER réel sur 500 messages, puis décider d'un éventuel fine-tuning** — ne pas promettre la darija tant que la mesure n'est pas faite.

**Ancrage.** `ChannelInboundOrchestrator.cs`, `ChannelCommandParser.cs`, `AiToolRegistry.cs`, `AiToolIntentRouter.cs`.

**KPI.** Actions réalisées par canal conversationnel/mois · taux de confirmation au premier essai · WER mesuré · **acquisition de segments non-utilisateurs d'ERP** (indicateur stratégique).

**Effort : 40 j/h** (+ 15 j/h si extension vocale sortante).

---

### 5.13 ★ F13 — Superviseur de portefeuille cabinet

**Problème.** Un chef de mission qui suit 60 dossiers ne sait pas, le lundi matin, **lequel est en train de dérailler**.

**Fonctionnement.** Job nocturne (Hangfire — `FirmMissionBriefingJob` existe déjà) qui parcourt le portefeuille et calcule un **indice de risque par dossier** : retard de saisie, écarts déclaratifs (F9), anomalies ouvertes (F4), tension de trésorerie (F6), échéances fiscales proches, rentabilité de la mission vs temps passé (les feuilles de temps et la rentabilité collaborateur existent). Puis : file de travail priorisée **par collaborateur**, tenant compte de sa charge réelle (`get_firm_collaborator_workload`), et brief client rédigé prêt à envoyer.

**Ancrage.** `FirmAgentToolExecutor.cs`, `FirmDashboardService.cs`, `FirmFiscalOpsAggregator.cs`, `FirmCollaboratorRentabilityService.cs`, `FirmMissionBriefingJob`.

**KPI.** Dossiers en dérive détectés avant échéance · charge équilibrée entre collaborateurs (écart-type en baisse) · **rentabilité de mission (+ points de marge)** · échéances manquées (→ 0).

**Effort : 35 j/h.**

---

### 5.14 ★ F14 — Benchmark sectoriel anonymisé

**Problème.** « Ma marge de 22 %, c'est bien ou pas ? » — question à laquelle aucun ERP tunisien ne répond, et à laquelle **seul un SaaS multi-tenant peut répondre**.

**Fonctionnement.** Agrégation nocturne d'indicateurs normalisés (marge brute, DSO, DPO, rotation de stock, poids des charges de personnel, taux de TVA effectif, saisonnalité) par cohorte **secteur × tranche de CA × région**. Restitution : positionnement en décile + explication générée des écarts + leviers d'amélioration priorisés.

**Gouvernance — condition de licéité et de confiance :**

- **Opt-in explicite** par tenant, révocable, avec retrait rétroactif des contributions.
- **k-anonymat strict** : aucune cohorte publiée en dessous de **20 dossiers contributeurs**, et aucun contributeur ne pesant plus de 15 % de l'agrégat.
- **Agrégats uniquement** : aucune donnée individuelle ne quitte jamais la base tenant ; le calcul produit des statistiques, pas des extraits.
- Conformité à la loi tunisienne sur la protection des données personnelles (INPDP) et au RGPD pour les groupes exportateurs — **avis juridique requis avant activation**.

**Ancrage.** Nouveau service d'agrégation côté Master DB, alimenté par job Hangfire ; réutilise les requêtes de reporting existantes.

**KPI.** Taux d'opt-in (cible : **> 40 %** — indicateur de confiance dans la marque) · usage de l'écran benchmark · **taux de citation en argument de vente**.

**Effort : 45 j/h.**

---

### 5.15 F15 — Studio : automatisations en langage naturel

**Problème.** Chaque client veut sa règle métier. Chaque règle métier est un ticket de développement.

**Fonctionnement.** « Si un client dépasse 30 jours de retard et 5 000 TND d'encours, bloquer ses bons de livraison et prévenir son commercial » → génération d'une automatisation Studio (déclencheur, conditions, actions) **présentée en clair pour validation**, testée en **dry-run sur 90 jours d'historique** (« cette règle se serait déclenchée 14 fois — voici lesquelles ») avant activation.

**Ancrage.** `Features/Studio/Automations/`, `Features/Studio/Ai/`, outils `studio_plan_*` déjà exposés.

**KPI.** Automatisations créées par les clients eux-mêmes · réduction des demandes de développement spécifique.

**Effort : 30 j/h.**

---

## 6. Ce qu'il ne faut PAS faire

Un plan crédible se juge autant à ce qu'il exclut. Les cinq pièges suivants sont ceux qui font échouer les programmes IA en comptabilité :

| Fausse bonne idée | Pourquoi c'est un piège |
|---|---|
| **« Comptabilité 100 % automatique »** | Ni le droit ni la responsabilité de l'expert-comptable ne le permettent. La promesse crédible est : **« 85 % des écritures pré-remplies, 100 % validées »**. Vendre l'autonomie totale, c'est perdre le segment cabinet dès la première réunion. |
| **Fine-tuner un LLM fiscal tunisien** | Coût élevé, données rares, obsolescence à chaque loi de finances, aucune traçabilité de la source. **Le RAG versionné est supérieur sur tous les axes** : moins cher, à jour, sourcé, auditable. |
| **Laisser le LLM produire des chiffres** | Source n°1 des incidents de confiance. Un montant faux détruit plus de crédibilité que dix résumés utiles n'en construisent. La frontière du §4.4 est non négociable. |
| **Agents autonomes qui envoient de l'argent ou déposent des déclarations** | Risque juridique et réputationnel disproportionné par rapport au gain. L'IA prépare, l'humain signe — définitivement. |
| **Empiler des fonctionnalités IA sans métrologie** | Sans S2/S4, impossible de savoir si le produit s'améliore. On accumule alors de la dette IA : des fonctions qu'on n'ose ni améliorer ni retirer. |

---

## 7. Feuille de route

| Vague | Contenu | Effort | Durée | Jalon de valeur |
|---|---|---|---|---|
| **V0 — Socle** | S1 mémoire vectorielle · S2 journal de décision · S3 garde-fous & doctrine d'autonomie · S4 evals + FinOps | **105 j/h** | 2–3 mois | Aucune démo — mais tout le reste devient possible et mesurable |
| **V1 — La comptabilité qui s'auto-alimente** | F1 imputation apprenante · F2 rapprochement prédictif · F11 zéro saisie · F9 pré-contrôle déclaratif | **175 j/h** | 3–4 mois | **« 3 heures par dossier et par mois »** — l'argument qui vend au cabinet |
| **V2 — Le pilotage** | F3 copilote de clôture · F4 contrôle continu · F5 recouvrement · F6 arbitrage trésorerie · F13 superviseur cabinet | **200 j/h** | 4–5 mois | **« −12 jours de DSO, −40 % de temps de clôture »** |
| **V3 — Le territoire réservé** | F8 copilote fiscal sourcé · F10 veille à impact · F14 benchmark · F7 marge & prix | **165 j/h** | 3–4 mois | **« Le seul ERP qui connaît le droit fiscal tunisien »** |
| **V4 — L'élargissement** | F12 conversationnel/vocal · F15 automatisations NL | **70 j/h** | 2 mois | **Nouveau segment** : les entreprises qui n'utilisent pas d'ERP |
| **Total** | | **~715 j/h** | 14–18 mois | |

**Séquencement — la logique.** V1 avant V2 parce que la qualité de la donnée conditionne la qualité du pilotage. V3 après V2 parce que le copilote fiscal exige un corpus validé et un harnais d'évaluation mature — c'est la fonctionnalité où une erreur coûte le plus cher en crédibilité. V4 en dernier parce que c'est de l'acquisition, et qu'on n'acquiert pas avant d'avoir prouvé la rétention.

**Parallélisation possible :** F14 (benchmark) et F12 (vocal) sont indépendants du reste et peuvent être avancés si une opportunité commerciale le justifie.

---

## 8. Mesure du succès

**Trois indicateurs de tête** — si un seul tableau de bord devait exister :

1. **Taux d'automatisation accepté** = propositions acceptées sans modification / propositions totales. Cible : **> 85 % à 90 jours** par tenant. C'est la mesure directe du moat.
2. **Heures économisées par dossier et par mois**, mesurées sur les feuilles de temps déjà instrumentées. Cible : **> 3 h**. C'est la mesure qui se transforme en prix.
3. **Coût IA par dossier et par mois** (millimes). Cible : **< 5 % du prix de l'abonnement**. C'est la mesure qui rend le modèle économique viable.

**Indicateurs de qualité** (harnais S4) : exactitude sur jeux dorés par fonctionnalité · taux d'affirmation non sourcée (cible **0 %** sur F8) · taux d'abstention pertinente · taux d'annulation d'action N3/N4 (cible **< 0,5 %**) · latence p95 par fonctionnalité.

**Indicateurs business** : DSO moyen du parc · taux de rétention des cabinets · nombre de dossiers par collaborateur (capacité) · taux d'opt-in benchmark (proxy de confiance) · part du CA attribuable aux modules IA.

---

## 9. Gouvernance, confidentialité et souveraineté

**L'argument souveraineté est un actif commercial sous-exploité.** FactuTrust peut faire tourner l'intégralité de son IA **en local via Ollama** — aucun octet de comptabilité client ne sort du pays. Pour un cabinet qui traite des dossiers sensibles, c'est un argument décisif qu'aucun concurrent cloud ne peut offrir.

**Trois modes d'exécution à formaliser et à vendre :**

| Mode | Inférence | Cible |
|---|---|---|
| **Souverain** | 100 % local (Ollama), aucune sortie réseau | Cabinets, dossiers sensibles, secteur public |
| **Hybride** *(défaut)* | Local pour le volume et la donnée sensible ; cloud pour le raisonnement long et la vision | PME standard |
| **Performance** | Cloud prioritaire (OpenRouter) | Tenants exigeant la latence/qualité maximale |

**Règles de gouvernance :**

- **Classification des données** : ce qui ne sort jamais du périmètre local — identités des salariés, salaires nominatifs, RIB, données de santé. À encoder dans le routeur de modèle, pas dans une politique écrite.
- **Journal d'accès IA** : quelle fonctionnalité a lu quelles données, pour quel utilisateur — exploitable en audit, adossé à la chaîne d'audit existante.
- **Réversibilité** : toute action N3/N4 annulable ; export complet des propositions et décisions.
- **Transparence utilisateur** : indication systématique de ce qui est généré par IA, avec le modèle et la confiance. Les comptables acceptent l'IA qui s'annonce, pas l'IA qui se cache.
- **Conformité** : loi tunisienne sur les données personnelles (INPDP) pour F14 et F5 ; mentions d'information dans les CGU ; DPA fournisseur pour le mode cloud.

---

## 10. Message produit et packaging

**Positionnement recommandé — trois phrases, trois preuves :**

> **« FactuTrust comprend votre comptabilité, connaît le droit fiscal tunisien, et ne fait jamais sortir vos données du pays. »**
>
> — *comprend* : 85 % des écritures pré-imputées, apprises de vos habitudes (F1, F2, F11)
> — *connaît* : chaque réponse fiscale sourcée à l'article, vérifiée sur votre dossier réel (F8, F9)
> — *ne fait pas sortir* : inférence 100 % locale disponible (§9)

**Packaging suggéré** (à valider par une étude de disposition à payer) :

| Offre | Contenu | Logique de prix |
|---|---|---|
| **Inclus** | Assistant conversationnel, analyse d'écran, briefing quotidien | Acquis — ne se facture plus en 2026 |
| **Copilote Comptable** | F1, F2, F3, F9, F11 | Par dossier/mois — se justifie par les heures économisées, mesurées (§8) |
| **Copilote Cabinet** | F13, F10, F4 + tableau de bord de portefeuille | Par collaborateur/mois |
| **Copilote Fiscal** | F8 + veille | Option premium — la plus forte valeur perçue, la plus coûteuse à maintenir |
| **Intelligence de marché** | F14 | Gratuit **si opt-in** — le benchmark est un produit d'engagement, pas de revenu |
| **Crédits IA** | Consommation au-delà du forfait | Adossé au FinOps de S4 |

---

## 11. Risques et mitigations

| Risque | Gravité | Mitigation |
|---|---|---|
| **Erreur comptable induite par l'IA** | Critique | Frontière déterministe (§4.4) · escalier d'autonomie (§4.3) · jamais d'écriture validée · réversibilité · harnais d'évaluation en CI |
| **Erreur d'interprétation fiscale (F8)** | Critique | Abstention par défaut · sourçage obligatoire · relecture expert-comptable avant activation et revue trimestrielle · mention de non-conseil |
| **Corpus réglementaire : droits et fraîcheur** | Élevée | Validation juridique du sourcing **avant** développement · versionnement avec date d'effet · alerte d'obsolescence · lien vers la source officielle plutôt que reproduction extensive |
| **Adoption : « je ne fais pas confiance »** | Élevée | Démarrage en N1/N2 · métriques de confiance visibles par l'utilisateur · désactivation par fonctionnalité · l'utilisateur voit toujours *pourquoi* l'IA propose |
| **Coût d'inférence non maîtrisé** | Moyenne | FinOps S4 · routage local par défaut · cache sémantique · quotas par tenant |
| **Capacité GPU sur le parc auto-hébergé** | Moyenne | Modèles quantifiés · gate de concurrence (existe) · traitements lourds en nocturne · dégradation gracieuse CPU (déjà implémentée) |
| **Confidentialité multi-tenant (F14)** | Élevée | Opt-in · k ≥ 20 · agrégats uniquement · audit externe avant lancement |
| **Dépendance à un fournisseur de modèle** | Faible | Abstraction multi-fournisseurs **déjà en place** · jeu doré comme filet de sécurité au changement de modèle |
| **Dilution : trop de fonctionnalités IA** | Moyenne | Une vague à la fois · une fonctionnalité n'est « livrée » que lorsque son KPI est atteint sur 3 tenants pilotes |

---

## 12. Prochaines étapes recommandées

1. **Arbitrer le segment prioritaire** — cabinets (V1+V2+V3, cycle de vente long, valeur unitaire élevée, effet de recommandation fort) ou PME directes (V1+V4, volume, acquisition). **Recommandation : cabinets d'abord** — ce sont eux qui apportent leurs PME ensuite.
2. **Lancer la Vague 0 immédiatement.** S1 et S2 sont sans regret : quel que soit l'arbitrage produit, ils sont nécessaires. 45 j/h qui débloquent tout le reste.
3. **Recruter le référent métier.** Un expert-comptable tunisien impliqué à temps partiel est **une dépendance bloquante** pour F3, F8 et F9 — pas un confort. À sécuriser avant le démarrage de V2.
4. **Sécuriser juridiquement le corpus fiscal** (F8) et le benchmark anonymisé (F14) — deux avis à obtenir en parallèle du développement de V1, pas après.
5. **Constituer un panel pilote** de 3 cabinets et 10 PME, instrumenté dès V0, qui valide chaque fonctionnalité sur ses KPI avant généralisation.
6. **Instrumenter la baseline dès maintenant** : temps de traitement par dossier, DSO, durée de clôture. **Sans mesure d'avant, aucun gain ne sera démontrable** — et c'est la démonstration qui se vend.

---

## 13. Annexe — points d'entrée du codebase

| Sujet | Emplacement |
|---|---|
| Registre d'outils IA | `src/Backend/FactuTrust.Application/Features/AI/Tools/AiToolRegistry.cs` |
| Pipeline de chat + tool-calling | `src/Backend/FactuTrust.Application/Features/AI/Commands/SendChatMessageCommand.cs` |
| Portées et permissions IA | `Features/AI/AiAgentScopeCatalog.cs`, `FirmDelegatedAiScopePolicy.cs` |
| Import de facture (OCR + vision) | `Features/AI/Commands/ImportInvoiceFromFileCommand.cs`, `InvoiceImportVisionPolicy.cs` |
| Proposition d'écriture | `Infrastructure/Services/DocumentImport/AccountingEntryProposalService.cs` |
| Moteur d'audit comptable | `Infrastructure/Services/AccountingAudit/AccountingAuditEngine.cs` + `Rules/` |
| Trésorerie prévisionnelle (patron de référence) | `Features/Treasury/`, `docs/architecture/treasury-cash-forecast.md` |
| Agent cabinet | `Infrastructure/Services/AI/FirmAgentToolExecutor.cs` |
| Canal WhatsApp | `Features/Channels/`, `API/Services/Channels/ChannelInboundOrchestrator.cs` |
| Fournisseurs d'inférence | `Infrastructure/Services/AI/OllamaHttpClient.cs`, `OpenAiChatCompletionsClient.cs`, `CursorSdkBridgeHost.cs` |
| Jobs planifiés | `API/Services/Background/HangfireRecurringJobsRegistrationService.cs` |
| Jeux dorés (patron d'évaluation) | `docs/ai-screen-analysis/golden/` |
| Configuration IA | `src/Backend/FactuTrust.API/appsettings.json` → `ScreenAnalysis`, `Ollama`, `OpenRouter`, `CursorSdk`, `TreasuryForecast:Ai` |

---

*Document établi à partir de l'analyse du codebase FactuTrust au 14 août 2026. Les efforts sont exprimés en jours/homme pour une équipe senior connaissant le codebase, conception, développement, tests et documentation minimale inclus ; hors validation juridique, hors temps d'expert-comptable référent, hors infrastructure.*
