# Plan d'amélioration UI — Page « Nouvelle commande client »

> **Périmètre** : route `/sales-orders/new`
> **Composant** : `src/Frontend/factutrust-web/src/app/features/sales-orders/sales-order-create/sales-order-create.component.ts` (477 lignes, template inline, **aucun style**)
> **Objectif** : refonte visuelle complète, **zéro régression** fonctionnelle et zéro modification de la logique métier / des appels API.
> **Date** : 2026-08-01

---

## 1. Diagnostic approfondi (avec preuves)

### 1.1 Cause racine : la page n'a AUCUN style

Le composant utilise 19 classes CSS dans son template, mais :

- Le décorateur `@Component` (lignes 49–261) ne déclare **ni `styleUrls` ni `styles`**.
- Un audit exhaustif du frontend montre que les classes suivantes **ne sont définies nulle part** (ni dans `styles.scss`, ni dans `styles/_design-layer.scss`, ni dans aucun SCSS global) :

| Classe utilisée dans le template | Définie globalement ? | Où elle existe éventuellement |
|---|---|---|
| `ft-panel`, `ft-panel__title` | ❌ | nulle part |
| `ft-form-grid`, `ft-form-grid--inline` | ❌ | uniquement dans `sales-order-detail.component.scss` (l.613) — **scopé à l'autre composant** |
| `ft-field`, `ft-field--full`, `ft-field--grow`, `ft-field--actions` | ❌ | `ft-field` seul existe dans `sales-order-detail.component.scss` (l.588) — scopé, inapplicable ici |
| `ft-required` | ❌ | idem, scopé au détail (l.601) |
| `ft-hint`, `ft-muted`, `ft-empty-inline` | ❌ | `ft-hint` scopé au détail (l.625) ; les deux autres nulle part |
| `ft-table`, `ft-num`, `ft-actions-col`, `ft-inline-input` | ❌ | nulle part |
| `ft-totals`, `ft-totals__row`, `ft-totals__row--grand` | ❌ | nulle part |

> ⚠️ L'encapsulation de vues Angular (Emulated, par défaut) ajoute un attribut `_ngcontent-xxx` par composant : les styles de `sales-order-detail.component.scss` **ne peuvent pas** s'appliquer au composant create. La page s'affiche donc avec les seuls styles PrimeNG de base — ce qui correspond exactement à la capture : page blanche plate, champs minuscules, aucun panneau, aucune hiérarchie.

### 1.2 Inventaire des problèmes visibles sur la capture

| # | Problème | Gravité | Origine |
|---|---|---|---|
| P1 | Aucun panneau/carte : « INFORMATIONS » et « LIGNES » flottent sur fond blanc | 🔴 | `ft-panel` non défini |
| P2 | Champs de saisie minuscules et désalignés (dates ~120 px, client ~150 px) ; 60 % de la largeur inutilisée à droite | 🔴 | `ft-form-grid` non défini → pas de grille |
| P3 | Champs date natifs `<input type="date">` au format US `mm/dd/yyyy`, incohérent avec le reste de l'app qui utilise `p-calendar` au format `dd/mm/yy` (voir `purchase-order-create.component.ts` l.79–97) | 🟠 | choix d'implémentation |
| P4 | Ligne d'ajout produit tassée (Produit / Quantité / Ajouter sur une seule ligne étroite) | 🟠 | `ft-form-grid--inline` non défini |
| P5 | Table des lignes sans style (bordures, en-tête, alignement numérique) | 🟠 | `ft-table`, `ft-num` non définis |
| P6 | Totaux inexistants visuellement (pas de panneau de total, pas de mise en avant du Total HT) | 🟠 | `ft-totals*` non définis |
| P7 | État vide réduit à une ligne de texte grisé, sans icône ni guidance | 🟡 | `ft-empty-inline` non défini |
| P8 | Bouton « Créer la commande » sans état de chargement pendant `saving()` ; risque de double-clic | 🟡 | pas de spinner lié au signal `saving()` |
| P9 | Aucun indicateur visuel pendant la résolution de prix (`resolving()`) ligne par ligne | 🟡 | signal existant mais non exploité visuellement |
| P10 | Sections sans icônes ni numérotation, contrairement au pattern établi `app-form-section` utilisé par `purchase-order-create` et `quote-form` | 🟡 | pattern maison non réutilisé |
| P11 | AutoComplete client/produit non élargi à 100 % de son champ | 🟡 | pas de `styleClass="w-full"` |
| P12 | Responsive non géré (grille, table, totaux sur petit écran) | 🟡 | aucune media query possible sans styles |

### 1.3 Ce qui fonctionne et NE DOIT PAS changer (zone intouchable)

- **Logique de résolution de prix** : `resolvePriceFor()`, `reresolveAll()`, `markOverridden()` (l.335–424) — flux serveur délicat (prix négocié / grille / catalogue).
- **Payload de création** : `submit()` (l.430–476) — mapping exact vers `CreateSalesOrderLineRequest`.
- **Guards de soumission** : `canSubmit` (l.293), règles `addLine()` (l.357).
- **Services injectés** : `SalesOrderService`, `PricingService`, `ClientService`, `ProductService`, `ToastService`, `ErrorHandlerService`.
- **Bindings `[(ngModel)]`, `inputId`, handlers** : identiques, pour préserver l'accessibilité et les éventuels tests.
- **Format des dates** : `orderDate` est une chaîne `YYYY-MM-DD` envoyée telle quelle à `pricingService.resolve()` et `createSalesOrder()` (l.402, 447). Tout changement de widget date doit préserver ce format exact en sortie.

### 1.4 Actifs réutilisables déjà présents dans l'application (aucune nouvelle dépendance)

| Actif | Chemin | Usage prévu |
|---|---|---|
| Tokens de design (`--color-*`, `--spacing-*`, `--radius-*`, `--font-*`, `--shadow-*`) | `src/styles/_superieur-tokens.scss` | base de tout le SCSS à écrire |
| `app-form-section` (titre + icône + numéro, carte avec bordure gauche primaire) | `shared/components/form-section/` | structurer « Informations » et « Lignes » |
| `app-page-header`, `app-breadcrumb`, `app-button` | `shared/components/…` | déjà utilisés, à conserver |
| Pattern table + `.empty-lines` + `.totals-section` | `purchase-order-create.component.ts` l.271–460 | modèle de styles éprouvé dans l'app |
| `p-calendar` avec `dateFormat="dd/mm/yy"`, `[showIcon]` | utilisé dans purchase-order-create | remplacement des dates natives (phase optionnelle, voir risque R2) |
| `p-tag` (déjà importé) | — | badges d'origine de prix, à conserver |
| `app-quick-create-client-dialog`, `app-quick-create-product-dialog` | `shared/components/…` | amélioration UX optionnelle (phase 4, hors refonte visuelle stricte) |

---

## 2. Cible visuelle (vision)

```
┌────────────────────────────────────────────────────────────────┐
│ Fil d'Ariane : Ventes › Commandes clients › Nouvelle           │
│ ┌──────────────────────────────────────────────────────────┐   │
│ │ Nouvelle commande client          [Annuler] [✓ Créer]    │   │
│ │ Sous-titre explicatif (prix serveur…)                    │   │
│ └──────────────────────────────────────────────────────────┘   │
│ ┌─ ① 👤 Informations ────────────────────────────────────┐     │
│ │  Client* (pleine largeur, autocomplete w-full)         │     │
│ │  ┌ Date commande* ┐ ┌ Livraison prévue ┐ ┌ Référence ┐ │     │
│ │  ┌ Conditions de règlement ────────────┐              │     │
│ │  Notes (pleine largeur, 2 lignes)                      │     │
│ └────────────────────────────────────────────────────────┘     │
│ ┌─ ② 🧾 Lignes ─────────────────────────────────────────┐     │
│ │  [Produit ▾ (grow)]  [Qté (120px)]  [+ Ajouter]       │     │
│ │  ┌────────────────────────────────────────────────┐   │     │
│ │  │ # │ Produit │ Qté │ PU HT │ Origine │ Remise │  │   │     │
│ │  │   │         │     │       │ du prix │  %     │… │   │     │
│ │  └────────────────────────────────────────────────┘   │     │
│ │                          ┌──────────────────────┐     │     │
│ │                          │ Total HT  1 234,500  │     │     │
│ │                          │ TND (grand, primaire)│     │     │
│ │                          └──────────────────────┘     │     │
│ └────────────────────────────────────────────────────────┘     │
└────────────────────────────────────────────────────────────────┘
```

Principes : cartes blanches à bordure gauche primaire (cohérence `app-form-section`), grille 12 colonnes fluide, champs pleine largeur, montants en `tabular-nums`, espacements uniquement via tokens `--spacing-*`, responsive ≤1024 px (1 colonne) et ≤640 px (empilement).

---

## 3. Plan d'exécution par phases

### Phase 0 — Préparation & filet de sécurité (0,5 h)

1. Créer une branche dédiée : `git checkout -b feat/sales-order-create-ui`.
2. Capturer des screenshots de référence (état vide, avec lignes, focus, erreur) dans `artifacts/ui-baseline/`.
3. Vérifier que le build de départ passe : `npm run build` (ou `ng build`) dans `src/Frontend/factutrust-web` → noter le résultat dans `build-output.txt`.
4. **Invariant de la refonte** : le fichier `.ts` ne pourra recevoir que 3 types de modifications, listées ci-dessous. Toute autre modification = arrêt et revue :
   - (a) ajout de `styleUrls` dans le décorateur ;
   - (b) réorganisation du template (balisage/classes uniquement) ;
   - (c) imports de modules/composants purement visuels (`FormSectionComponent`, `CalendarModule` si phase 3.3).

### Phase 1 — Styles dédiés (risque quasi nul, gain visuel ~70 %) (2–3 h)

> **Principe : on ne touche PAS au template.** On crée uniquement le fichier de styles. Effet immédiat : les 19 classes orphelines prennent vie.

1. Créer `sales-order-create.component.scss` à côté du `.ts`.
2. Ajouter `styleUrls: ['./sales-order-create.component.scss']` au décorateur (modification de type (a)).
3. Contenu du SCSS — définir, **avec les tokens uniquement**, chaque classe orpheline :

   - `:host { display:block; }` + rythme vertical (`gap: var(--spacing-6)` entre panneaux).
   - `.ft-panel` : carte blanche `var(--color-background-elevated)`, `border: 1px solid var(--color-border-subtle)`, `border-left: 4px solid var(--color-primary-500)`, `border-radius: var(--radius-xl)`, `box-shadow: var(--shadow-soft-sm)`, `padding: var(--card-padding, var(--spacing-5))`.
   - `.ft-panel__title` : `font-size-lg`, `font-weight-semibold`, bordure basse `var(--color-primary-100)`, marge basse `var(--spacing-4)`.
   - `.ft-form-grid` : `display:grid; grid-template-columns: repeat(3, 1fr); gap: var(--spacing-4) var(--spacing-5)` ; media ≤1024 px → 2 colonnes ; ≤640 px → 1 colonne.
   - `.ft-field` : `flex-direction: column; gap: var(--spacing-1)` ; label `font-size-sm / semibold` ; `.ft-field--full { grid-column: 1 / -1; }` ; `.ft-field--grow { flex: 1; }` ; `.ft-field--actions { align-self: end; }`.
   - `.ft-form-grid--inline` : `display: flex; align-items: flex-end; gap: var(--spacing-3); flex-wrap: wrap` (Produit en grow, Quantité fixe 120 px, bouton aligné bas).
   - Champs PrimeNG à pleine largeur : `:host ::ng-deep .ft-field .p-autocomplete, :host ::ng-deep .ft-field .p-inputnumber, :host .ft-field input[pInputText], :host .ft-field textarea { width: 100%; }` — **périmètre strictement limité à `:host`** pour ne jamais fuiter sur d'autres pages.
   - `.ft-required { color: var(--color-error-600); }`, `.ft-hint { font-size-xs; color: var(--color-text-secondary); }`, `.ft-muted { color: var(--color-neutral-600); }`.
   - `.ft-table` : en-tête fond `var(--color-neutral-50)`, `text-transform: uppercase`, `font-size-xs`, lettres espacées ; cellules `padding: var(--spacing-3) var(--spacing-4)` ; `.ft-num { text-align: right; font-variant-numeric: tabular-nums; }` ; `.ft-actions-col { width: 48px; text-align: center; }`.
   - `.ft-inline-input` (inputs dans la table) : largeur bornée (`110px` qté, `130px` PU, `90px` remise) via `::ng-deep` scopé.
   - `.ft-totals` : bloc aligné à droite, `min-width: 280px; max-width: 360px`, fond `var(--color-primary-50)`, bordure `var(--color-primary-100)`, `border-radius: var(--radius-lg)` ; `.ft-totals__row--grand` : `font-size-lg`, `bold`, `color: var(--color-primary-700)`.
   - `.ft-empty-inline` : centré, icône-friendly, `padding: var(--spacing-6)`, couleur `var(--color-text-tertiary)`.
   - Focus visibles : `:focus-visible { outline: 2px solid var(--color-primary-400); outline-offset: 1px; }` sur les contrôles du composant.

4. **Vérification phase 1** : `ng build` sans erreur ; revue visuelle vs baseline ; la page doit déjà ressembler aux autres formulaires de l'app.

### Phase 2 — Structure du template (risque faible, balisage seul) (2–3 h)

> Modifications de type (b) uniquement : aucune expression, aucun binding, aucun `inputId` ne change.

1. Remplacer les deux `<div class="ft-panel">` par `<app-form-section>` :
   - Section ① « Informations » (`icon="pi-user" [number]="1"`) ;
   - Section ② « Lignes » (`icon="pi-list" [number]="2"`).
   - Importer `FormSectionComponent` dans `imports` (modification de type (c)).
   - ⚠️ Conserver les classes `ft-form-grid` etc. à l'intérieur : le SCSS de phase 1 reste valide ; ne pas supprimer le SCSS.
2. Réordonner la grille « Informations » : Client en `ft-field--full` ; puis Date / Livraison / Référence sur une ligne de 3 ; Conditions de règlement ; Notes en full.
3. Élargir les AutoComplete : ajouter `styleClass="w-full"` sur les deux `p-autoComplete` (attribut purement visuel).
4. État vide enrichi (dans le `@if (lines().length === 0)`) : bloc centré avec `<i class="pi pi-inbox">` + texte guidant « Ajoutez au moins un produit pour activer le bouton Créer ». Reprendre le pattern `.empty-lines` de `purchase-order-create.component.ts` l.338–348.
5. Numérotation des lignes : ajouter une colonne `#` (index) — purement affichage.
6. Colonne Produit : code en mono/gris sous le nom (pattern `.line-product` du détail, `sales-order-detail.component.scss` l.339–353).
7. **Vérification phase 2** : build + test manuel du parcours complet (voir §5).

### Phase 3 — Polish UX & cohérence (risque maîtrisé) (2 h)

1. **État de chargement du bouton Créer** : remplacer le `app-button` de soumission par un `p-button` avec `[loading]="saving()"`, **ou** conserver `app-button` et ajouter `[disabled]="!canSubmit() || saving()"` (déjà le cas) + libellé dynamique `{{ saving() ? 'Création…' : 'Créer la commande' }}`. Choisir la 2ᵉ option (aucun import nouveau).
2. **Indicateur de résolution de prix** : petit spinner `<i class="pi pi-spin pi-spinner">` à côté du tag « Origine du prix » quand `resolving()` est vrai — signal déjà existant, aucune logique ajoutée.
3. **Dates (POINT LE PLUS RISQUÉ — voir R2)** — deux options, choix explicite requis :
   - **Option A (recommandée, zéro risque)** : conserver `<input type="date">` mais le styler pleine largeur (déjà fait en phase 1) et accepter le format navigateur.
   - **Option B (cohérence dd/mm/yy)** : migrer vers `p-calendar`. **Obligation** : convertir `Date ↔ string YYYY-MM-DD` via getter/setter dédiés (`orderDateModel` / `expectedDeliveryDateModel`) qui lisent et écrivent les chaînes existantes `orderDate` / `expectedDeliveryDate`. Les chaînes restent la seule source de vérité envoyée à `pricingService.resolve()` (l.402) et à `createSalesOrder()` (l.447–448). Tests unitaires de conversion obligatoires avant merge.
4. Responsive final : vérifier 1920 / 1366 / 768 / 390 px.
5. Accessibilité : contraste des `.ft-hint` (≥ 4,5:1 — utiliser `var(--color-neutral-600)` et non 500), ordre de tabulation, `aria-label` sur le bouton poubelle de ligne (déjà `pTooltip`, ajouter `aria-label="Retirer la ligne"`).

### Phase 4 — Améliorations optionnelles (hors scope initial, à valider) 

- Bouton « + Nouveau client » à côté de l'autocomplete via `app-quick-create-client-dialog` (existant).
- Colonne « Stock disponible » dans la table (donnée déjà côté produit ?).
- Barre d'action bas de page sticky reprenant Total HT + bouton Créer.
- ⚠️ Ces points **changent le comportement** : les traiter dans un ticket séparé, jamais dans cette refonte.

---

## 4. Registre des risques & parades

| ID | Risque | Probabilité | Impact | Parade |
|---|---|---|---|---|
| R1 | Le SCSS du composant fuite sur d'autres pages | Faible | Moyen | `:host` systématique + `::ng-deep` toujours préfixé par `:host` ; encapsulation Emulated conservée (ne jamais passer en `None`) |
| R2 | Migration `p-calendar` casse le format `YYYY-MM-DD` attendu par l'API (pricing + create) | Moyenne | **Élevé** | Option A par défaut ; si Option B : getter/setter de conversion + tests unitaires + test E2E du payload réseau (onglet Network) |
| R3 | Régression du flux de re-tarification au changement de client | Faible | Élevé | Aucune modification des méthodes `onClientChange` / `reresolveAll` autorisée ; test manuel dédié (§5 T4) |
| R4 | `app-form-section` affiche une bordure gauche différente du reste | Faible | Faible | C'est le pattern standard de l'app (purchase-order-create, quote-form) : cohérence voulue |
| R5 | Double soumission pendant `saving()` | Faible | Moyen | Phase 3.1 : disabled + libellé dynamique |
| R6 | Conflit de classes `ft-field` avec le SCSS scopé du détail | Nulle | — | L'encapsulation isole les deux ; les définitions restent indépendantes |
| R7 | Régression responsive sur la table (7 colonnes) | Moyenne | Faible | `overflow-x: auto` sur le conteneur de table dès la phase 1 |

---

## 5. Plan de tests & checklist anti-régression

### 5.1 Tests automatisés
- `ng build` : 0 erreur, 0 warning nouveau.
- `ng lint` (si configuré) sur le composant modifié.
- Si Option B (dates) : tests unitaires du getter/setter de conversion (cas : date valide, vide, minuit UTC — utiliser les composantes locales, **jamais** `toISOString()` qui décale d'un jour en UTC+1).

### 5.2 Matrice de tests manuels (à cocher avant merge)

| # | Scénario | Attendu |
|---|---|---|
| T1 | Arrivée sur `/sales-orders/new` | Sections ①② en cartes, champs pleine largeur, bouton « Créer » désactivé |
| T2 | Sélection client + ajout produit + quantité | Ligne ajoutée, prix résolu serveur, tag « Prix négocié »/« Grille »/« Catalogue » correct, spinner pendant résolution |
| T3 | Saisie manuelle du PU | Tag « Saisi » (warning), prix figé, re-tarification ultérieure sans effet sur cette ligne |
| T4 | Changement de client après ajout de lignes | Les lignes non surchargées sont re-tarifées (comportement actuel préservé) |
| T5 | Remise % + modification quantité | Total ligne et Total HT recalculés, alignés à droite, 3 décimales |
| T6 | Suppression de ligne | Ligne retirée, total mis à jour |
| T7 | Soumission complète | Toast succès « Commande créée en brouillon. », redirection vers `/sales-orders/:id` |
| T8 | Erreur serveur à la création | Toast erreur, formulaire conservé, bouton réactivé |
| T9 | Payload réseau (DevTools → Network) | Corps JSON **strictement identique** à celui d'avant refonte (comparer avec baseline) |
| T10 | Responsive 1366 / 768 / 390 px | Grille 2 → 1 colonne, table scrollable horizontalement, totaux lisibles |
| T11 | Navigation clavier | Tabulation logique, focus visible, `*` requis annoncés |
| T12 | Double-clic sur « Créer » | Une seule requête POST émise |

### 5.3 Critères d'acceptation
1. Aucune ligne de la classe TypeScript métier (hors décorateur/imports/template) modifiée — vérifié par `git diff`.
2. Payload de création identique (T9).
3. Parité visuelle avec `purchase-order-create` (mêmes cartes, mêmes tokens).
4. Zéro régression sur T2–T8.

---

## 6. Estimation & séquencement

| Phase | Contenu | Effort | Risque |
|---|---|---|---|
| 0 | Branche, baseline, build témoin | 0,5 h | — |
| 1 | SCSS dédié (19 classes) | 2–3 h | Quasi nul |
| 2 | Restructuration template + `app-form-section` | 2–3 h | Faible |
| 3 | Polish (loading, spinner prix, dates, a11y) | 2 h | Moyen (R2 uniquement si Option B) |
| — | Tests & revue | 1,5 h | — |
| **Total** | | **8–10 h** | |

**Ordre impératif** : 0 → 1 → (validation) → 2 → (validation) → 3 → tests. Chaque phase est commitable séparément et livrable en l'état (la phase 1 seule apporte déjà ~70 % du gain visuel).

---

## 7. Annexe — Fichiers de référence

- Composant cible : `src/Frontend/factutrust-web/src/app/features/sales-orders/sales-order-create/sales-order-create.component.ts`
- Modèle de styles (page sœur) : `…/purchase-orders/purchase-order-create/purchase-order-create.component.ts` (template l.43–170, styles l.271–460)
- Modèle de carte détail : `…/sales-orders/sales-order-detail/sales-order-detail.component.scss`
- Composants partagés : `src/app/shared/components/form-section/`, `page-header/`, `button/`, `breadcrumb/`
- Tokens : `src/styles/_superieur-tokens.scss`, couche design `src/styles/_design-layer.scss`
