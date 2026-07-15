# 04 - Achats

Ce chapitre couvre la gestion des achats : fournisseurs, bons de commande et factures fournisseurs.

---

## Fournisseurs

Les **fournisseurs** sont les entreprises ou personnes chez qui vous achetez des marchandises ou des services.

### Liste des fournisseurs

1. Dans le menu, cliquez sur **Achats** puis **Fournisseurs**.
2. La liste de tous vos fournisseurs s'affiche.

![Liste des fournisseurs](../screenshots/04-achats/fournisseurs-liste.png)

### Créer un fournisseur

1. Cliquez sur **Nouveau fournisseur**.
2. Renseignez les informations :
   - Nom (obligatoire)
   - NIF
   - Adresse
   - Téléphone, email
3. Cliquez sur **Enregistrer**.

### Voir une fiche fournisseur

Cliquez sur un fournisseur dans la liste. La fiche affiche ses informations, ses bons de commande et ses factures.

### Modifier un fournisseur

1. Ouvrez la fiche du fournisseur.
2. Cliquez sur **Modifier**.
3. Effectuez vos modifications puis enregistrez.

---

## Bons de commande

Un **bon de commande** (BC) est le document que vous envoyez à un fournisseur pour commander des marchandises.

### Liste des bons de commande

1. Dans le menu, cliquez sur **Achats** puis **Bons de commande**.
2. La liste de tous vos bons de commande s'affiche.

![Liste des bons de commande](../screenshots/04-achats/bc-liste.png)

### Créer un bon de commande

1. Cliquez sur **Nouveau bon de commande**.
2. Sélectionnez le fournisseur.
3. Ajoutez les articles commandés (produits, quantités, prix).
4. Enregistrez.

### États d'un bon de commande

| État | Signification |
|------|---------------|
| **Brouillon** | Le bon de commande n'est pas encore envoyé. Vous pouvez le modifier. |
| **Confirmé** | Le bon de commande a été envoyé au fournisseur. |
| **Réceptionné** | Les marchandises ont été reçues. |

### Réceptionner un bon de commande

Quand vous recevez les marchandises :

1. Ouvrez le bon de commande (état Confirmé).
2. Cliquez sur **Réceptionner**.
3. Vérifiez les quantités reçues et validez.

### Créer une facture fournisseur depuis un bon de commande

Une fois le bon de commande réceptionné, vous pouvez créer la facture fournisseur associée :

1. Ouvrez le bon de commande réceptionné.
2. Cliquez sur **Créer facture fournisseur**.
3. Une fenêtre s'ouvre avec les informations pré-remplies (numéro de facture fournisseur, montant, etc.).
4. Modifiez si besoin (date, conditions de paiement, référence).
5. Cliquez sur **Créer la facture**.

![Modal création facture fournisseur](../screenshots/04-achats/modal-facture-fournisseur.png)

---

## Factures fournisseurs

Les **factures fournisseurs** sont les factures que vos fournisseurs vous envoient pour les achats que vous avez effectués.

### Liste des factures fournisseurs

1. Dans le menu, cliquez sur **Achats** puis **Factures fournisseurs**.
2. La liste de toutes vos factures fournisseurs s'affiche.

### Voir le détail d'une facture fournisseur

Cliquez sur une facture dans la liste. La fiche affiche toutes les informations, le fournisseur associé et le bon de commande lié (si applicable).

---

[Retour à l'index](README.md) | [Précédent : Ventes](03-ventes.md) | [Suivant : Fiches](05-fiches.md)
