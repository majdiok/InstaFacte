# Superieur Admin — Baseline visuelle

## Objectif

Figer l’état InstaFact avant migration theme-superieur, et documenter la checklist QA.

## Feature flag

- Classe CSS : `theme-superieur` sur `<html>`
- Contrôle : `AppComponent` → `SUPERIEUR_THEME_ENABLED = true` (activé Phase 7)
- Rollback : mettre `SUPERIEUR_THEME_ENABLED = false` ou retirer la classe = retour immédiat au look Poseidon/InstaFact

## Activation

Thème Superieur **activé par défaut** depuis Phase 7 (`SUPERIEUR_THEME_ENABLED = true` dans `app.component.ts`).

## Screenshots baseline (à capturer manuellement)

| Écran | Route | Notes |
|-------|-------|-------|
| Login | `/auth/login` | Split panel branding |
| Dashboard company | `/dashboard` | KPI + chart + bottom-grid |
| Firm dashboard | `/firm/dashboard` | KPI + panels |
| CRM dashboard | `/crm/dashboard` | 3 KPI + listes |
| Invoices list | `/invoices` | Table PrimeNG |
| Confirm modal | n’importe quelle action destructive | NgbModal |
| POS | `/pos` | **Ne doit PAS changer** |

## Checklist visuelle (32 points) — Phase 7

### Shell (8)
- [ ] Sidebar fond blanc
- [ ] Item actif bleu plein + texte blanc
- [ ] Hover item sidebar
- [ ] Collapse dual-rail
- [ ] Header bleu royal uni
- [ ] Icônes header blanches
- [ ] Badge notification rouge
- [ ] Content bg `#f4f6f9`

### Auth (6)
- [ ] Login split-card Superieur
- [ ] Inputs icon-box + underline
- [ ] Bouton SIGN IN bleu
- [ ] Register wizard restylé
- [ ] Register-firm restylé
- [ ] Select-warehouse harmonisé + mobile 991px

### Dashboards (8)
- [ ] Company KPI solid
- [ ] Company chart area (p-chart)
- [ ] Company bottom panels
- [ ] Firm KPI / panels
- [ ] CRM KPI solid
- [ ] Drag-drop blocks company
- [ ] Drill-down links
- [ ] Skeletons / empty states

### Listes & overlays (6)
- [ ] Table invoices hover
- [ ] Status badges pills
- [ ] Pagination PrimeNG
- [ ] Confirm modal
- [ ] Quick-create dialog
- [ ] Drawer z-index

### Non-régression (4)
- [ ] POS plein écran inchangé
- [ ] AI FAB + chat panel
- [ ] Tokens `--pos-*` / `--ai-*` intacts
- [ ] Build prod OK

## Matrice tokens (aperçu)

| Token actuel | Superieur |
|--------------|-----------|
| `--topbar-gradient-start: #0073b7` | `#3862F5` |
| `--topbar-gradient-end: #00c0ef` | `#2962FF` |
| `--layout-content-bg: neutral-50` | `#f4f6f9` |
| `--layout-sidebar-item-active-bg: primary-50` | `#3862F5` + texte blanc |
| Card radius `--radius-xl: 0.75rem` | `8px` |
