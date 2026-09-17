# C12 — cinq recettes d'étude, deux coûts géométriques (A1 partiel)

**Blockout incomplet et non approuvé. Aucune release, aucun rendu canonique,
aucune navigation navigateur ni acceptation juridique/artistique.**

Cette tranche dépend du socle A0 et ne remplace pas son registre. Elle ne contient
que l'étude `commerce/textile-habillement` C12, intérieur **6 × 10 × 3,4 m**.
La comparaison principale est désormais **Modern (1), standard**, conformément
à la revue V3 inchangée dans `canonical36.csv`. Les anciennes images Classic du
parent ne sont ni renommées ni réattribuées. Les cinq recettes et deux qualités
ci-dessous sont des **études géométriques/export**, pas une qualification finale
du réalisme, du rendu r161 ou des budgets runtime.

## Reproduire sans réseau (depuis la racine du dépôt)

Prérequis : Blender **4.0.2**, exporteur glTF livré avec ce Blender, Python inclus.
Aucun pip, npm, font, image, HDRI, service externe ou ressource téléchargée.
Le Blender Ubuntu disponible n'inclut pas OpenImageDenoiser : débruitage désactivé,
échantillons CPU bornés, grain possible. L'avertissement de bibliothèque Draco
absente concerne une compression optionnelle **non utilisée**.

```sh
blender -b -t 6 --python-exit-code 1 \
  --python art/virtual-street/source/pilots/c12/build.py -- \
  --run-id c12-modern-standard-review-001 --style 1 --quality standard --samples 32

blender -b -t 2 --python-exit-code 1 \
  --python art/virtual-street/source/pilots/c12/test_scene.py

python3 art/virtual-street/source/pilots/c12/test_provenance.py -v

blender -b -t 2 --python-exit-code 1 \
  --python art/virtual-street/source/pilots/c12/test_variants.py

python3 art/virtual-street/source/production.py validate
python3 -m unittest discover -s art/virtual-street/source/tests -v
python3 art/virtual-street/source/production.py preflight
# Le dernier doit toujours retourner 2 (release bloquée).
```

Un run doit être nouveau : aucune réécriture, aucun chemin de destination libre,
aucune sortie hors `art/virtual-street/build/c12-*` ignoré. `--no-render` permet
une itération géométrie/export, **pas** une livraison de deux images. Les échantillons
acceptés vont de 4 à 64 (défaut 24), 1600 × 1000 px, Cycles CPU, six threads,
seed 12, cinq rebonds, pas de débruitage. Les lumières sont originales mais hors
export : aucune fidélité de lumière Three/r161 n'est démontrée par les PNG.

Sorties de travail : `.blend` éditable, GLB non compressé PBR, deux PNG
`*-facade-offline-study.png` / `*-interior-offline-study.png` et
`study-evidence.json`. Le JSON lie les deux images au même fingerprint des
vertices/triangles/matériaux évalués, aux hashes des scripts, aux octets GLB,
aux versions Blender/exporteur, aux caméras et aux temps effectivement mesurés.
Le commit de checkout est indiqué comme base, pas faussement comme commit des
sources si elles sont encore non committées. Les PNG sont des masters d'étude,
pas des images web finales optimisées à 300 KiB.

## Recettes concrètes, programme commun intégral

`--style 0..4` et `--quality economy|standard` sélectionnent une recette de
`recipes.py` ; défaut **1/standard**. Le nom des sorties inclut style et qualité.
Les valeurs hors domaine sont refusées, sans transformation silencieuse.

| Style | Surfaces PBR temporaires et construction hors passage |
|---|---|
| 0 Classic | Enduit crème, bois brun, bronze ; deux moulures étagées au bandeau |
| 1 Modern | Enduit clair neutre, sol gris, habillage sombre, métal ; joint horizontal fin |
| 2 Vintage | Crème sourd, vert sauge, laiton mat ; deux panneaux encastrés au bandeau |
| 3 Minimal | Tons pierre clairs, métal gris ; corniche fine et joint d'ombre |
| 4 Artisan | Terre cuite, bois brun mat, métal sombre ; douze tasseaux verticaux au bandeau |

Le rôle et le nom des onze matériaux restent adressables/stables. Les recettes
ne sont pas des copies de nouveaux métiers : même pièce, même seuil, mêmes
sept zones, 18 vêtements et leurs 72 parties de cintres, deux portants complets,
table/pliages, alcôve/rideau/miroir, caisse décorative et vitrines/formes abstraites.
Les différences d'architecture sont en hauteur au-dessus de la baie, pas dans
la circulation. Aucun changement d'éclairage par recette, aucune lightmap.

**Economy** conserve tous les objets et les vraies épaisseurs ; les cylindres
passent de 12 à 6 côtés et les biseaux de 2 à 1 segment. Il ne retire ni vêtement,
cintre, zone, issue ni prop. Les silhouettes fines sont plus facettées : cette
perte mesurée de détail doit encore recevoir une revue visuelle humaine. Les
deux tests matriciels construisent réellement les dix variantes, vérifient
leurs triangles GLB et bornes après réimport, et exigent pour chacune une
réduction economy de plus de 20 % des triangles et 15 % des octets GLB face à
son standard. Il s'agit d'un objectif de cette étude, **pas** d'un plafond V4
augmenté ou d'un budget global qualifié.

Chaque combinaison subit aussi quatre corruptions déterministes : table
essentielle retirée de l'export, matériau sans propriétaire, qualité déclarée
incompatible avec ses biseaux, véritable obstruction de sortie correctement
proxyfiée. Le contrôle de programme compte les objets effectivement exportables,
pas un simple drapeau déclarant la couverture. Les sept paramètres invalides
sont testés avant réinitialisation de la scène. La provenance vérifie également
qu'une modification de `recipes.py` empêche la finalisation.

Le ledger mesure triangles, octets GLB, buffers et primitives du binaire ;
**un mesh ou une primitive n'est pas une mesure de draw calls toutes passes**.
Textures/lightmaps, GPU, FPS, mémoire, latence et budgets de scène complète
restent non mesurés. Seule la paire Modern standard de cette tranche est rendue ;
les neuf autres exports ne valent pas neuf revues visuelles ni les 80 captures
runtime requises pour les quatre pilotes.

### Provenance figée avant authoring

`build.py` capture HEAD avant tout import d'authoring. Il réexécute son lanceur
depuis les octets capturés et charge de la même manière `provenance.py`, puis
vérifie que ces octets correspondent à la capture. Tous les Python de `source/`
(y compris les futurs helpers d'export), `registry.py`, les deux entrées
canoniques et leur lock sont copiés sans écrasement dans le
`source-snapshot/` du run. `authoring.py`, `scene.py`, `recipes.py` et `validation.py` sont
importés uniquement depuis cette copie neuve, sans pycache ; le registre lit
également les **entrées copiées**, pas les fichiers de travail pendant le rendu.
Un processus Blender neuf est exigé : un module projet déjà importé est refusé.
La stdlib et Blender/exporteur installés restent des dépendances de toolchain,
pas des créations locales copiées ; leurs versions restent enregistrées.

Juste avant la création exclusive du ledger, `provenance.py` compare HEAD,
contenu **et liste** des entrées originales et de leur snapshot. Une édition,
addition, suppression ou dérive du snapshot fait échouer le run **sans ledger
finalisé**. Les hashes écrits proviennent des octets capturés, jamais d'une
nouvelle lecture tardive présentée comme source exécutée. Un run interrompu
peut laisser des fichiers de travail, mais pas une preuve finalisée.

Les huit tests stdlib utilisent seulement des fixtures synthétiques temporaires :
exécution d'une copie puis édition explicite de l'original avant finalisation,
bootstrap différent, dépendances dont helper d'export, module ajouté/supprimé,
HEAD modifié, snapshot modifié, succès et refus d'écrasement. Aucun `sleep`,
course de timing ou changement des entrées canoniques réelles. Les nouvelles
preuves `validated-v2` restent des études non approuvées ; aucun indice ne
démontre que les images antérieures aient subi une dérive de source.

## Géométrie et provenance

Tout est dessiné localement dans `scene.py` : volumes architecturaux avec
épaisseur, profilés fins, porte physiquement ouverte, portants/cintres/vêtements
extrudés sans marque, table basse et pliages, rideau replié, miroir visuel simulé,
comptoir décoratif. Deux formes d'étalage facettées sur pied complètent les
vitrines : **sans tête, visage, membres, anatomie ni personne médicale**.
Aucun texte/font, image de référence, logo, prix, terminal de paiement,
transaction ou donnée de tenant. Enseigne laissée vierge.

Les matériaux temporaires sont uniquement des constantes Principled PBR
originales (albédo/roughness/metallic/alpha). Vitrage alpha simple, pas de
transmission obligatoire ; miroir teinté métallique **simulé**, pas de capture
secondaire runtime. Pas d'UV lightmap ni de baking : une future lightmap doit
exporter `TEXCOORD_1 → uv1` avec `lightMap.channel = 1` sur **Three r161**.
Aucune montée de moteur et aucune URI libre dans le GLB d'étude.

Repère Blender : X transversal, Y profondeur intérieure, Z haut ; seuil à
`(0, 0, 0)`. L'exporteur convertit vers glTF +Y haut, +Z vers la façade.
Les dimensions 6×10×3,4 sont **intérieures libres** ; murs de 0,20 m, sol et
plafond occupent une enveloppe extérieure plus grande, mesurée séparément.
Aucun redimensionnement au slot de rue legacy n'est effectué. La façade d'étude
n'est pas encore un raccord runtime qualifié.

| Zone | Disposition métrique Blender XY |
|---|---|
| Deux vitrines | Rives avant, plinthes de 1,55 × 0,95 m ; entrée centrale libre |
| Portants latéraux | X ±2,56 m, Y 2,95–6,15 m ; rail à 1,78 m |
| Table basse | 1,30 × 2,40 m, dessus à 0,70 m ; centre Y 4,40 m |
| Boucle piétonne | Axes X ±1,40 m, Y 2,50–6,60 m, sweep libre 1,20 m |
| Essayage | Fond gauche, cloison/rideau ouvert/miroir, approche X −1,50 m |
| Caisse décorative | Fond droit, 1,60 × 0,70 m, dessus à 1,07 m |
| Entrée/sortie | Même seuil dégagé, baie entre montants de 1,60 m ; pas de seconde issue réglementaire revendiquée |

La caméra intérieure est à hauteur piétonne 1,65 m dans l'entrée dégagée, pas
une coupe sans murs. La vue façade et la vue intérieure utilisent **le même
modèle**, sans masquage des murs/plafond/mobilier entre les deux. Collections
`Architecture`/`Props` seules exportées ; `CollisionGuides`, `CameraGuides`,
`BakeSources` et `BrandingAnchors` restent hors GLB. Les éclairages offline ne
sont pas un bake transférable au runtime.

## Contrôles réellement géométriques

`validation.py` lit les meshes **évalués** avec modifiers et matrices monde :
- cotes intérieures calculées depuis murs/sol/plafond, enveloppe totale distincte ;
- mètres, bornes finies non dégénérées, scène non vide, statut non publiable ;
- sept zones présentes avec contrôle de l'emplacement réel de leurs objets ;
- propriété de chaque objet/matériau et absence de données externes ;
- un proxy AABB conservateur par obstacle réel, exactitude proxy/mesh et exclusion
  des guides de l'export ;
- balayages rectangulaires continus **1,20 m** sur tous les segments/virages,
  puis recherche de connexité indépendante sur grille 10 cm avec rayon 0,30 m
  jusqu'au spawn, aux deux côtés de table, à l'essayage et au fond ;
- GLB non vide, signature/longueur/JSON/meshes, aucune URI libre ni guide exporté ;
- test d'export **et réimport Blender dans une scène vide**, comparaison des bornes
  évaluées de chaque mesh avec celles de l'original.

Le build et ce test utilisent le même petit helper `export_study_glb` : sélection
seule, modifiers appliqués, +Y haut, caméras/lumières/extras exclus. Les obstacles
sont toujours détectés géométriquement ; aucun flag déclaratif ne les désactive.

Tests négatifs : déplacement réel d'un mur, unité centimétrique, matériau sans
provenance, zone absente, proxy décalé, vraie obstruction du seuil correctement
proxyfiée et export vide. Un résultat vert prouve seulement cette géométrie
statique et l'export de travail. Ce n'est ni un solveur de collisions runtime,
une conformité d'accessibilité bâtiment, une mesure FPS/GPU/draw calls, ni une
preuve du parcours rue–fiche–intérieur dans le navigateur.

## Gates conservés

Les droits de ces nouvelles sources restent **à revoir**, sans ajout artificiel
`reviewed` au registre `licenses.csv`. La provenance locale est déclarée ; elle
ne vaut pas décision juridique. Aucun `.blend` sous le chemin final attendu,
aucune spécification release débloquée et aucune copie sous assets Angular.

Les quatre pilotes C12/C17/C19/C05 doivent toujours recevoir une revue humaine
commune **avant les 32 restants**. Le besoin des huit grands extérieurs demeure.
Cette étude n'ajoute pas une composition au quota et n'est pas A1 complet.
Prochaines étapes de C12 : critique humaine métrique/artistique, reprise des
formes/matières, revue visuelle finale des deux qualités/cinq styles, fidélité r161, droits et budgets,
parcours applicatif réel et images canoniques seulement après ces contrôles.
