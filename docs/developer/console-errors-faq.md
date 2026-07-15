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
| `Uncaught (in promise) Error: Could not establish connection. Receiving end does not exist.` | `ai-assistant:1` (URL de la page) | Content script ↔ service worker d’extension suspendu |
| `Unchecked runtime.lastError: Could not establish connection. Receiving end does not exist.` | — | API `chrome.runtime` d’extension (émis par Chrome) |

Indices fiables que c’est une extension : présence de `chrome.runtime` / `runtime.lastError`,
préfixe `[MindStudio]`, type `launcher/current_url_updated`, pile mentionnant `content.js` ou
`chrome-extension://…`. Extensions vues dans les captures : **MindStudio, MaxAI, Cookie-Editor**,
traducteur, gestionnaire de mots de passe.

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
branché dans `main.ts` sous `isDevMode()`) neutralise le **rejet de promesse** d’extension
`Uncaught (in promise) … Could not establish connection. Receiving end does not exist.` (via
`window.addEventListener(‘unhandledrejection’)` + `preventDefault` sur la **chaîne exacte**). Il ne
touche **ni la production, ni les intercepteurs HTTP, ni `console.error`/`console.warn`** (donc
l’attribution des sources d’erreurs dans DevTools reste correcte). Le reste du bruit d’extension —
logs `[MindStudio]` (via `console.error`), `Unchecked runtime.lastError` (émis par Chrome), rejets
du « monde isolé » des content scripts — n’est pas interceptable depuis la page → utiliser le toggle
DevTools **« Hide messages from extensions »**.

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

## Politique « pas de régression » (important)

- **Ne pas** modifier les intercepteurs HTTP pour « faire taire » des erreurs **émises par des extensions** : cela n’a pas d’effet sur ces promesses et risque de masquer de vrais problèmes sur l’API.
- **Ne pas** ajouter d’intercepteur « 200 + code métier → erreur »** sans** preuve Network montrant que **l’API FactuTrust** utilise ce contrat sur des routes données ; un tel changement doit être limité aux routes concernées, revu en équipe et couvert par des tests.

## Référence rapide — enveloppe API FactuTrust

Le backend utilise `ApiResponse<T>` (`src/Backend/FactuTrust.Application/DTOs/CommonDtos.cs`) avec `Success`, `Message`, `Code`, `Errors`. Les échecs sont en principe renvoyés avec les **codes HTTP appropriés** (`BadRequest`, `Unauthorized`, `NotFound`, etc.) selon les contrôleurs — pas avec le schéma `code` / `msg` numérique observé sur certaines API tierces.

---

*Document aligné sur la procédure de diagnostic interne (erreurs console type extension vs application).*
