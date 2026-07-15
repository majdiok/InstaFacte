# Checklist QA — Export PowerPoint Assistant IA

Document de référence pour la validation manuelle et automatique de la fonctionnalité « Export
PowerPoint depuis l'Assistant IA » (`POST /api/ai/exports/powerpoint`).

> **Périmètre :** v2.0 — **36 thèmes** (3 legacy + 33 hybrid masters), galerie picker type Dokie
> (previews 16:9, recherche), moteur hybrid `.pptx` derrière feature flag
> `TemplateHybridEnabled` (désactivé par défaut), étape wizard « Thème », 2 orientations (16:9 / 4:3),
> sélection multi-réponses inter-conversations, génération serveur, téléchargement signé (TTL 1 h),
> audit & purge 24 h.

---

## 1. Tests automatisés exécutés (gate CI)

### 1.1 Backend (xUnit)

| Suite | Tests | Statut |
|---|---|---|
| `MarkdownToOpenXmlConverterTests` | 7 cas (vide, paragraphe, heading, listes, gras, lien, suppression JSON) | OK |
| `PowerPointGeneratorTests` | E2E + 36 thèmes legacy + baseline 0–2 × orientations | OK |
| `HybridTemplateGeneratorTests` | Hybrid pilots, router flags, 33 hybrid themes OpenXml | OK |
| `PowerPointThemeLibraryTests` | 36 defs, hybrid metadata, catalogue previews, resolver fallback | OK |
| `FilesystemExportStorageServiceTests` | 5 cas (signature, expiration, multi-tenant, cleanup) | OK |
| **Tests Infrastructure totaux** | **534 / 534** | **OK** |
| **Tests API totaux** | **20 / 20** | **OK** |

Exécution :
```bash
dotnet test src/Backend/tests/FactuTrust.Infrastructure.Tests/FactuTrust.Infrastructure.Tests.csproj
dotnet test src/Backend/tests/FactuTrust.API.Tests/FactuTrust.API.Tests.csproj
```

### 1.2 Frontend (Karma + Jasmine)

| Suite | Tests | Statut |
|---|---|---|
| `MessageSelectionService` (Signals) | 8 cas (toggle, multi-conversations, limite, reorder…) | OK |
| `PowerPointExportService` (HTTP) | inline blob, preview, templates catalogue | OK |
| `PowerPointThemePickerComponent` | filtres, recherche, thumbnails 16:9, sélection | OK |

Exécution :
```bash
cd src/Frontend/factutrust-web
npx ng test --watch=false --browsers=ChromeHeadless --include="src/app/features/ai-assistant/services/message-selection.service.spec.ts" --include="src/app/features/ai-assistant/services/powerpoint-export.service.spec.ts"
```

> **NB :** Le test `chat-speech-transcription.service.spec.ts` échoue **avant** ces changements
> (initialisation `lastFakeRec` non robuste, sans rapport avec PowerPoint). Hors périmètre.

---

## 2. Non-régression — Exports existants

Aucune des modifications n'a touché les modules existants. Vérifier néanmoins que les **8 exports
historiques** restent fonctionnels :

| # | Export | Module | Vérifié |
|---|---|---|---|
| 1 | Facture PDF | `InvoicePdfService` | ☐ |
| 2 | Liste factures Excel | `InvoiceExcelExportService` | ☐ |
| 3 | Liste clients Excel | `CustomerExcelExportService` | ☐ |
| 4 | Stock CSV | `InventoryCsvService` | ☐ |
| 5 | Rapport ventes PDF | `SalesReportPdfService` | ☐ |
| 6 | DGI (XML CGT) | `CgtXmlExportService` | ☐ |
| 7 | Comptabilité (FEC) | `FecExportService` | ☐ |
| 8 | Assistant IA → PDF | `AssistantPdfExportService` | ☐ |

**Procédure :**
1. `dotnet run --project src/Backend/FactuTrust.API`
2. Pour chaque module ci-dessus : déclencher l'export depuis l'UI → fichier généré OK → contenu correct.

---

## 3. Validation manuelle — Lecture .pptx dans les 3 environnements

Les fichiers d'exemple sont générés via :
```powershell
$env:AI_EXPORT_DUMP_DIR = "C:\Solution\FactuTrust - Copy\artifacts\qa-pptx"
dotnet test src/Backend/tests/FactuTrust.Infrastructure.Tests/FactuTrust.Infrastructure.Tests.csproj `
  --filter "FullyQualifiedName~GenerateAsync_all_template_and_orientation_combinations"
```

6 fichiers produits : `{Executive|Analyse|Standard}_{Widescreen16x9|Standard4x3}.pptx`.

### 3.1 Microsoft PowerPoint Desktop (Windows + macOS)

Pour chaque fichier :
- ☐ Le fichier s'ouvre sans message d'erreur ni alerte de récupération.
- ☐ Le thème (couleurs, polices) correspond à la maquette.
- ☐ La couverture affiche : titre, sous-titre, auteur, date.
- ☐ L'agenda liste les sections numérotées.
- ☐ Les slides KPI affichent valeur + tendance + unité.
- ☐ Les tableaux sont rendus comme **vrais tableaux Office** (alignement, en-têtes colorés).
- ☐ Les graphiques (bar) s'ouvrent en mode édition (clic droit → Modifier les données).
- ☐ Les notes du présentateur sont visibles (mode « Page de commentaires »).
- ☐ Le slide « Sources » liste les outils MCP appelés.
- ☐ Format 16:9 = ratio écran large ; 4:3 = ratio classique.

### 3.2 LibreOffice Impress (Linux + Windows)

- ☐ Ouverture sans avertissement de compatibilité bloquant (un avertissement informatif est toléré).
- ☐ Les couleurs du thème sont préservées.
- ☐ Les tableaux restent éditables.
- ☐ Les graphiques sont rendus (au moins en aperçu).

### 3.3 Google Slides (import .pptx)

- ☐ Upload via Drive → ouverture dans Slides.
- ☐ Les couleurs du thème sont préservées.
- ☐ Les graphiques peuvent être incorrects (Google Slides convertit) — **toléré** dans la v1.

---

## 4. Tests fonctionnels end-to-end (manuels — UI)

### 4.1 Sélection mono-réponse

1. ☐ Ouvrir l'Assistant IA, envoyer un prompt produisant du markdown.
2. ☐ Cliquer sur le bouton « PPT » dans la toolbar de la réponse.
3. ☐ Le dialogue s'ouvre **avec cette réponse pré-sélectionnée**.
4. ☐ Compléter le titre → générer → fichier téléchargé.
5. ☐ Vérifier badges preview (KPI, sections) et outline slides à l'étape Réponses.

### 4.1bis Cas invoice-list (régression premium)

1. ☐ Analyser « Liste des factures » avec l'assistant IA.
2. ☐ Exporter en template Executive.
3. ☐ Pas de syntaxe markdown `|` visible ; cartes KPI présentes.
4. ☐ Footers (titre deck + page) sur slides contenu.
5. ☐ ≤ 10 slides pour une réponse standard.

### 4.2 Sélection multi-réponses (même conversation)

1. ☐ Activer le mode « Sélection » dans le header.
2. ☐ Cocher 2-3 réponses → la barre sticky affiche « N réponses sélectionnées ».
3. ☐ Cliquer « Exporter PowerPoint » → dialogue avec liste pré-remplie.
4. ☐ Réordonner (↑/↓), retirer (×), filtrer les blocs → générer.
5. ☐ Le fichier contient bien les blocs filtrés (pas de KPI si décochés, etc.).

### 4.3 Sélection inter-conversations

1. ☐ Sélectionner une réponse dans la conversation A.
2. ☐ Passer à la conversation B sans quitter le mode sélection.
3. ☐ Sélectionner une réponse dans B.
4. ☐ La barre sticky additionne les sélections.
5. ☐ L'export inclut une section par conversation.

### 4.4 Étape Thème (bibliothèque 36 thèmes — galerie Dokie)

1. ☐ Le stepper affiche : Configurer → **Thème** → Réponses → Générer → Terminer.
2. ☐ Modal élargi (~900px) avec zone thèmes scrollable.
3. ☐ Recherche texte filtre par nom / description.
4. ☐ Filtres : Tous | Clair | Sombre | Premium | Coloré réduisent la grille.
5. ☐ Chaque carte affiche une **vignette 16:9** (`previewThumbnailUrl`) + nom + description.
6. ☐ Thèmes hybrid (id ≥ 3) affichent badge « Master ».
7. ☐ Thèmes représentatifs Desktop : Pearl, Vortex, Executive, Nova, TechReport, DarkCinematic.
8. ☐ La sélection est mémorisée (`localStorage` `factutrust.ppt.lastTheme`, ids 0–35).
9. ☐ Rollback flag `ExtendedThemeLibraryEnabled: false` → API retourne 3 thèmes uniquement.
10. ☐ **Vignettes hybrid visibles sur `localhost:4200`** : URLs résolues vers l'origine API (`https://localhost:7001/assets/powerpoint/...`).
11. ☐ Thèmes legacy 0–2 (Standard, Analyse, Executive) : fallback CSS (dégradé + texte), **pas d'icône cassée**.
12. ☐ Thèmes sombres (Vortex, Indigo, Onyx) : texte « Titre » lisible dans la vignette SVG (pas de `##FFFFFF`).
13. ☐ Échec réseau simulé (API arrêtée ou 404) → fallback CSS automatique, pas d'icône cassée.
14. ☐ Proxy dev (`proxy.conf.json` → `/assets/powerpoint`) actif en secours si URL relative non résolue.

### 4.5 Templates & moteur hybrid (non-régression)

1. ☐ `TemplateHybridEnabled: false` dans `appsettings.json` (prod) ; `true` dans `appsettings.Development.json`.
2. ☐ Payload POST utilise des enums **string PascalCase** (`"Vortex"`, `"Widescreen16x9"`) — pas de nombres.
3. ☐ Erreur 400 affiche le message backend (plus « Unknown Error ») dans le dialogue.
4. ☐ Standard / Analyse / Executive conservent le rendu historique (enum 0–2 inchangés).
5. ☐ Avec hybrid activé en dev : thèmes ≥ 3 (Pearl, Vortex, TechReport) exportent le scénario « liste factures ».
6. ☐ Fallback legacy si le moteur hybrid échoue (log warning backend).
7. ☐ Les 36 thèmes produisent un .pptx valide (OpenXmlValidator).

### 4.6 États de génération

1. ☐ Étape « Génération » : spinner accessible (`aria-busy`, `aria-live="polite"`).
2. ☐ Étape « Terminé / OK » : nom de fichier + nombre de slides + bouton « Télécharger à nouveau ».
3. ☐ Étape « Terminé / Erreur » : message localisé + bouton « Réessayer » qui ramène au step 2.

### 4.7 Limites & validation

1. ☐ Tenter d'ajouter plus de 50 réponses → bouton désactivé + texte « limite atteinte ».
2. ☐ Soumettre sans titre → bouton « Suivant » désactivé.
3. ☐ Soumettre sans réponse → bouton « Générer » désactivé.
4. ☐ Décocher tous les blocs d'une réponse → bouton « Générer » désactivé.
5. ☐ Preset « Deck exécutif » applique template Executive + cover + agenda + TOC.

### 4.8 Téléchargement signé (deferred)

1. ☐ Générer un export volumineux (>6 Mo) → réponse 202 + URL signée.
2. ☐ Le bouton « Télécharger » utilise l'URL → succès.
3. ☐ Attendre l'expiration du token (1 h) → re-tentative → 404 « Lien expiré ».

---

## 5. Sécurité & multi-tenancy

| Cas | Attendu | Vérifié |
|---|---|---|
| Utilisateur sans permission `AiChat` | 403 | ☐ |
| Token volé, autre tenant tente download | 404 (silencieux) | ☐ |
| Token tronqué / falsifié | 404 | ☐ |
| Token expiré | 404 | ☐ |
| Rate limiting (politique `ai`) appliqué | 429 après seuil | ☐ |

---

## 6. Performance & robustesse

| Cas | Cible | Vérifié |
|---|---|---|
| Génération 1 réponse (texte + KPI + table + graphique) | < 1 s sur poste dev | ☐ |
| Génération 10 réponses | < 5 s | ☐ |
| Génération 50 réponses (limite haute) | < 30 s | ☐ |
| Mémoire pic process backend | < 200 Mo | ☐ |
| Job Hangfire `ai-export-cleanup` exécuté | toutes les heures | ☐ |
| Purge effective après 24 h | dossiers tenants vides supprimés | ☐ |

---

## 7. Accessibilité (WCAG 2.1 AA — composants UI)

| Élément | Critère | Vérifié |
|---|---|---|
| Dialogue export | `role="dialog"`, focus trap, `Esc` ferme | ☐ |
| Stepper | `aria-current="step"` | ☐ |
| Champs requis | `aria-required="true"` | ☐ |
| Liste de réponses | navigation clavier (↑/↓/Tab) | ☐ |
| États (loading/error) | `aria-live="polite"` | ☐ |
| Contraste couleurs templates | ratio ≥ 4.5:1 sur texte | ☐ |
| Spinner | `aria-busy="true"` sur le conteneur | ☐ |

---

## 8. Sign-off

| Rôle | Nom | Date | Signature |
|---|---|---|---|
| Tech Lead | | | |
| Product Owner | | | |
| QA | | | |
| Sécurité / DPO | | | |

---

_Document généré dans le cadre de l'implémentation `powerpoint_export_assistant_ia` (mai 2026)._
