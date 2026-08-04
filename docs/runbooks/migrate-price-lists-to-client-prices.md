# Migration : grilles tarifaires vers prix negocies par client

## Contexte

Le module **Grilles tarifaires** (`PriceList`) est deprecie au profit des **prix negocies par client** (`ClientProductPrice`), geres depuis la fiche produit.

Cette migration convertit les donnees existantes sans perte de tarification.

## Pre-requis

- Sauvegarde de la base tenant avant execution.
- Application deployee avec l'endpoint `GET /api/pricing/products/{productId}`.
- Fenetre de maintenance recommandee (operation rapide, mais modifie les affectations client).

## Procedure

1. Executer le script SQL idempotent sur **chaque base tenant** :

   [`sql/MigratePriceListsToClientProductPrices_Tenant.idempotent.sql`](sql/MigratePriceListsToClientProductPrices_Tenant.idempotent.sql)

2. Verifier les resultats :

```sql
-- Aucun client ne doit plus avoir de grille affectee
SELECT COUNT(*) AS ClientsWithPriceList FROM Clients WHERE PriceListId IS NOT NULL;

-- Les prix migres doivent etre visibles
SELECT COUNT(*) AS NegotiatedPrices FROM ClientProductPrices;

-- Les grilles doivent etre desactivees (donnees conservees)
SELECT COUNT(*) AS ActivePriceLists FROM PriceLists WHERE IsActive = 1;
```

3. Valider dans l'UI :
   - Ouvrir une fiche produit concernee : section **Tarifs par client** remplie.
   - Creer un devis/facture pour un client migre : prix correct via le resolver.

4. Deployer la version frontend qui masque **Ventes > Grilles tarifaires**.

## Rollback

Il n'existe pas de rollback automatique. En cas d'erreur :

- Restaurer la sauvegarde tenant.
- Les tables `PriceLists` / `PriceListItems` ne sont pas supprimees : une restauration partielle est possible si necessaire.

## Notes

- Les **paliers quantitatifs** (`PriceListItemTiers`) ne sont pas migres : seul le prix de base de la grille est copie.
- Un prix negocie existant pour le meme couple client/produit n'est jamais ecrase (contrainte d'unicite).
