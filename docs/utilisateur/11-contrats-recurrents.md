# 11 - Contrats récurrents / Abonnements B2B

Le module **Contrats récurrents** permet de gérer les abonnements et prestations périodiques : facturation mensuelle, trimestrielle ou annuelle, consommation (usage), et prorata lors des avenants ou résiliations.

Les factures sont générées en **brouillon** : vous les validez manuellement dans l'assistant facture avant signature.

---

## Activer le module

1. Le module **Contrats récurrents** doit figurer sur le plan (back-office) et être accordé à l'utilisateur.
2. L'API exige `Features:RecurringContracts:Enabled` (et `BillingJobEnabled` pour la génération automatique).
3. Le menu **Ventes → Contrats récurrents** apparaît si vous avez la permission `recurring_contracts:read`.

---

## Créer un contrat

1. **Ventes → Contrats récurrents → Nouveau contrat**
2. Choisissez le **client**, la **périodicité** (mensuel, trimestriel, annuel) et le **jour de facturation**.
3. Ajoutez des **lignes** :
   - **Récurrent fixe** : montant fixe à chaque période
   - **À la consommation** : facturation selon l'usage (avec franchise et dépassement)
   - **Frais d'installation** : facturé une seule fois
4. Enregistrez, puis **Activez** le contrat depuis la fiche.

### Depuis un devis accepté

Sur un devis **Accepté**, cliquez sur **Créer un contrat récurrent**. Les lignes et le client sont repris automatiquement.

---

## Cycle de vie

| Statut | Description |
|--------|-------------|
| Brouillon | En préparation, non facturé |
| Actif | Facturation automatique selon l'échéancier |
| Suspendu | Pause temporaire (pas de facturation) |
| Résilié | Fin définitive |
| Expiré | Date de fin atteinte |

Actions disponibles sur la fiche : **Activer**, **Suspendre**, **Reprendre**, **Générer brouillon** (manuel).

---

## Facturation automatique

Un job quotidien (6h30 UTC) scanne les contrats actifs dont la **prochaine date de facturation** est dans la fenêtre configurée (`BillingWindowDays`, par défaut 3 jours).

Pour chaque période :
1. Calcul des montants fixes, usage et prorata
2. Création d'un **brouillon de facture** (`InvoiceDraft`)
3. Enregistrement d'un **billing run** (piste d'audit)

### Valider les brouillons

1. **Ventes → Brouillons récurrents** (ou bouton sur la liste des contrats)
2. Cliquez sur **Valider dans le wizard** pour ouvrir l'assistant facture
3. Vérifiez les lignes, puis signez et envoyez la facture

La facture validée est automatiquement liée au contrat et au billing run.

### Ajuster un brouillon d'échéance

Sur une échéance en statut **Brouillon** (onglet **Échéances**), l'action **Ajuster** permet de modifier la description, la quantité, le prix unitaire HT et le **taux de TVA** (0 / 7 / 13 / 19 %). Le produit et le nombre de lignes restent figés.

À l'enregistrement :
- le brouillon de facture et les montants de l'échéance concernée sont mis à jour ;
- les **lignes du contrat** (onglet Services) et les **montants estimés des autres échéances** ne sont **pas** modifiés.

Pour un changement de tarif permanent sur le contrat, utilisez **Nouvel avenant**.

L'ajustement **n'écrit pas** dans le journal comptable : seule l'**émission** de la facture génère l'écriture de vente. Pour un historique d'avenant formel (prorata calendaire), utilisez **Nouvel avenant**.

---

## Consommation (usage metering)

Pour les lignes **À la consommation** :

1. Définissez des **métriques** (ex. « Utilisateurs actifs », « Go stockés »)
2. Saisissez les quantités sur la fiche contrat (onglet **Consommation**)
3. Lors de la facturation, le système calcule :
   - Quantité incluse (franchise)
   - Dépassement facturé au tarif unitaire

Import CSV : disponible via l'API (`POST /recurring-contracts/{id}/usage-records/import`).

---

## Avenants et prorata

Les **avenants** (upgrade, downgrade, changement de prix, ajout/suppression de ligne) enregistrent l'historique et appliquent un **prorata calendaire** sur la période en cours lors de la prochaine facturation.

Politique : arrondi à 3 décimales, règle AwayFromZero.

---

## Permissions

| Permission | Action |
|------------|--------|
| `recurring_contracts:read` | Consulter contrats et brouillons |
| `recurring_contracts:create` | Créer / convertir depuis devis |
| `recurring_contracts:update` | Modifier un brouillon de contrat |
| `recurring_contracts:delete` | Supprimer un brouillon |
| `recurring_contracts:manage` | Activer, suspendre, résilier, avenants |
| `recurring_contracts:usage` | Saisir la consommation |
| `recurring_contracts:billing` | Déclencher manuellement la facturation |

---

## Produits « Abonnement »

Lors de la création d'un produit de type **Abonnement**, celui-ci est mappé sur `ProductType.Subscription` et peut être utilisé dans les lignes de contrat récurrent.

---

## Dépannage

- **Aucun brouillon généré** : vérifiez que le contrat est **Actif**, que `NextBillingDate` est atteint, et que `BillingJobEnabled` est activé.
- **Doublon de période** : impossible par conception (contrainte unique sur contrat + période).
- **Module invisible** : vérifiez le plan SaaS, les droits utilisateur et le feature flag.
