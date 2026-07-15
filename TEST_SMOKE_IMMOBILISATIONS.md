# Smoke test — Immobilisations (FactuTrust)

Prérequis : backend (`https://localhost:7001/swagger`) et frontend (`http://localhost:4200`) démarrés, tenant avec module **Comptabilité** actif.

```powershell
# Terminal 1 — API
cd src/Backend/FactuTrust.API
dotnet run

# Terminal 2 — Web
cd src/Frontend/factutrust-web
npm start
```

Flags (optionnel) :
- Backend : `Features:FixedAssets:Enabled: true` dans `appsettings.json`
- Frontend : `fixedAssetsEnabled: true` (défaut) via `localStorage` clé `ft.accounting.featureFlags`

---

## A. Parcours manuel (registre)

| # | Action | Résultat attendu |
|---|--------|------------------|
| 1 | Menu **Comptabilité → Immobilisations** | Liste (filtres statut / catégorie / exercice, totaux, pagination) |
| 2 | **Nouvelle immobilisation** (`/accounting/fixed-assets/new`) : onglets **Généralités** (désignation, catégorie, méthode, taux/durée synchronisés, comptes) et **Acquisition** (date, fournisseur, prix HT, TVA, frais, valeur résiduelle) | Formulaire affiché (page non vide), brouillon créé (`IMMO-AAAA-NNNN`) |
| 3 | Brouillon : modifier un champ → **Enregistrer les modifications** | PUT accepté, fiche mise à jour |
| 4 | Brouillon : **Simuler le tableau** avec une date hypothétique | Tableau norme tunisienne (Année / Base / Annuité / Annuités cumulées / VNC + TOTAL), rien n'est persisté |
| 4 bis | Ouvrir un brouillon existant (ex. IMMO-2026-0001) | Statut affiché **« Brouillon »** (pas `—`), type d'amortissement **pré-sélectionné** (ex. Linéaire), bandeau info brouillon visible, section **« Mise en service »** avec bouton primary en bas de fiche |
| 5 | **Mettre en service** (compte crédit `404`) — après enregistrement préalable du brouillon | Succès sans **409** ni **400 INVALID_STATE** (pas de message SqlServerRetryingExecutionStrategy) ; statut « En service », écriture **JIM** générée, **tableau généré automatiquement** |
| 6 | Vérifier le tableau (format norme + détail CP17) | 1ère année au prorata **jours/360**, dernière année solde la base |
| 7 | **Export Excel** | `.xlsx` avec feuille « Tableau amortissement » (format officiel) + feuille « Détail CP17 » |
| 8 | **Dotations** (`/accounting/fixed-assets/depreciation-run`) → comptabiliser exercice courant | Dotations postées, écritures **681 / 281** |
| 9 | **Export Excel dotations** | Rapport exercice téléchargé |
| 10 | (Optionnel) **Cession** avec prix + compte trésorerie `5321` | Statut « Cédé », dotation prorata de l'année de sortie, écritures **675 / 781** |

**Journal** : filtrer code **JIM** — vérifier équilibre débit/crédit sur chaque pièce.

### A bis. Méthodes d'amortissement

| # | Action | Résultat attendu |
|---|--------|------------------|
| 1 | Créer un actif **Accéléré** (coefficient 1,5 ou 2 — matériel industriel multi-équipes) | Annuités constantes sur base constante (taux linéaire × coefficient), prorata jours/360 1ʳᵉ année, total = base |
| 2 | Créer un actif **Intégral** (base ≤ 200 DT recommandée — Décret 2008-492 art. 4) | Bandeau de suggestion si base ≤ 200 DT ; une seule annuité = base amortissable complète sur l'exercice de mise en service |
| 3 | Surcharger taux ou durée (ex. durée 10 ans → taux 10 %) | Champs synchronisés (taux = 100/durée), plan calculé sur la valeur surchargée |
| 4 | Catégorie non amortissable (Terrains) | Méthode/taux désactivés, aucune ligne de tableau |
| 5 | Référentiel taux (catégories) | ≥ 40 rubriques (16 initiales + Décret 2008-492 : grosses réparations, transport, hôtellerie, agriculture…) |

---

## B. Parcours achats → immo (V2)

| # | Action | Résultat attendu |
|---|--------|------------------|
| 1 | Créer BC fournisseur, confirmer, **réceptionner** les lignes | Statut Reçu / Partiellement reçu |
| 2 | **Créer facture fournisseur** : cocher **Immo** sur une ligne + catégorie taux | FF créée |
| 3 | Ouvrir **Factures fournisseurs** → liste | Badge **Immo** sur la ligne |
| 4 | Détail FF → onglet lignes | Badge Immobilisation, compte 21x, lien registre si brouillon créé |
| 5 | **Journal** (JA) sur la FF | Débit **21x** + **43662**, crédit **4011** ; marchandises restent **607 + 43666** |
| 6 | **Immobilisations** → ouvrir brouillon lié | Lien « Voir la facture source » vers la FF |
| 7 | **Mettre en service** le brouillon issu de la FF | Statut « En service », **aucune écriture d'acquisition JIM supplémentaire** (le 21x est déjà au journal JA — pas de double comptabilisation), tableau généré |

---

## C. Contrôles comptables rapides

- **TVA** : déclaration TVA → ligne TVA déductible immobilisations (somme débits **43662** sur la période).
- **FEC** : export inclut journal **JIM** si écritures immo présentes.
- **Feature flag off** : `Features:FixedAssets:Enabled: false` → API `/api/accounting/fixed-assets/*` retourne **503** ; entrée menu masquée si `fixedAssetsEnabled: false`.

---

## D. Tests automatisés (CI locale)

```powershell
cd src/Backend
dotnet test tests/FactuTrust.Infrastructure.Tests/FactuTrust.Infrastructure.Tests.csproj --filter "FullyQualifiedName~FixedAsset|FullyQualifiedName~DepreciationEngine|FullyQualifiedName~SupplierInvoiceJournal|FullyQualifiedName~CreateFixedAssetsFromSupplierInvoice"

cd src/Frontend/factutrust-web
npm run test -- --no-watch --browsers=ChromeHeadless --include=**/accounting/**/*.spec.ts
```

---

## E. Dépannage

| Symptôme | Piste |
|----------|-------|
| Menu Immobilisations absent | `fixedAssetsEnabled` dans `localStorage` ; permissions `accounting:read` |
| 503 sur API immo | `Features:FixedAssets:Enabled` backend |
| Pas de brouillon après FF | Ligne non cochée Immo à la création ; vérifier handler après migration tenant |
| Tableau vide | Immo non mise en service ou taux = 0 % (catégorie non amortissable) |
| Écriture FF sans 43662 | Ligne non classée immo ; regénérer FF ou corriger classification |