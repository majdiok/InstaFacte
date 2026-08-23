# 03 - Ventes

Ce chapitre couvre tout ce qui concerne vos ventes : devis, bons de livraison, bons de retour et factures.

---

## Devis

Un **devis** est une proposition de prix envoyée à un client avant la vente. Si le client accepte, vous pouvez le transformer en facture.

### Liste des devis

1. Dans le menu, cliquez sur **Ventes** puis **Devis**.
2. La liste de tous vos devis s'affiche.

![Liste des devis](../screenshots/03-ventes/devis-liste.png)

### Créer un devis

1. Cliquez sur **Nouveau devis**.
2. Renseignez les informations : client, articles, quantités, prix.
3. Enregistrez le devis.

### Modifier un devis

1. Cliquez sur un devis dans la liste.
2. Cliquez sur **Modifier**.
3. Effectuez vos modifications puis enregistrez.

### Convertir un devis en facture

Une fois le devis accepté par le client, vous pouvez le convertir en facture directement depuis la fiche du devis.

---

## Bons de livraison

Un **bon de livraison** (BL) atteste qu'un client a bien reçu des marchandises. Il peut être lié à une facture.

### Liste des bons de livraison

1. Dans le menu, cliquez sur **Ventes** puis **Bon de Livraison**.
2. La liste de tous vos bons de livraison s'affiche.

![Liste des bons de livraison](../screenshots/03-ventes/bl-liste.png)

### Créer un bon de livraison

1. Cliquez sur **Nouveau bon de livraison**.
2. Sélectionnez le client et les articles livrés.
3. Enregistrez.

### Voir le détail d'un bon de livraison

Cliquez sur un bon de livraison dans la liste. La fiche affiche toutes les informations et, si applicable, le lien vers la facture associée.

La fiche indique aussi les quantités **retournées** et **à facturer**. Si tout a été retourné, le bouton **Générer facture** disparaît.

---

## Bons de retour

Un **bon de retour** (préfixe **BRT**) sert à enregistrer un retour client **après livraison et avant facture**. Il réintègre le stock au même dépôt que le bon de livraison et **diminue la quantité facturable** du BL.

Ce n'est **pas** un avoir :

| | Bon de retour | Avoir de vente |
|---|---|---|
| Quand ? | Articles livrés, **pas encore facturés** | **Après** une facture émise |
| Effet | Stock + réduction de la qté à facturer | Document fiscal (TVA, comptabilité) |
| Numéro | `BRT-AAAA-NNNNNN` | `AVO-…` |

### Créer un bon de retour

1. Dans le menu, cliquez sur **Ventes** puis **Bon de retour**, ou ouvrez le BL livré et cliquez sur **Créer un bon de retour**.
2. Choisissez le bon de livraison éligible (livré ou partiellement livré, non facturé, avec un restant).
3. Indiquez la date, un **motif** (obligatoire) et les quantités à retourner (bornées au restant).
4. Enregistrez le **brouillon** : le stock ne bouge pas encore.
5. Ouvrez le brouillon et cliquez sur **Confirmer**. Le stock est réintégré et le restant à facturer du BL diminue.

Vous pouvez créer **plusieurs** bons de retour sur le même BL, tant qu'il reste une quantité livrée non facturée.

### Après confirmation

- La facture générée depuis le BL ne porte que le **reliquat**.
- Si tout est retourné, la facture est refusée et le bouton **Générer facture** est masqué.
- Un bon de retour confirmé n'est plus modifiable.

---

## Factures

Les **factures** sont les documents officiels que vous envoyez à vos clients pour exiger le paiement.

### Liste des factures

1. Dans le menu, cliquez sur **Ventes** puis **Factures**.
2. La liste de toutes vos factures s'affiche.

![Liste des factures](../screenshots/03-ventes/factures-liste.png)

Vous pouvez filtrer par :
- **Statut** : Brouillon, Émise, Payée, etc.
- **Date**
- **Client**

### Créer une facture (assistant en 6 étapes)

1. Cliquez sur **Nouvelle facture**.
2. Un **assistant** vous guide à travers 6 étapes. Suivez-les dans l'ordre.

#### Étape 1 : Type & Date

![Assistant facture - Étape 1](../screenshots/03-ventes/facture-etape1.png)

- Choisissez le **type de document** (Facture, Facture proforma, etc.).
- Saisissez la **date** de la facture.
- Sélectionnez la **devise** (TND par défaut).

Cliquez sur **Suivant**.

#### Étape 2 : Émetteur

![Assistant facture - Étape 2](../screenshots/03-ventes/facture-etape2.png)

Vérifiez ou complétez les **informations du vendeur** (votre entreprise) : nom, adresse, NIF, etc.

Cliquez sur **Suivant**.

#### Étape 3 : Client

![Assistant facture - Étape 3](../screenshots/03-ventes/facture-etape3.png)

- **Recherchez** un client existant en tapant son nom ou son code.
- Ou **ajoutez un nouveau client** en saisissant ses informations.

Cliquez sur **Suivant**.

#### Étape 4 : Articles

![Assistant facture - Étape 4](../screenshots/03-ventes/facture-etape4.png)

Ajoutez les **lignes de facturation** :
- Produit ou description
- Quantité
- Prix unitaire
- TVA si applicable

Vous pouvez ajouter plusieurs lignes. Le total se calcule automatiquement.

Cliquez sur **Suivant**.

#### Étape 5 : Paiement

![Assistant facture - Étape 5](../screenshots/03-ventes/facture-etape5.png)

- Indiquez les **conditions de paiement** (à réception, 30 jours, etc.).
- Ajoutez les **mentions légales** si nécessaire.

Cliquez sur **Suivant**.

#### Étape 6 : Validation

![Assistant facture - Étape 6](../screenshots/03-ventes/facture-etape6.png)

- Consultez l'**aperçu** de la facture.
- Cliquez sur **Émettre la facture** pour la valider définitivement.
- Ou **Enregistrer en brouillon** pour terminer plus tard.

> **Astuce** : Vous pouvez revenir en arrière à tout moment avec le bouton **Précédent** pour modifier une étape.

### Voir le détail d'une facture

Cliquez sur une facture dans la liste. La fiche affiche toutes les informations, le statut de paiement et les actions possibles (télécharger, envoyer, créer un avoir).

### Créer un avoir (facture d'avoir)

Un **avoir** annule ou réduit une facture déjà émise (retour de marchandise **après facture**, erreur, etc.). Pour un retour **avant facture**, utilisez un **bon de retour**, pas un avoir.

1. Ouvrez la facture concernée.
2. Cliquez sur **Créer un avoir**.
3. L'assistant s'ouvre avec les données pré-remplies. Ajustez les montants ou lignes si besoin.
4. Validez l'avoir.

---

## Recette manuelle — bon de retour

À jouer après déploiement (anti-régression) :

1. BL livré, stock sorti → créer un BRT **partiel** en brouillon : stock **inchangé**, BL encore facturable pour le restant.
2. Confirmer → stock **+qté** au **même dépôt** que le BL, mouvement « Retour Client ».
3. Générer la facture depuis le BL → lignes = **restant uniquement** ; valider la facture **ne redéduit pas** le stock.
4. Second BRT pour le reliquat → facture ensuite refusée ; bouton **Générer facture** masqué.
5. Tenter un BRT sur un BL déjà facturé → erreur.
6. Créer un **avoir** sur une autre facture classique → inchangé (stock + écriture).
7. BL issu d'une commande : « reste à livrer » **inchangé** ; « livré non facturé » **diminue** du retour. Pas de BL complémentaire du seul fait du retour.
8. Impression PDF BRT + liens croisés BL ↔ BRT.
9. Utilisateur sans `return_notes:read` : menu absent, route bloquée.
10. Redémarrage API : migration tenant appliquée, pas de `TENANT_MIGRATION_FAILED`.

---

[Retour à l'index](README.md) | [Précédent : Tableau de bord](02-tableau-de-bord.md) | [Suivant : Achats](04-achats.md)
