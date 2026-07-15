# Registre des licences — Thèmes PowerPoint FactuTrust

Document de conformité pour la bibliothèque de thèmes hybrid (.pptx). **Ne pas merger un thème sans entrée ici.**

## Politique

| Règle | Détail |
|-------|--------|
| Masters internes | Création FactuTrust — usage commercial SaaS autorisé |
| Photos CC0 | Unsplash / Pexels / Openverse — crédit recommandé dans slide Sources |
| Google Fonts | SIL Open Font License — pas d'attribution obligatoire |
| Slidesgo (si utilisé) | Slide crédits obligatoire ; max ~40 % du catalogue ; revue juridique |
| Microsoft stock | Interdit en redistribution template |

## Format d'entrée par thème

| Champ | Description |
|-------|-------------|
| `themeId` | Valeur enum `PowerPointTemplate` |
| `key` | Clé dossier asset (`wwwroot/assets/powerpoint/themes/{key}/`) |
| `source` | `Internal` \| `CC0-Photo` \| `Slidesgo` |
| `license` | Référence licence |
| `attributionText` | Texte affiché si requis |
| `assets[]` | Liste crédits images/fonts |
| `dateAudit` | ISO date |
| `reviewer` | Initiales validateur |

## Thèmes pilotes hybrid (Phase 1)

| ID | Key | Source | License | Attribution |
|----|-----|--------|---------|-------------|
| 24 | TechReport | Internal | Proprietary FactuTrust | — |
| 25 | MinimalBusiness | Internal + CC0 | FactuTrust / Unsplash License | Photo optionnelle dans Sources |
| 26 | DarkCinematic | Internal | Proprietary FactuTrust | — |

## Thèmes 0–23 (legacy tokens)

| ID | Key | Engine | Notes |
|----|-----|--------|-------|
| 0–2 | Standard, Analyse, Executive | Legacy (hybrid optionnel Phase 5) | Golden tests obligatoires |
| 3–23 | Voir PowerPointThemeLibrary | Legacy | Tokens couleur ; hybrid si asset présent |

## Thèmes 27–35 (hybrid internes)

Tous `source: Internal`, `license: Proprietary FactuTrust`, pas d'attribution slide requise.

## Fichier license.json (par thème)

```json
{
  "themeId": 24,
  "key": "TechReport",
  "source": "Internal",
  "license": "Proprietary-FactuTrust",
  "attributionText": null,
  "assets": [],
  "dateAudit": "2026-05-23",
  "reviewer": "FactuTrust-Engineering"
}
```

## Rollback

`TemplateHybridEnabled: false` dans `appsettings.json` → moteur legacy uniquement, ce registre reste la source de vérité pour activation future.
