# FAQ — Erreurs console (permissions, HTTP 200 vs code métier)

Ce guide aide à distinguer les **erreurs produites par l’application FactuTrust** des **erreurs injectées par le navigateur ou des extensions**, et évite d’ouvrir des tickets backend ou de modifier le code sans preuve.

## Symptômes fréquents (souvent non liés à FactuTrust)

- `Uncaught (in promise)` avec un objet contenant :
  - `httpStatus: 200`, `httpError: false`, mais `code: 403` et `message: "permission error"` dans le corps métier
  - `reqInfo` avec `pathPrefix` / `path` (ex. `/site_integration/template_list`, `/writing/get_template_list`)
- Une pile d’appels mentionnant **`chrome-extension://.../background.js`** (ex. `handleRes`)

Ces chemins **ne figurent pas** dans le code source de `factutrust-web`. L’API FactuTrust expose typiquement des routes sous **`/api/...`** (voir les contrôleurs ASP.NET Core).

## Signatures connues (extensions navigateur — NON corrigeables côté app)

Observées notamment sur la page **Assistant IA** (`/ai-assistant`), mais présentes sur toutes les pages
(elles se déclenchent à chaque changement d’URL, pas seulement sur l’IA). Vérifié : **0 occurrence**
de ces chaînes dans `src/Frontend/factutrust-web/src/**`.

| Message console | Source | Origine |
|---|---|---|
| `[MindStudio][Messaging] Runtime message failed: {type:'launcher/current_url_updated', …}` | `content.js` | Extension **MindStudio** (content script) |
| `[Auth] Failed to get auth status:` + `message port closed` / `Receiving end does not exist` | `panelState-*.js` | Extension navigateur (content script ↔ service worker suspendu) |
| `Uncaught (in promise) Error: Could not establish connection. Receiving end does not exist.` | `ai-assistant:1` (URL de la page) | Content script ↔ service worker d’extension suspendu |
| `Unchecked runtime.lastError: Could not establish connection. Receiving end does not exist.` | — | API `chrome.runtime` d’extension (émis par Chrome) |
| `Uncaught TypeError: Cannot read properties of undefined (reading 'toLowerCase')` | `keyboard.ts-*.js` | Extension (gestionnaire de mots de passe / assistant clavier) — **aucun** `keyboard.ts` dans `factutrust-web` |

**Preuve de non-régression (2026-09-05)** : `http://localhost:4200/auth/register` chargée dans un
navigateur **sans extensions** ne produit que `[vite] connected` et
`Angular is running in development mode` — **zéro erreur**. Les 18 erreurs / 48 warnings visibles
sur le profil Chrome de développement proviennent donc des extensions installées, pas de
l'application. Refaire cette vérification (profil vierge) **avant** d'ouvrir un ticket sur une
erreur console de cette page.

Indices fiables que c’est une extension : présence de `chrome.runtime` / `runtime.lastError`,
préfixe `[MindStudio]`, type `launcher/current_url_updated`, pile mentionnant `content.js`,
`keyboard.ts-*.js` ou `chrome-extension://…`. Extensions vues dans les captures : **MindStudio, MaxAI, Cookie-Editor**,
traducteur, gestionnaire de mots de passe.

Pour isoler ce bruit sur `/auth/login` : navigation privée **sans** extensions, ou désactiver
le gestionnaire de mots de passe pour `localhost:4200`, ou activer DevTools **« Hide messages from extensions »**.

### Warnings de polices

| Message | Origine | Statut |
|---|---|---|
| `[Intervention] Slow network is detected… Fallback font will be used while loading: <URL>` | Chargement d’une police distante sous réseau lent | **Réglé** — Inter est désormais auto-hébergée |
| `Failed to decode downloaded font: <URL>` / `OTS parsing error: Failed to convert WOFF 2.0 font to SFNT` | Police woff2 distante échouée/altérée (Inter CDN, ou une des 6 familles pluto) | **Réglé** — Inter **et** les familles pluto (Poppins/Raleway/Roboto) sont désormais auto-hébergées |

**Inter** n’est plus chargée via le CDN Google Fonts : elle est auto-hébergée
(`src/assets/fonts/inter/`, `@font-face` dans `styles.scss`, sous-ensembles latin + latin-ext,
typeface byte-identique à l’ancien CDN). Cela supprime la dépendance réseau externe **et** améliore
le temps de premier rendu. NB : PrimeNG (thème lara) fournit déjà sa propre famille `"Inter var"`
(distincte de `"Inter"`) — ce n’est donc pas un doublon.

**Thème pluto (audit Axe 4) — RÉGLÉ (auto-hébergement)** : `src/assets/theme/pluto/style.css`
`@import`ait **6 familles Google Fonts** (Source Sans Pro, Raleway, Poppins, Roboto, Arvo, Rajdhani),
chargées globalement via angular.json donc présentes sur **toutes** les pages (y compris
`/ai-assistant`) bien qu’inutiles hors pages publiques. Ces `@import` distants ont été **supprimés**
et remplacés par de l’auto-hébergement (comme pour Inter) :

- **Poppins, Raleway, Roboto** — les seules familles réellement référencées — sont auto-hébergées
  via `@font-face` dans `src/styles.scss` (woff2 sous `src/assets/fonts/{poppins,raleway,roboto}/`,
  sous-ensembles **latin + latin-ext**, `font-display: swap`). Audit d’usage : Poppins
  (200/300/400/500/600/700, sans italique) pour pluto ; Raleway (600) pour pluto ; Roboto
  (300/400/500/700 + italiques 400/500/700) pour le thème **chain** de la page d’accueil — qui ne
  déclarait pas son propre import et dépendait du `@import` Roboto global de pluto.
- **Source Sans Pro, Arvo, Rajdhani** — importées mais **jamais** référencées par une règle
  `font-family` → simplement supprimées (zéro impact visuel).

Résultat : **aucune** requête `fonts.googleapis.com` / `fonts.gstatic.com` ne subsiste, et les
warnings « slow network / OTS / decode font » liés au CDN disparaissent. Si un warning de police
persiste, identifier l’URL exacte via l’onglet Network (filtre `font` / `woff2`, colonnes Status +
Initiator).

### Filtre console dev (confort, optionnel)

En build **développement** uniquement, un filtre (`src/app/core/utils/dev-console-noise-filter.ts`,
branché dans `main.ts` sous `isDevMode()`) neutralise :

- le **rejet de promesse** d’extension `Uncaught (in promise) … Could not establish connection. Receiving end does not exist.` (via `window.addEventListener('unhandledrejection')` + `preventDefault` sur la **chaîne exacte**) ;
- les logs **`[Auth] Failed to get auth status`** et **`message port closed`** émis via `console.warn` / `console.error` par des scripts d’extension (`panelState-*.js`) ;
- les **`TypeError … toLowerCase`** dont la pile ou le fichier source mentionne **`keyboard.ts`** (via `unhandledrejection`, `window.error`, et les wrappers console).

Il ne touche **ni la production, ni les intercepteurs HTTP**. Les wrappers `console.warn` / `console.error` ne filtrent que des signatures documentées d’extensions ; les logs applicatifs réels restent affichés. Le reste du bruit d’extension —
logs `[MindStudio]` (via `console.error`), `Unchecked runtime.lastError` (émis par Chrome), rejets
du « monde isolé » des content scripts — n’est pas interceptable depuis la page → utiliser le toggle
DevTools **« Hide messages from extensions »**.

### Page connexion — `InvalidStateError` / View Transitions

Un `Uncaught (in promise) InvalidStateError: Transition was aborted because of invalid state`
pouvait apparaître sur `/auth/login` lorsqu’une session expirée déclenchait `logout()` →
`navigate(['/auth/login'])` alors que la page était déjà affichée, en combinaison avec
`withViewTransitions()`. Correctif applicatif : `invalidateSession()` (clear sans POST inutiles)
et navigation vers `/auth/login` **uniquement** si l’URL courante n’est pas déjà sous `/auth/*`.

### Warning Angular `NG0956` sur `/auth/login`

Le template login (`features/auth/login`) **n’utilise aucun `@for`**. Un warning
`NG0956 … collection of size 6` observé sur cette URL est donc en général :
- un résidu d’une navigation précédente (sidebar / rapports), ou
- émis par une dépendance / extension, pas par le formulaire de connexion.

Avant de « corriger » le tracking sidebar (`track item.label`), **ouvrir la pile** du warning
dans DevTools : ne modifier le code métier que si la pile pointe clairement vers un composant
FactuTrust. Sinon : ignorer (perf warning non bloquant) ou « Hide messages from extensions ».

## Étape 1 — Valider dans l’onglet Network (≈ 5 min)

1. Ouvrir DevTools (F12) → onglet **Network** (Réseau).
2. Reproduire l’action qui affiche l’erreur en console.
3. Filtrer la liste : `template_list`, `site_integration`, `writing`, ou le nom d’hôte suspect.
4. Cliquer une requête concernée et vérifier :
   - **URL complète** : domaine d’un tiers ou service inconnu vs `https://localhost:7001/api/...` (ou votre URL API configurée).
   - Colonne **Initiator** (Initiateur) : script de page FactuTrust vs **extension**.

Si l’initiator pointe vers une **extension** ou l’URL ne cible **pas** l’API FactuTrust, le problème **n’est pas** corrigé dans ce dépôt.

## Étape 2 — Isoler l’extension (≈ 10 min)

1. Fermer les onglets inutiles ; ne garder que `http://localhost:4200/` (ou votre port).
2. Ouvrir une **fenêtre de navigation privée** avec **extensions désactivées**  
   (Chrome : ne pas autoriser les extensions en navigation privée pour le profil utilisé, ou utiliser un profil vierge).
3. Charger l’application : si les erreurs **disparaissent**, la cause est **externe** (extension ou autre injecteur).
4. Réactiver les extensions **une par une** et recharger jusqu’à voir les erreurs réapparaître : l’extension ainsi identifiée est la source.

Option : dans la **console**, activer **« Masquer les messages des extensions »** (libellé variable selon la version de Chrome) pour réduire le bruit pendant le débogage de l’app.

## Étape 3 — Corriger côté poste (sans toucher au code)

- Mettre à jour l’extension vers la dernière version.
- Se connecter au compte attendu par le service de l’extension si l’erreur reflète un **vrai** manque de droit côté SaaS tiers.
- Désinstaller l’extension ou la **désactiver pour les origines locales** si l’UI le permet.
- À défaut, utiliser un profil Chrome dédié au développement FactuTrust **sans** extensions non essentielles.

## Quand ouvrir un ticket applicatif FactuTrust

Ouvrir un ticket (avec capture Network) **uniquement si** :

- La requête part vers **votre** API (ex. `https://localhost:7001/api/...`) **et**
- Vous observez un comportement incorrect (401/403/500, ou corps `ApiResponse` avec `success: false` selon le cas).

Les erreurs HTTP côté SPA sont traitées par les intercepteurs Angular (voir `src/Frontend/factutrust-web/src/app/core/interceptors/error.interceptor.ts`) à partir de **`HttpErrorResponse`** (statuts HTTP d’erreur réels).

## Cabinet comptable — import facture et assistant IA comptabilité

En mode **délégué** (dossier client ouvert), les utilisateurs cabinet reçoivent `ai:chat` et le module `AppModule.AI` si `Features:AccountingFirms:AiAccountingEnabled` est `true` (défaut). Cela active :

- l’import IA de factures externes depuis **Saisie manuelle** (`/accounting/manual-entry`) ;
- l’assistant expert **Comptabilité** (`/ai-assistant/comptabilite`) uniquement ;
- l’export PowerPoint depuis cet assistant (même permission `ai:chat` ; isolation tenant = dossier client).

**Après déploiement** : les sessions cabinet doivent être rafraîchies (reconnexion ou changement de dossier) pour obtenir le nouveau JWT.

Si l’import affiche *« nécessite l'assistant IA, pour lequel vous n'avez pas d'autorisation »* :

1. Vérifier que l’utilisateur est bien en mode délégué dans un dossier client (pas sur `/firm/*`).
2. Vérifier `Features:AccountingFirms:AiAccountingEnabled` côté API.
3. Vérifier que le token contient la permission `ai:chat` (claim JWT).

### Assistant IA : « Génération interrompue » + « Une erreur est survenue » en dossier client

Symptôme typique : le bandeau **Modèle prêt** s’affiche, mais tout message (y compris « salut » ou **Analyser avec l’assistant IA**) échoue immédiatement avec *Génération interrompue* et un message générique.

**Diagnostic Network (DevTools)** :

1. Filtrer `POST /api/ai/chat`.
2. Si le statut est **403** avec un corps `ApiResponse` du type *« Modification interdite en mode dossier client… »*, le middleware `DelegatedAccessMiddleware` bloque encore les écritures IA — vérifier que le déploiement inclut l’autorisation du préfixe `/api/ai`.
3. Si le statut est **401**, rafraîchir la session (reconnexion ou changement de dossier).
4. Si le statut est **200** avec `Content-Type: text/event-stream` mais pas de contenu, investiguer Ollama / modèle cloud (voir sections ci-dessous).

**Correctifs côté utilisateur** :

- Quitter le dossier puis le rouvrir (ou se reconnecter) pour obtenir un JWT délégué avec `ai:chat`.
- Vérifier `Features:AccountingFirms:AiAccountingEnabled` côté API.

### Erreur « does not support chat » (ex. nomic-embed-text)

Le modèle d'import plateforme configuré dans le backoffice (**Configuration IA → Modèle d'import**) est un modèle **d'embedding**, pas un modèle **instruct/chat**. Ollama refuse l'appel.

**Correctif** :

1. Backoffice → Configuration IA → choisir un modèle instruct (ex. `qwen2.5:7b-instruct`) ou vider le champ (repli sur `Ollama:InvoiceImportModel` dans appsettings).
2. Depuis la version durcie : le runtime ignore automatiquement un modèle embedding plateforme et retombe sur appsettings — mais corriger la config évite les logs d'avertissement.

### Erreur « données exploitables » / « n'a pas pu structurer cette pièce »

L'appel IA a abouti mais le JSON renvoyé par le modèle n'a pas pu être analysé. Fréquent sur **photos de factures** lorsque le chemin texte/OCR est utilisé sans modèle vision.

**Correctif** :

1. Vérifier `Ollama:InvoiceImportVisionModel` dans appsettings (ex. `gemma3:4b`) et que le modèle est installé (`ollama list`).
2. S'assurer que `InvoiceImportVisionOnImages` est `true` (défaut) pour forcer la vision sur les fichiers image.
3. Backoffice → Configuration IA → modèle d'import instruct (`qwen2.5:7b-instruct`), pas d'embedding.
4. Consulter `GET /api/accounting/document-import/capabilities` : `visionModelReady` doit être `true` pour les imports photo.

### Inscription — jetons en console (corrigé, ne pas réintroduire)

`AuthService.register()` a longtemps journalisé la réponse complète du serveur
(`console.log('[AuthService] Réponse reçue:', response)`), c'est-à-dire **accessToken,
refreshToken et le profil utilisateur en clair** dans la console — visibles sur n'importe quelle
capture d'écran de support. Ce log a été supprimé et un test le verrouille
(`auth.service.spec.ts` : « never writes the registration response to the console »).

Règle : **aucune réponse d'authentification ne doit être passée à `console.*` ni à un puits de
télémétrie.** Pour déboguer une inscription, utiliser l'onglet **Network** (la réponse y est
lisible ponctuellement) plutôt qu'un log persistant dans le code.

### Inscription — avertissements après création d'espace

Le bandeau de fin d'inscription affiche les avertissements renvoyés par l'API
(`AuthResponseDto.WarningDetails`, code + message + sévérité). Un message du type
« Le segment sélectionné est « Association », mais votre NIF indique la catégorie … »
(`NIF_SEGMENT_MISMATCH`) **n'est pas** un échec d'activation de module : l'espace et les modules
sont bien créés. Sur la sémantique — non validée fiscalement — de la lettre de catégorie du NIF,
voir `docs/fiscal/nif-taxpayer-category.md` (et le kill-switch
`Features:RegistrationSector:NifSegmentCoherenceWarningEnabled`).

## Politique « pas de régression » (important)

- **Ne pas** modifier les intercepteurs HTTP pour « faire taire » des erreurs **émises par des extensions** : cela n’a pas d’effet sur ces promesses et risque de masquer de vrais problèmes sur l’API.
- **Ne pas** ajouter d’intercepteur « 200 + code métier → erreur »** sans** preuve Network montrant que **l’API FactuTrust** utilise ce contrat sur des routes données ; un tel changement doit être limité aux routes concernées, revu en équipe et couvert par des tests.

## Référence rapide — enveloppe API FactuTrust

Le backend utilise `ApiResponse<T>` (`src/Backend/FactuTrust.Application/DTOs/CommonDtos.cs`) avec `Success`, `Message`, `Code`, `Errors`. Les échecs sont en principe renvoyés avec les **codes HTTP appropriés** (`BadRequest`, `Unauthorized`, `NotFound`, etc.) selon les contrôleurs — pas avec le schéma `code` / `msg` numérique observé sur certaines API tierces.

---

*Document aligné sur la procédure de diagnostic interne (erreurs console type extension vs application).*
