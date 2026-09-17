# Visite virtuelle réaliste — contrats L0 figés

Statut : **L0 — contrats purs uniquement**. Aucun chargeur, résolveur de taxonomie ou
catalogue de production n'est activé. Ces types et validateurs sont la référence commune
des lots L1 (moteur/chargeur), L2 (référence publique consentie) et L3 (couverture)
du plan approuvé `/code/.plans/v3-realistic-business-3d.md` (Volet I §§3–9, tâche 1).

Fichiers livrés (tous nouveaux, frontend uniquement) :

| Fichier | Rôle |
|---|---|
| `WEB/src/app/features/virtual-street/3d/business-scene-contracts.ts` | Types readonly + constantes figées |
| `WEB/src/app/features/virtual-street/3d/business-scene-contract-validation.ts` | Validateurs purs sur données inconnues (aucune dépendance Angular/Three/HTTP) |
| `WEB/src/app/features/virtual-street/3d/business-scene-contracts.spec.ts` | Tests Jasmine des contrats et validateurs |
| `WEB/src/app/features/virtual-street/testing/business-taxonomy-fixture.ts` | Base taxonomique synthétique 36 + 6 + 24 + 8 |
| `WEB/src/app/features/virtual-street/testing/business-taxonomy-fixture.spec.ts` | Parité de la base avec la source domaine + règle de couverture |

La porte de build `WEB/scripts/validate-street-catalog.mjs` (script parent, hors
périmètre L0) applique les mêmes règles cœur ; le validateur TypeScript est le
sur-ensemble normatif strict (chemins avec `\`, complétude des recettes par thème,
kinds de scènes, cycles de dépendances, etc.). Un catalogue refusé ici ne doit
jamais atteindre un renderer.

## 1. Les versions à ne pas confondre (plan §6.1)

| Version / clé | Porteur | Règle figée |
|---|---|---|
| `schemaVersion` du manifeste | Catalogue 3D | Vaut `BUSINESS_CATALOG_SCHEMA_VERSION = 2` ; majeure inconnue = rejet contrôlé |
| `catalogVersion` | Catalogue + `visualProfile.catalogVersion` | Jeton de livraison immuable (convention `v2.<release>`) ; sélectionne une livraison, **ne substitue jamais** une identité acceptée |
| `profileKey` | Référence publique consentie | Clé métier stable ; ne change jamais de sens |
| `editorialRevision` | Référence publique consentie + preuve Master | `r<entier positif>` ; toute transformation substantielle = nouvelle révision à accepter |
| Version de taxonomie effective | Proposition propriétaire / audit interne | Jamais exposée au visiteur |
| `FacadeTheme` (0–4) | DTO/publication + adaptateur legacy | Dimension esthétique orthogonale : Classic=0, Modern=1, Vintage=2, Minimal=3, Artisan=4 ; n'encode aucune activité |
| Version de consentement visuel | Preuve Master **non publique** | Absente des DTO publics et de ces contrats frontend |

**L'acceptation publique lie `profileKey` + `editorialRevision`** (type
`VisualProfileIdentity`), pas `catalogVersion`. Une mise à jour technique de
compression/LOD/matériaux change `catalogVersion` sans toucher l'identité éditoriale
acceptée ; les livraisons N et N−1 conservent les couples acceptés ou produisent un
repli neutre explicite.

## 2. Formats normatifs (validateur pur)

| Champ | Règle | Prédicat |
|---|---|---|
| `profileKey`, `sceneKey`, `recipeKey`, clés de viewpoint/interaction | `^[a-z0-9]+(-[a-z0-9]+)*$`, ≤ 64 | `isContractKey` |
| `assetKey` | `^[0-9A-Za-z][0-9A-Za-z._-]*$`, ≤ 128 (règle de la porte de build) | `isAssetKey` |
| `catalogVersion` | même alphabet, ≤ 64, sans `..` (sert de nom de répertoire) | `isValidCatalogVersionToken` |
| `editorialRevision` | `^r[1-9][0-9]*$` | `isValidEditorialRevision` |
| Nœuds/matériaux GLB | `^[A-Za-z0-9_.-]{1,128}$` (conserve `Sign_Plane`, `StorefrontRoot_LOD1`) | `isNodeName` |
| `sha256` | exactement 64 hex minuscules | `isValidSha256Hex` |
| `path` d'asset | relatif immuable sous `<catalogVersion>/` ; **aucune URI** (`http:`, `data:`, `blob:`…), aucun chemin absolu, aucun `..`, aucun `\`, aucun segment vide | `isValidCatalogAssetPath` |

Les champs inconnus supplémentaires sont **tolérés** (évolution additive) ; les valeurs
inconnues des champs connus sont rejetées. Les types readonly ne sont pas une frontière
de confiance : toute donnée reçue passe par les validateurs avant usage.

## 3. Manifeste (plan §6.2) — points de contrôle

`validateBusinessCatalogManifest(unknown) : ContractViolation[]` ne lève jamais
d'exception ; chaque écart porte un code stable (`manifest.schema-version`,
`asset.path`, `profile.scene-kind`, …) exploitable par les tests et les outils.

- **En-tête** : `schemaVersion = 2`, `catalogVersion` valide, `renderer = { engine: 'three', revision: '161', minContractVersion: 1 }`.
- **Profils** : identité `(profileKey, editorialRevision)` unique ; `representationKind = 'illustrative'` ; `facadeSceneKey` et `interiorSceneKey` obligatoires et typés (`street-facade` / `interior`) ; `localExteriorSceneKey` nul ou scène `local-exterior` (le raccord de rue d'un hall est une entrée, pas la façade complète) ; `coverage` pointe des assets `render` (preuves façade + intérieur) et `attribution`.
- **FacadeTheme orthogonal** : `styleRecipes` contient **exactement** les clés `0..4`, chacune avec `recipeKey`, `materialNames`, `assetKeys` résolubles. Aucun thème ne varie selon le domaine.
- **Scènes** : `sceneKey` unique ; `kind ∈ {street-facade, local-exterior, interior}` ; unités mètres, `+Y` up, façade `+Z`, origine au seuil ; bornes cohérentes (`min ≤ max`) ; variantes `economy` **et** `standard` (pas de tier `enhanced` en L0) respectant les plafonds déclarés `SCENE_VARIANT_BUDGETS` (120 000/300 000 triangles, 100/180 draw calls, 96/192 Mio textures) ; LODs triés par distance croissante.
- **Navigation** : `spawn`/`exit`/`viewpoints` finis ; `walkableBounds`/`collisionBounds` valides ; interactions à rôle fermé `exit | openStorefront | inspectScene`, libellé ≤ 120 caractères, `inspectScene` référence un viewpoint existant. Aucune action n'exécute d'URL, de POST ou de commande métier.
- **Assets** : `assetKey` unique ; chemin/hash/tailles conformes ; `dependencyAssetKeys` résolues, sans auto-dépendance ni cycle ; bloc `gltf` réservé aux GLB, extensions requises limitées à `KHR_draco_mesh_compression`, `EXT_meshopt_compression`, `KHR_texture_basisu` (décodeurs auto-hébergés, activés après pilote) ; lightmaps `texCoord = 1` (r161 : `TEXCOORD_1` → `uv1`, `lightMap.channel = 1`).

## 4. Propriété des ressources (plan §7.2)

`ResourceOwnership` est une union discriminée (`visit-runtime` | `asset-registry` |
`scene-instance` | `scene-scope` avec `generation`) : déclaration de cycle de vie pour
le futur runtime, sans remplacer le registre `street-disposable-registry.ts` existant.
La génération distingue l'annulation logique de l'annulation transport ; un résultat
tardif sans propriétaire est libéré, jamais attaché à une nouvelle scène.
`BusinessAssetCacheKey = (catalogVersion, assetKey, quality)` déduplique les
téléchargements ; aucun intérieur n'est préchargé sans intention explicite.

## 5. Configuration à chaud (plan §9)

`StreetExperienceConfig` (payload futur, endpoint non implémenté ici) :
`schemaVersion = 1`, `mode ∈ {list, legacy, catalog-v2}`, `catalogVersion` nul sauf en
`catalog-v2`, `leaseSeconds ∈ (0, 60]`, `configRevision` non vide. Le bail expire sur
horloge monotone côté client ; son expiration arrête la 3D (repli liste/fiche), jamais
de prolongation sur échec réseau. Le mode `list` ne désactive que la 3D.

## 6. Base taxonomique synthétique (tâche 1)

`business-taxonomy-fixture.ts` gèle ce que la résolution visuelle devra absorber.
Toutes les lignes sont synthétiques ; aucune lecture de base ni de tenant réel.

| Groupe | Lignes | Attente |
|---|---:|---|
| Paires seedées | 36 | Profil spécialisé dédié `syn-<segment>-<domaine>` ; **couverture = façade + intérieur exigés** |
| Segment sans domaine | 6 | Alias du profil seedé `(segment, autre)` ; l'alias affiche mais **ne couvre pas** un domaine |
| Connues non seedées | 24 (produit cartésien 6×10 − 36, vérifié) | Repli neutre de famille (`syn-neutral-boutique|bureau|atelier`), puis file art |
| Cas limites | 8 | Codes custom admin (kebab ≤ 50), codes inactifs, classification partielle/absente → neutre global/famille |

La spec compare la matrice seedée à `SEGMENT_ALLOWED_DOMAINS` / `SEGMENT_OPTIONS` /
`DOMAIN_OPTIONS` (miroir frontend documenté comme copie exacte de
`SectorConfigurationCatalog.cs`) : égalité des 36 liens, des 6 segments et des 10
domaines, ordre compris. Huit paires seedées exigent en outre une scène
`local-exterior` à grande échelle (colonne `facade_locale_grande_echelle` de
`/code/.plans/catalogue-coverage.csv`).

**Règle de couverture** : seuls les 36 profils spécialisés comptent comme couverture ;
chacun exige façade **et** intérieur. Les replis neutres restent de l'affichage et ne
couvrent aucun domaine — y compris l'alias `(segment, autre)` des lignes sans domaine.
Toute classification privée (segment/domaine réel d'un tenant) n'apparaît jamais dans
un DTO public : la résolution privée est une décision serveur (L2), hors de ce fichier.

## 7. Hors périmètre L0 (lots suivants)

Champ `visualProfile` dans les DTO publics et migration Master ; endpoints
propriétaire et `experience-config` ; runtime `business-scene.runtime.ts`, registry,
loader, navigation ; validation de GLB réels et assets pilotes ; activation
production. Le catalogue legacy (5 GLB, budgets historiques, alias de nœuds) reste
inchangé et validé par ses propres outils.
