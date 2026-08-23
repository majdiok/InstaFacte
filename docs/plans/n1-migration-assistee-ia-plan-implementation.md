# N1 — Migration assistée par IA : plan d'implémentation

**Version :** 1.0
**Date :** 15 août 2026
**Périmètre :** FactuTrust (.NET 8 / Angular 17+, multi-tenant, une base par tenant)
**Documents liés :** [Feuille de route IA](../strategy/ai-differentiation-roadmap.md) · [Analyse des écarts ERP](../strategy/erp-gap-analysis.md)
**Statut :** prêt pour arbitrage — aucune ligne de code écrite à ce stade

---

## 1. Synthèse exécutive

L'analyse du codebase montre que **la moitié de N1 existe déjà** : la reprise de dossier déterministe (plan comptable, plan tiers, balance d'ouverture, écritures) est implémentée avec dry-run, commit tout-ou-rien et table de correspondance de comptes. Ce qui manque — et ce qui fait échouer les migrations réelles — n'est pas l'import, c'est **la préparation** : comprendre le fichier source hétérogène (Sage Ligne 100, EBP, Cegid, Quadra, exports Excel « à la main »), mapper ses colonnes, traduire son plan de comptes vers le référentiel local, et détecter les doublons de tiers.

**Principe cardinal de ce plan : l'IA produit des intrants pour le pipeline existant ; elle ne touche jamais au pipeline lui-même.** La validation, l'équilibre, le commit additif et l'audit restent à 100 % dans `ReferenceDataImportService` et `JournalImportService`, inchangés. C'est ce choix qui garantit l'absence de régression : le chemin d'exécution final est exactement celui qu'exercent déjà les tests existants.

| Indicateur | Valeur |
|---|---|
| Effort total estimé | **~38 j/h** (dont 8 j/h de tests) |
| Durée | 6 à 8 semaines, 1 à 2 devs |
| Réutilisation directe de l'existant | pipeline d'import, table de correspondance, dry-run/commit, permissions, audit, frontend import-hub |
| Nouveaux endpoints | 4 (analyse, mapping colonnes, mapping comptes, rapport) |
| Modification de code existant | **0 ligne** dans les chemins de validation/commit ; extensions additives uniquement |
| Feature flag | `MigrationAi:Enabled` (défaut : désactivé) |

---

## 2. État des lieux — ce qui existe déjà (vérifié dans le code)

### 2.1 La reprise de dossier déterministe — déjà en production

| Capacité | Emplacement | État |
|---|---|---|
| Import plan comptable / plan tiers / balance d'ouverture, dry-run → commit tout-ou-rien, **strictement additif** | `Infrastructure/Services/ReferenceDataImportService.cs` (585 lignes) | Production |
| Table de correspondance de comptes source→cible, fournie **en fichier** (`source, cible`), appliquée avant validation | `Infrastructure/Services/AccountMappingTable.cs` | Production |
| Lecture tabulaire CSV/Excel avec synonymes de colonnes | `Infrastructure/Services/TabularRowReader.cs` (utilisé par les deux services) | Production |
| Import d'écritures (CSV/Excel/FEC) | `JournalImportService` (même patron dry-run/commit) | Production |
| Endpoints `POST reference-import/preview` et `/commit`, politique `PermissionPolicies.AccountingImport`, limite 25 Mo | `API/Controllers/AccountingController.cs:290-330` | Production |
| Feature flag `AccountingSettings.DossierImportEnabled` | `Application/Configuration/AccountingSettings.cs:31` | Production |
| Audit de l'import (`AuditActions.Accounting.DossierImported`) | `ReferenceImportCommands.cs` | Production |
| Écran frontend `reference-import.component.ts` dans le hub d'import | `features/accounting/import/` | Production |
| Tests | `ReferenceDataImportServiceTests.cs`, `AccountMappingTableTests.cs` | CI verte |

### 2.2 Le socle IA — déjà en production

| Capacité | Emplacement |
|---|---|
| Pipeline d'extraction structurée LLM avec réparation JSON, routage local (Ollama) / cloud (OpenRouter) | `Features/AI/AiStructuredExtractionPipeline.cs` (`IAiStructuredExtractionPipeline.RunAsync`) |
| Résolution du modèle par usage (import ≠ assistant ≠ studio) | `Features/AI/ImportAiModelResolver.cs`, `PlatformAiSettingsController.cs` |
| Patron de prompt système strict « JSON uniquement, n'invente jamais » | `ImportInvoiceFromFileCommand.cs` (modèle de référence à copier) |
| Comptes SCE tunisiens de référence | `Features/Accounting/TunisianPostingAccounts.cs` |

### 2.3 Ce qui n'existe PAS (à ne pas présupposer)

- **Aucun index vectoriel (S1)** et **aucun journal de décision IA (S2)** — confirmé par recherche sur l'ensemble du backend. Conséquence directe sur la conception : la détection de doublons et la réutilisation des mappings feront appel à de la **similarité déterministe** (normalisation, préfixes, Levenshtein borné) et à un **stockage relationnel**, pas à des embeddings. Quand S1/S2 arriveront (Vague 0 de la roadmap), ils s'ajouteront sans refonte — voir §8.4.
- Pas de modèle de données « migration » : tout est à créer, mais en **additif pur**.

### 2.4 Les 5 manques que N1 comble

| Manque actuel | Conséquence terrain | Réponse N1 |
|---|---|---|
| Colonnes source non reconnues (synonymes figés dans `Schema()`) | L'utilisateur renomme ses colonnes à la main dans Excel | **M1** — mapping de colonnes assisté |
| Table de correspondance de comptes construite à la main | 2 à 5 jours d'expertise comptable par migration | **M2** — proposition source→NCT éditable |
| Aucune détection de format/provenance | L'utilisateur doit savoir quel format choisir | **M0** — fingerprinting automatique |
| Aucune détection de doublons de tiers | Référentiel pollué dès le jour 1 | **M3** — rapprochement déterministe + arbitrage |
| Aucun rapport de migration | Pas de preuve, pas de dossier de reprise pour l'auditeur | **M4** — rapport PDF chiffré et narratif |

---

## 3. Architecture cible

### 3.1 Le flux de bout en bout

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         WIZARD FRONTEND (6 étapes)                       │
│  1.Dépôt → 2.Analyse → 3.Correspondances → 4.Aperçu → 5.Commit → 6.Rapport │
└─────────────────────────────────────────────────────────────────────────┘
        │                    │                    │               │
        ▼                    ▼                    ▼               ▼
  [upload fichier]   [MigrationAssistant     [suggestions     [EXISTANT,
                      — NOUVEAU, IA]          éditables par    INTACT]
                       · fingerprint           l'humain]       ReferenceData-
                       · détection format                          ImportService
                       · mapping colonnes                          JournalImportService
                       · mapping comptes
                       · doublons tiers
                              │
                              ▼
              [MigrationLearningStore — NOUVEAU]
              profils validés, stats d'acceptation
```

**Règle d'or :** les étapes 4 et 5 appellent les endpoints existants `reference-import/preview` et `reference-import/commit`. L'IA n'a aucun chemin d'écriture propre. Si tout le code nouveau est supprimé, la fonctionnalité d'import historique continue de fonctionner à l'identique.

### 3.2 Arborescence du code à créer

```
src/Backend/FactuTrust.Application/Features/Migration/
    Commands/
        AnalyzeMigrationSourceCommand.cs          ← fingerprint + profil connu ?
        SuggestColumnMappingCommand.cs            ← colonnes source → schéma canonique
        SuggestAccountMappingCommand.cs           ← comptes source → plan local
        DetectThirdPartyDuplicatesCommand.cs      ← paires candidates de doublons
        GenerateMigrationReportCommand.cs         ← rapport PDF
    DTOs/
        MigrationDtos.cs                          ← tous les DTO (voir §4.3)
    Services/
        IMigrationAssistantService.cs             ← port applicatif
        IMigrationLearningStore.cs                ← port profils validés

src/Backend/FactuTrust.Infrastructure/Services/Migration/
    MigrationAssistantService.cs                  ← orchestration (déterministe + LLM)
    SourceFormatFingerprinter.cs                  ← détection Sage/EBP/Cegid/Excel (déterministe)
    KnownFormatCatalog.cs                         ← empreintes et synonymes par progiciel
    ThirdPartyDuplicateDetector.cs                ← similarité normalisée (déterministe)
    MigrationLearningStore.cs                     ← persistance Master DB
    MigrationReportBuilder.cs                     ← PDF via IPdfService existant

src/Backend/FactuTrust.API/Controllers/
    MigrationAssistantController.cs               ← 4 endpoints, politique AccountingImport

src/Frontend/factutrust-web/src/app/features/accounting/import/
    migration-wizard/                             ← assistant 6 étapes (voir §6)
```

**Aucun fichier existant n'est modifié dans sa logique.** Les seuls points de contact avec l'existant sont additifs : enregistrement DI, un contrôleur, une entrée de menu dans `import-hub`, des entités/migrations EF Core, une section de configuration.

### 3.3 Modèle de données (additif)

**Tenant DB — journal de décision local (préfiguration de S2, périmètre migration) :**

`MigrationDecisionLogs` : `Id`, `Kind` (`ColumnMapping` | `AccountMapping` | `DuplicateTriage`), `SubjectRef` (compte source / paire de tiers), `ProposedJson`, `FinalJson`, `Outcome` (`Accepted` | `Edited` | `Rejected`), `Confidence`, `ModelRef`, `PromptVersion`, `DecidedByUserId`, `DecidedAtUtc`. → C'est la table `AiDecisionLog` de la roadmap, limitée à la migration : quand S2 arrivera, elle sera fusionnée ou lue telle quelle.

**Master DB — profils de migration réutilisables (inter-tenants, sans aucune donnée client) :**

`MigrationMappingProfiles` : `Id`, `SourceFingerprint` (empreinte du format : progiciel détecté + signature d'en-têtes normalisée, jamais de données), `ColumnMappingJson`, `AccountMappingJson`, `UsageCount`, `AcceptedRate`, `UpdatedAtUtc`.

> **Point de confidentialité :** un profil ne contient que des noms de colonnes et des numéros de comptes de progiciels connus — aucune donnée nominative, aucun solde. C'est ce qui rend le partage inter-tenants acceptable ; à valider dans la DPA.

---

## 4. Spécification des 5 moteurs

### 4.1 M0 — Fingerprinting du format source *(déterministe d'abord, LLM en repli)*

**Fonctionnement en deux étages :**

1. **`SourceFormatFingerprinter` (déterministe).** Lit les en-têtes et un échantillon de 20 lignes ; confronte à `KnownFormatCatalog` : signatures d'en-têtes caractéristiques (ex. Sage Ligne 100 : `CG_NUM,CG_INTITC` ; EBP : `Compte;Intitulé;Collectif`), conventions de codification (longueurs, préfixes), séparateur, encodage. Sortie : `Sage100 | Ebp | Cegid | Quadra | ExcelGenerique | Inconnu` + score.
2. **Repli LLM** uniquement si `Inconnu` ou score < seuil : le modèle reçoit **les en-têtes et 5 lignes anonymisées** (montants remplacés par des formes `999,99`, noms par `AAAA`) et classe le format. Jamais de données réelles envoyées pour cette étape.

**Sortie :** format détecté, confiance, séparateur/encodage, cibles applicables (un fichier peut contenir plan comptable ET plan tiers ET balance — le fingerprint détecte la cible probable par onglet Excel).

**Critère d'acceptation :** 100 % de détection correcte sur le corpus de tests (§7.2) ; abstention explicite au lieu d'une classification forcée.

### 4.2 M1 — Mapping de colonnes assisté

**Problème concret :** `Schema()` de `ReferenceDataImportService` impose des synonymes figés (`compte/numero/account…`). Un export Sage avec `CG_NUM` est rejeté.

**Fonctionnement :**

1. Si le format est connu (M0) → mapping de colonnes **du catalogue**, proposé tel quel. Pas d'appel LLM.
2. Sinon → le LLM reçoit la liste des en-têtes source et le schéma canonique de la cible (ex. pour `ChartOfAccounts` : `compte, libelle, classe, nature?`) et produit un JSON strict : `{ "CG_NUM": "compte", "CG_INTITC": "libelle", ... }` avec une confiance par colonne. Prompt système calqué sur celui de `ImportInvoiceFromFileCommand` (JSON uniquement, jamais d'invention, `null` si incertain).
3. **Application sans toucher au service existant :** le backend **réécrit l'en-tête du fichier** côté serveur (renommage de colonnes vers le schéma canonique, une seule fois, en mémoire) puis appelle `PreviewAsync` **inchangé**. Alternative retenue contre l'ajout d'un paramètre au service : zéro impact sur le code testé.
4. Présentation à l'utilisateur : grille éditable « colonne source → colonne cible → confiance », modifiable avant aperçu.

**Garde-fous :** une colonne cible déclarée deux fois → anomalie bloquante ; colonne source non mappée et requise → l'aperçu existant produit l'erreur comme aujourd'hui ; la suggestion n'est jamais appliquée silencieusement — l'utilisateur voit la grille.

### 4.3 M2 — Mapping comptable source → plan local *(le cœur de la valeur)*

**Fonctionnement en quatre étages, du moins cher au plus cher :**

1. **Identité** : le compte source existe déjà dans le plan local → proposition = identique (cas fréquent : NCT déjà utilisé dans l'ancien logiciel).
2. **Profil appris** : `MigrationLearningStore` contient un profil du même fingerprint avec ce compte → proposition reprise, avec son taux d'acceptation historique affiché.
3. **Préfixe NCT** : rapprochement par préfixe de classe/sous-classe (déterministe, `TunisianPostingAccounts` + plan local) avec libellé normalisé.
4. **LLM borné** : pour les comptes restants, le modèle choisit **parmi la liste fermée des comptes du plan local** (injectée dans le prompt, plafonnée — si le plan dépasse ~400 comptes, on n'envoie que la classe correspondante). Schéma : `{ source, cible, confiance, justification }`. Le modèle ne peut pas produire un compte hors de la liste — contrôle programmatique après parsing, rejet de la ligne sinon.

**Sortie :** la grille de correspondance éditable. À la validation, le frontend **génère le CSV `source, cible`** et le transmet comme `accountMappingContent` — le paramètre **déjà supporté** par `PreviewAsync`/`CommitAsync`. Aucune nouvelle logique d'application n'est écrite.

**Garde-fous (hérités de l'existant, gratuits) :** une cible absente du plan comptable est déjà une anomalie bloquante (`ApplyAccountMappingAsync`) ; les correspondances jamais rencontrées sont déjà remontées (`UnusedMappings`) ; tout passe par le dry-run.

**Critère d'acceptation :** sur le corpus de tests, ≥ 90 % des comptes « évidents » (identité/préfixe) résolus sans LLM ; 0 % de proposition hors plan local ; taux d'abstention mesuré et affiché.

### 4.4 M3 — Détection de doublons de tiers *(déterministe)*

**Fonctionnement.** Sur le plan tiers importé + le référentiel existant :

| Signal | Méthode | Poids |
|---|---|---|
| NIF / matricule fiscal identique | égalité exacte | bloquant-fort |
| Email ou téléphone identique | égalité exacte | fort |
| Nom normalisé (casse, accents, formes juridiques : STE/SARL/SUARL retirées) | égalité | fort |
| Nom proche | Levenshtein borné (distance ≤ 2) sur nom normalisé + même ville | moyen |
| Sigle vs nom développé | tokenisation + inclusion | moyen |

Sortie : paires candidates avec score et **motif lisible** (« même matricule fiscal », « noms quasi identiques, même ville »). Le LLM n'intervient pas en V1 — la similarité déterministe est explicable devant un auditeur ; les embeddings (S1) enrichiront le signal plus tard.

**Périmètre strict :** N1 **signale** les doublons, il ne **fusionne pas**. La fusion réversible est le périmètre de N7 (feuille de route). L'utilisateur choisit par paire : *ignorer / importer quand même / ne pas importer le doublon*. Cette décision est écrite dans `MigrationDecisionLogs` (Kind=`DuplicateTriage`).

### 4.5 M4 — Rapport de migration

**Fonctionnement.** Après chaque commit réussi : collecte **déterministe** des chiffres (lignes lues, créées, ignorées, comptes traduits, doublons signalés, écarts résiduels) → le LLM rédige **uniquement la narration** (résumé exécutif, points de vigilance, suite recommandée) avec interdiction de produire un montant — les chiffres sont injectés par gabarit. PDF via `IPdfService` existant (patron des exports `RevisionDossierPdf`).

**Valeur :** c'est le dossier de reprise opposable — le document que le cabinet montre à l'auditeur et que le commercial montre au prospect.

---

## 5. API et intégration backend

### 5.1 Endpoints nouveaux (`MigrationAssistantController`)

| Endpoint | Rôle | Réponse |
|---|---|---|
| `POST api/accounting/migration/analyze` | M0 : fingerprint + détection de cible(s) par fichier/onglet | `MigrationAnalysisDto` |
| `POST api/accounting/migration/suggest-column-mapping` | M1 : correspondance de colonnes | `ColumnMappingSuggestionDto` |
| `POST api/accounting/migration/suggest-account-mapping` | M2 : correspondance de comptes | `AccountMappingSuggestionDto` |
| `POST api/accounting/migration/detect-duplicates` | M3 : paires de doublons tiers | `DuplicateDetectionDto` |
| `GET api/accounting/migration/report/{importAuditId}` | M4 : PDF du rapport | `application/pdf` |

- **Politique :** `PermissionPolicies.AccountingImport` (même politique que l'import existant — pas de nouvelle permission en V1).
- **Limites :** `RequestSizeLimit(25_000_000)`, aligné sur l'existant.
- **Aucun endpoint existant modifié.** Le wizard appelle ensuite `reference-import/preview` et `reference-import/commit` tels quels.
- **Feature flag :** `MigrationAi:Enabled` (défaut `false`) vérifié dans chaque action → `404` si désactivé, comme les autres modules optionnels.

### 5.2 Configuration (additif dans `appsettings.json`)

```json
"MigrationAi": {
  "Enabled": false,
  "Model": null,                       // null = modèle plateforme d'import (ImportAiModelResolver)
  "MaxAccountsSentToModel": 400,
  "DuplicateMinScore": 0.6,
  "KnownProfilesEnabled": true,        // réutilisation des profils Master DB
  "ReportNarrativeEnabled": true
}
```

### 5.3 Injection de dépendances

Enregistrement dans `Infrastructure/DependencyInjection.cs` derrière le flag, en suivant le patron des services IA existants. Le `MigrationAssistantService` consomme `IAiStructuredExtractionPipeline` **tel quel** — pas de nouveau fournisseur d'inférence.

---

## 6. Frontend — wizard de migration (Angular)

Nouveau dossier `features/accounting/import/migration-wizard/`, entrée ajoutée dans `import-hub.component.ts` (carte « Migration assistée — depuis Sage, EBP, Excel… »). Standalone components, PrimeNG, mêmes conventions CSS que `reference-import.component.ts`.

| Étape | Contenu | Sécurité UX |
|---|---|---|
| **1. Dépôt** | Multi-fichiers (plan, tiers, balance) ou classeur multi-onglets | Types et taille contrôlés |
| **2. Analyse** | Format détecté par fichier, cible proposée, confiance | L'utilisateur peut corriger la cible |
| **3. Correspondances** | Onglets : *Colonnes* (grille source→cible éditable, badge de confiance) · *Comptes* (grille éditable, recherche dans le plan local, filtre « confiance < seuil ») · *Doublons* (paires + décision par paire) | Rien n'est appliqué à cette étape ; tout est éditable ; compteur « X suggestions à vérifier » |
| **4. Aperçu** | Appelle `reference-import/preview` — **écran existant réutilisé** (`reference-import.component.ts` en sous-composant) | Commit impossible si `canCommit = false` (règle existante) |
| **5. Commit** | Appelle `reference-import/commit` ; récapitulatif créé/ignoré | Confirmation explicite avec résumé chiffré |
| **6. Rapport** | Téléchargement du PDF + lien vers les écritures d'à-nouveau en brouillon | — |

**Règles UX non négociables :** à aucun moment le commit n'est accessible sans être passé par l'aperçu ; les suggestions IA sont visuellement distinguées (badge « IA » + confiance) ; l'écran fonctionne en mode dégradé si l'IA est désactivée (le wizard devient l'import manuel existant — même composant, sans les étapes de suggestion).

---

## 7. Stratégie anti-régression et plan de tests

### 7.1 Ce qui garantit l'absence de régression — par construction

1. **Zéro modification** de `ReferenceDataImportService`, `JournalImportService`, `AccountMappingTable`, `TabularRowReader` — leurs ~tests existants restent la preuve.
2. Les suggestions IA entrent par les **mêmes trous d'aiguille** que la saisie manuelle : un fichier dont les en-têtes sont canoniques, un CSV `source,cible`. Si la suggestion est absurde, c'est le validateur existant qui la rejette — pas un code nouveau.
3. **Feature flag off par défaut** : la CI et la production sans le flag exécutent exactement le code actuel.
4. L'IA n'a **aucun accès en écriture** : elle lit des en-têtes et des listes de comptes, elle produit du JSON validé par schéma, re-parsé et contrôlé programmatiquement.

### 7.2 Tests à écrire (~8 j/h inclus dans l'estimation)

| Couche | Tests | Emplacement |
|---|---|---|
| Unitaires déterministes | fingerprint sur 15 formats synthétiques (Sage/EBP/Cegid/Excel variants), mapping de colonnes catalogue, détecteur de doublons (cas limites : formes juridiques, accents, sigles, NIF), contrôle « cible hors plan local rejetée » | `tests/FactuTrust.Infrastructure.Tests/Migration/` |
| Unitaires IA (LLM mocké) | parsing + réparation JSON des suggestions, abstention sous seuil, plafond `MaxAccountsSentToModel`, anonymisation M0 (aucun montant réel ne part — test d'assertion sur le prompt construit) | idem, via `IAiStructuredExtractionPipeline` stubbé |
| Intégration API | endpoints nouveaux (auth, flag off → 404, taille max), et **rejoue complète du chemin wizard** : suggestion → fichier réécrit → `preview` → `commit` sur LocalDB | `tests/FactuTrust.API.Tests` |
| Corpus « golden » | fichiers d'export synthétiques versionnés par progiciel avec mappings attendus — même patron que `docs/ai-screen-analysis/golden/` | `tests/Fixtures/Migration/` |
| Non-régression | exécution inchangée de `ReferenceDataImportServiceTests` et `AccountMappingTableTests` en CI — **critère de merge bloquant** | `ci.yml` (rien à changer) |
| Frontend | specs du wizard (état des étapes, blocage du commit, mode dégradé flag off) | specs Angular existantes |

### 7.3 Données de test

Les exports Sage/EBP/Cegid réels sont des données clients — **jamais en CI**. Le corpus golden est **synthétique** : fichiers construits à partir des spécifications publiques de formats, avec des données fictives. Un protocole de recueil de fichiers anonymisés auprès de 2–3 cabinets pilotes alimente la recette, hors repo.

---

## 8. Séquencement détaillé

| Phase | Contenu | Effort | Critère de sortie |
|---|---|---|---|
| **P0 — Fondations** (3 j/h) | Entités + migrations EF (tenant + master), feature flag, squelette contrôleur, DI | — | Flag off = comportement strictement actuel, démontré par tests existants |
| **P1 — Fingerprint + analyse** (6 j/h) | M0 complet, catalogue des 4 formats, endpoint `analyze`, corpus golden v1 | — | 100 % du corpus classé correctement |
| **P2 — Mapping colonnes** (6 j/h) | M1, réécriture d'en-têtes, endpoint, grille frontend | — | Un export Sage brut importé sans retouche manuelle, via preview/commit existants |
| **P3 — Mapping comptes** (9 j/h) | M2 (4 étages), endpoint, grille comptes frontend, génération du CSV de correspondance | — | ≥ 90 % des comptes évidents sans LLM ; 0 proposition hors plan ; validation bloquante héritée opérationnelle |
| **P4 — Doublons tiers** (5 j/h) | M3, endpoint, onglet frontend, décisions journalisées | — | Précision ≥ 90 % sur corpus synthétique de doublons |
| **P5 — Rapport** (4 j/h) | M4, PDF, injection chiffrée des montants | — | Rapport généré sur migration de bout en bout en environnement de recette |
| **P6 — Apprentissage** (3 j/h) | `MigrationLearningStore`, remontée des profils à l'étape M2.2, stats d'acceptation | — | Une 2e migration du même format propose le mapping validé de la 1re |
| **P7 — Wizard + finition** (2 j/h) | Assemblage 6 étapes, mode dégradé, entrée import-hub, doc utilisateur courte | — | Scénario complet joué en recette, revue UX |
| **Total** | | **~38 j/h** | |

**Dépendances externes :** aucune. S1/S2 ne sont **pas** des prérequis — ce plan les préfigure (§2.3) sans les attendre.

### 8.4 Compatibilité ascendante avec la roadmap

- Quand **S1** (embeddings) existera : M3 gagne un signal sémantique, M2.4 gagne du few-shot sur libellés proches — deux injections additives derrière des interfaces déjà prévues.
- Quand **S2** (journal de décision générique) existera : `MigrationDecisionLogs` y migre ou est lu en parallèle — le schéma est volontairement aligné sur celui de la roadmap.
- Quand **N7** (fusion de tiers) existera : M3 lui fournit sa file de candidats.

---

## 9. Risques et mitigations

| Risque | Gravité | Mitigation |
|---|---|---|
| Suggestion de mapping comptable fausse mais plausible | Élevée | Étape 3 éditable obligatoire · dry-run existant · cible hors plan = bloquant · badge de confiance · compteur « à vérifier » |
| Formats sources plus hétérogènes que prévu | Moyenne | Fingerprint déterministe d'abord · abstention explicite · le mode manuel existant reste accessible dans le même écran |
| Fuite de données vers le modèle cloud | Élevée | Anonymisation M0 · M2 n'envoie que des numéros/libellés de comptes (jamais de soldes, jamais de tiers) · routage local Ollama possible (cohérent avec l'argument souveraineté) |
| Coût/latence LLM sur gros plans de comptes | Faible | Plafond `MaxAccountsSentToModel` · étages 1–3 sans LLM · modèle d'import léger déjà réglé par `ImportAiModelResolver` |
| Profils appris erronés qui se propagent | Moyenne | Taux d'acceptation affiché · profil désactivable · un profil n'est promu qu'après 2 validations acceptées |
| Régression sur l'import existant | **Visée : zéro** | §7.1 — aucune modification du chemin existant ; suite de tests existante comme preuve bloquante |

---

## 10. Mesure du succès

| KPI | Cible | Source |
|---|---|---|
| Durée d'une migration standard (plan + tiers + balance) | **< 1 jour** (vs 2–5 j) | suivi commercial pilotes |
| Taux d'acceptation des suggestions de mapping comptable | > 80 % au 1er profil, > 95 % au 5e | `MigrationDecisionLogs` |
| Propositions hors plan local | **0 %** (contrôle programmatique) | tests + télémesure |
| Migrations abouties sans retouche manuelle du fichier | > 70 % | `MigrationDecisionLogs` |
| Taux de conversion des prospects en migration assistée | + points vs autonome | pipeline commercial |

---

## 11. Prochaine étape

1. Arbitrage produit sur ce plan (périmètre V1, formats du catalogue initial).
2. Constitution du corpus golden synthétique (P1) — peut démarrer immédiatement.
3. Démarrage P0–P2 : elles sont indépendantes de toute décision métier et sans risque.
