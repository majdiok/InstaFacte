# Scripts de capture pour la documentation

## Capture automatique des screenshots (Playwright)

Le script de capture se trouve dans `src/Frontend/factutrust-web/e2e/capture-screenshots.spec.ts`.

### Important : captures complètes vs partielles

- **Sans** `DOC_EMAIL` et `DOC_PASSWORD` : seules les captures du **chapitre 01 (Premiers pas)** sont générées (5 images : landing, connexion, inscription x3). Les tests 6 à 18 sont ignorés.
- **Avec** `DOC_EMAIL` et `DOC_PASSWORD` : **toutes** les captures (chapitres 01 à 09) sont générées. La documentation affichera sans icônes cassées.

Si les captures des chapitres 02 à 09 sont absentes, la documentation affichera des images cassées dans ces chapitres.

### Prérequis

1. **Backend** démarré sur `https://localhost:7001` (ou `http://localhost:7000`)
2. **Frontend** démarré sur `http://localhost:4200` (ou laissé au script via `webServer`)
3. **Compte de test** créé (pour les captures des pages protégées, tests 6-18). Idéalement avec au moins un client, un produit et une facture pour des captures réalistes.

### Exécution

Depuis **src/Frontend/factutrust-web** :

```powershell
# Pages publiques uniquement (chapitre 01 - 5 captures)
npm run docs:screenshots

# TOUTES les captures (chapitres 01 à 09 - 18+ captures) - OBLIGATOIRE pour une doc complète
# Windows (PowerShell)
$env:DOC_EMAIL="votre@email.com"
$env:DOC_PASSWORD="VotreMotDePasse"
npm run docs:screenshots
```

Windows (CMD) :

```cmd
set DOC_EMAIL=votre@email.com
set DOC_PASSWORD=VotreMotDePasse
npm run docs:screenshots
```

Linux/Mac :

```bash
export DOC_EMAIL=votre@email.com
export DOC_PASSWORD=VotreMotDePasse
npm run docs:screenshots
```

Après la capture, relancez `ng build` ou `ng serve` pour que les nouveaux assets soient pris en compte dans l'application.

### Emplacement des screenshots

Les captures sont sauvegardées dans `docs/screenshots/` avec la structure :

- `01-premiers-pas/` : landing, connexion, inscription (étapes 1, 2, 3)
- `02-tableau-de-bord/` : dashboard
- `03-ventes/` : factures, devis, bons de livraison
- `04-achats/` : fournisseurs, bons de commande
- `05-fiches/` : clients, produits
- `06-stock/` : gestion du stock
- `07-paiements/` : vue paiements
- `08-rapports/` : rapports ventes
- `09-parametres/` : profil

---

## Capture manuelle (alternative)

Si le script automatique ne fonctionne pas, vous pouvez prendre les captures manuellement. Suivez cette checklist :

| Fichier | URL | Action |
|---------|-----|--------|
| landing.png | http://localhost:4200/ | Page d'accueil |
| connexion.png | http://localhost:4200/auth/login | Formulaire de connexion |
| inscription-etape1.png | http://localhost:4200/auth/register | Étape 1 (informations de connexion) |
| inscription-etape2.png | http://localhost:4200/auth/register | Remplir étape 1, cliquer Suivant |
| inscription-etape3.png | http://localhost:4200/auth/register | Remplir étapes 1-2, cliquer Suivant |
| dashboard-complet.png | http://localhost:4200/dashboard | Après connexion |
| factures-liste.png | http://localhost:4200/invoices | Liste des factures |
| devis-liste.png | http://localhost:4200/quotes | Liste des devis |
| bl-liste.png | http://localhost:4200/delivery-notes | Liste des bons de livraison |
| facture-etape1.png | http://localhost:4200/invoices/new | Assistant nouvelle facture |
| fournisseurs-liste.png | http://localhost:4200/suppliers | Liste des fournisseurs |
| bc-liste.png | http://localhost:4200/purchase-orders | Liste des bons de commande |
| clients-liste.png | http://localhost:4200/clients | Liste des clients |
| produits-liste.png | http://localhost:4200/products | Liste des produits |
| stock-gestion.png | http://localhost:4200/stock | Gestion du stock |
| paiements-vue.png | http://localhost:4200/payments | Vue paiements |
| rapports-ventes.png | http://localhost:4200/reports/sales | Rapports ventes |
| profil.png | http://localhost:4200/settings/profile | Paramètres profil |

Enregistrez chaque capture dans le dossier correspondant sous `docs/screenshots/`.
