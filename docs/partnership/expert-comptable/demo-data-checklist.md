# Checklist — Tenant démo partenariat expert-comptable

Ce tenant sert à enregistrer la vidéo `factutrust-partenariat-expert-comptable.mp4` et à reproduire la démo devant un cabinet comptable.

## Compte et modules

| Élément | Requis | Vérification |
|---------|--------|--------------|
| Email / mot de passe démo | Oui | Variables `DOC_EMAIL` / `DOC_PASSWORD` |
| Modules activés | Comptabilité, Fiscal/TEJ, Trésorerie, Ventes, Achats, Administration | Sidebar complète visible |
| Entrepôt actif | Au moins 1 | Connexion → dashboard sans erreur |
| Rôle admin pour l'enregistrement | Oui | Accès Paramètres > Utilisateurs |

## Données métier minimales

### Ventes & comptabilité automatique

- [ ] **3+ factures validées** (statut Validée / Payée) sur l'exercice courant
- [ ] Écritures **JV — Ventes** visibles dans `/accounting/journal` (filtre JV)
- [ ] Au moins **1 facture** cliquable depuis `/invoices` avec signature électronique affichée

### Achats

- [ ] **1+ facture fournisseur** validée
- [ ] Écritures **JA — Achats** visibles dans le journal

### Trésorerie

- [ ] **Paiements clients** enregistrés (`/payments/clients`) — liste non vide
- [ ] **Compte bancaire** configuré (`/payments/bank-accounts`)

### Comptabilité (module SCE)

- [ ] Plan comptable peuplé (`/accounting/chart`)
- [ ] Balance générale avec lignes (`/accounting/balance`)
- [ ] Bilan et compte de résultat sur l'exercice courant
- [ ] Déclaration TVA préremplie pour un mois récent
- [ ] Périodes comptables listées (`/accounting/closing`) — pour export FEC

### Fiscal TEJ

- [ ] Retenues à la source sur au moins **1 paiement fournisseur**
- [ ] Export TEJ (`/withholding-tax/tej-export`) — prévisualisation XML non vide

### Gouvernance

- [ ] **Utilisateur secondaire** rôle **Comptable** visible dans `/settings/users`
- [ ] Journal d'audit avec entrées (`/audit`)

## Données fictives recommandées

Utiliser des données **fictives** uniquement :

- Société : « Demo Cabinet Partenaire SARL »
- NIF : format valide fictif `1234567/A/B/C/000`
- Clients / fournisseurs : noms génériques (Client Alpha, Fournisseur Beta)
- Emails : `@example.tn` ou `@demo.factutrust.local`

## Préparation rapide (si tenant vide)

1. **Paramètres > Entreprise** : renseigner raison sociale, NIF, régime fiscal
2. **Fiches > Clients** : créer 2 clients
3. **Fiches > Produits** : créer 3 produits avec TVA 19 %
4. **Ventes > Factures > Nouvelle facture** : émettre 3 factures et les **valider**
5. **Achats > Fournisseurs** + **Factures fournisseurs** : 1 cycle achat complet
6. **Trésorerie > Paiements clients** : enregistrer 1 paiement sur facture
7. **Comptabilité > Journal** : vérifier écritures JV/JA auto-générées
8. **Fiscal/TEJ** : configurer retenue sur paiement fournisseur si module actif
9. **Paramètres > Utilisateurs** : ajouter utilisateur « Marie Comptable » rôle Comptable

## Variables d'environnement (enregistrement vidéo)

```powershell
$env:DOC_EMAIL="demo@cabinet-partenaire.tn"
$env:DOC_PASSWORD="VotreMotDePasseDemo"
# Optionnel si plusieurs entrepôts :
# $env:DOC_WAREHOUSE="Nom entrepôt principal"
```

## Commandes pipeline vidéo

```powershell
# 1. Démarrer backend + frontend (voir docs/scripts/README.md)

# 2. Enregistrer le screencast (8 chapitres)
cd src/Frontend/factutrust-web
$env:DOC_EMAIL="..."; $env:DOC_PASSWORD="..."
npm run docs:partnership-video

# 3. Générer la voix off TTS
..\..\..\docs\partnership\scripts\generate-tts.ps1

# 4. Assembler la vidéo finale
..\..\..\docs\partnership\scripts\assemble-video.ps1
```

## Fichiers produits

| Fichier | Description |
|---------|-------------|
| `raw/chapitre-01.webm` … `chapitre-08.webm` | Screencasts Playwright par chapitre |
| `audio/chapitre-01.mp3` … `chapitre-08.mp3` | Narration TTS |
| `factutrust-partenariat-expert-comptable.mp4` | Vidéo finale |
