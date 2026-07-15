# Façades GLB — Rue FactuTrust (visite virtuelle)

## Unités et orientation

- **1 unité Three.js = 1 mètre** (échelle métrique).
- La **façade commerciale** regarde l’axe **+Z** (visiteur côté +Z de la scène ; le runtime parente la vitrine et applique `lookAt` sur l’arc de rue).
- **Hiérarchie export (contrat gelé)**  
  - **`FaçadePack`** (racine `gltf.scene`) contient exactement deux enfants nommés :  
    - **`StorefrontRoot`** : détail complet (enseigne, vitrine, volumes).  
    - **`StorefrontRoot_LOD1`** : silhouette simplifiée pour `THREE.LOD` quand la carte comporte **≥ 10** vitrines et GLB est activé.  
  - **Rétrocompatibilité** : si le fichier n’a qu’une racine unique nommée `StorefrontRoot` (anciens exports), le runtime traite toute la scène comme niveau haute qualité et conserve le repli boîte pour le LOD distant.

## Contrat dimensions par `FacadeTheme` (référence placeholders générés)

Les valeurs ci-dessous sont celles utilisées par `npm run virtual-street:build-glbs` ; les exports DCC finaux doivent **rester comparables** (même ordre de grandeur) pour ne pas casser l’arc de rue ni le cadrage caméra.

| Enum | Fichier | Largeur (m) | Hauteur (m) | Profondeur (m) | Style visuel cible |
|------|---------|-------------|-------------|----------------|---------------------|
| 0 Classic | `classic.glb` | 3.36 | 4.73 | 2.57 | Pilastres + corniche |
| 1 Modern | `modern.glb` | 3.10 | 4.05 | 2.37 | Façade flush, store-narrow |
| 2 Vintage | `vintage.glb` | 3.48 | 4.85 | 2.64 | Auvent plus profond |
| 3 Minimal | `minimal.glb` | 3.02 | 3.82 | 2.30 | Sans pilastres |
| 4 Artisan | `artisan.glb` | 3.28 | 4.50 | 2.49 | Tons chauds / matériaux bois |

Les dimensions procédurales runtime (`facade-theme-presets.ts`) suivent le même facteur **×1,14** sur largeur / hauteur / profondeur / enseigne pour cohérence avec les GLB générés.

## Budget cible

- **Triangles** : viser &lt; 8k triangles par vitrine sur `StorefrontRoot` ; **&lt; 800** sur `StorefrontRoot_LOD1`.
- **Poids fichier** : cible &lt; 800 Ko gzip par thème après compression (meshopt/Draco) une fois le pipeline art stabilisé.

## Noms obligatoires (branding runtime)

| Objet (name) | Rôle | Statut |
|--------------|------|--------|
| `Sign_Plane` | Mesh recevant la texture d’enseigne (nom / marque) — peut être plan ou box tant que le nom est exact. | requis |
| `Logo_Plane` | Mesh pour texture logo (URL whitelist API). | requis |
| `Trim` | Socle / bandeau. | requis |
| `FacadeBody` | Corps principal (teinte / ombres). | requis |
| `Mullions` | Grille de menuiserie de la vitrine. | requis depuis v5 |
| `Door_Leaf` | Vantail de porte ; `userData.openable=true` honoré par animations futures. | requis depuis v5 |
| `Awning_Canopy` | Auvent (ciblé par `tickAwningSway`). | requis depuis v5 |
| `Lantern_Housing` | Boîtier de la lanterne. | requis depuis v5 |
| `Door_Frame`, `Door_Handle` | Détails de la porte. | optionnel |
| `Awning_Brackets` | Équerres sous l'auvent. | optionnel |
| `Planter_Left`, `Planter_Right` | Jardinières (`Foliage_*` enfants). | optionnel |
| `Address_Plate` | Plaque numéro de rue. | optionnel |
| `Interior_Recess` | Fond de vitrine (3 plans inclinés). | optionnel |
| `Window_Prop_Anchor_Left`, `Window_Prop_Anchor_Right` | `Object3D` vides où le runtime greffe des props. | optionnel |
| `Roof_Style` | Variante de toit par thème. | optionnel |
| `Lantern_Bulb_Anchor` | `Object3D` (pas un Mesh) — point d'attache d'une PointLight. | optionnel |

Le validateur (`scripts/validate-facade-glbs.mjs`) **fail** si l'un des « requis depuis v5 » est absent. Il vérifie aussi le poids (≤ 110 Ko par fichier, ≤ 550 Ko cumulés), les budgets triangles (≤ 7 800 / ≤ 780 LOD1), et qu'aucune image n'est embedded (les textures restent externes — voir `assets/virtual-street/textures/` régénéré par `npm run virtual-street:build-textures`).

## Fichiers par `FacadeTheme` (enum backend)

| Valeur | Fichier |
|--------|---------|
| 0 Classic | `classic.glb` |
| 1 Modern | `modern.glb` |
| 2 Vintage | `vintage.glb` |
| 3 Minimal | `minimal.glb` |
| 4 Artisan | `artisan.glb` |

## Diagnostic visuel (pourquoi la vitrine ressemble encore à un « bloc »)

Utiliser cette liste **dans l’ordre** avant d’ouvrir un ticket code ou art.

1. **GLB bien chargé ?** Onglet Réseau : `GET /assets/virtual-street/facades/<theme>.glb?v=<version>` doit répondre **200** (voir `facadeThemeToFilename` / `storefrontGltfFacadesVersion` dans l’environnement Angular).
2. **Flag GLB** : `storefrontGltfFacades` doit être `true` dans l’`environment` utilisé par le build (`ng serve` = dev).
3. **Cache navigateur** : après remplacement des binaires, incrémenter `storefrontGltfFacadesVersion` puis recharger forcé (Ctrl+F5).
4. **Console** : erreur `GLTFLoader` / CORS / 404 → le runtime **garde le procédural** ; corriger l’URL ou l’asset avant de juger le rendu GLB.
5. **Nombre de vitrines** : avec une seule entreprise publiée (`n === 1`), la rue est volontairement centrée sur un module — tester avec **≥ 2** vitrines pour l’effet « rue ».
6. **LOD distant** : le niveau simplifié (`StorefrontRoot_LOD1` ou repli boîte) n’apparaît qu’avec **≥ 10** vitrines ; de près, c’est toujours le niveau détail.
7. **Qualité mesh** : les placeholders générés en CI restent des **approximations** ; le réalisme « jeu » vient surtout d’**exports DCC** (normal / roughness / AO baked, détails d’architecture). Le script Node `generate-facade-glbs.mjs` évite les `roughnessMap` embarqués (limitation `GLTFExporter` sans `document`) ; les exports Blender/Unity n’ont pas cette contrainte.

### Logs de diagnostic (dev uniquement)

Avec `storefrontGltfDebugLog: true` dans `src/environments/environment.ts` (développement seulement), la console trace succès / échec du swap GLB par vitrine (voir `street-scene.runtime.ts` dans le dossier `3d/` de la visite virtuelle).

## Génération locale & CI

- Placeholders détaillés + LOD1 : `npm run virtual-street:build-glbs` (`scripts/generate-facade-glbs.mjs`).
- Validation glTF : `npm run validate:facade-glb`.

## Draco (optionnel, même origine)

1. `npm run vendor:draco` — copie les décodeurs depuis `three/examples/jsm/libs/draco/gltf/` vers `src/assets/vendor/draco/gltf/`.
2. Activer `storefrontGltfDraco: true` dans l’environnement Angular **uniquement** si les GLB exportés utilisent la compression Draco.
3. Ne pas élargir la CSP : les WASM/JS restent servis depuis `/assets/...` (self).

## Meshopt (optionnel)

Si les exports utilisent `EXT_mesh_gpu_compression`, activer **`storefrontGltfMeshopt: true`** dans l’environnement Angular : le runtime enregistre alors `MeshoptDecoder` via `GLTFLoader.setMeshoptDecoder` (module `three/examples/jsm/libs/meshopt_decoder.module.js`, bundlé). Tester impérativement sur **Safari iOS** avant production.

## Extensions glTF

- **Autorisé** : matériaux PBR cœur, textures embarquées.
- **À éviter** : extensions non supportées par le `GLTFLoader` web ; valider avec `npm run validate:facade-glb`.

## Checklist non-régression (roll-out vitrine GLB)

- [ ] `userData.pickSlug` sur le groupe parent (wrapper) : clic / survol sur chaque vitrine.
- [ ] Logo API : uniquement origine whitelist ; après swap GLB, logo visible si URL valide.
- [ ] Échec réseau / GLB manquant : repli procédural conservé.
- [ ] `prefers-reduced-motion` : navigation liste accessible inchangée.
- [ ] Carte avec **≥ 2** vitrines + carte **≥ 10** vitrines (LOD) : pas de crash WebGL, pas de fuites mémoire évidentes après navigation.
- [ ] Bump `storefrontGltfFacadesVersion` à chaque remplacement binaire des GLB (actuellement **4** après agrandissement module + GLB).

### Couverture tests automatisés (non-régression minimale)

- Karma : `street-facade-pack-utils.spec.ts`, `street-facade-gltf-urls.spec.ts`, `street-facade-gltf.spec.ts` (URL versionnée).
- Playwright : `e2e/playwright.virtual-street.spec.ts` (HTTP 200 sur les 5 GLB, liste accessible, canvas tabindex hors mouvement réduit).

Remplacez les GLB placeholders par des exports DCC finaux en conservant les **noms** et la hiérarchie `FaçadePack` / `StorefrontRoot` / `StorefrontRoot_LOD1` lorsque le LOD artistique est requis.
