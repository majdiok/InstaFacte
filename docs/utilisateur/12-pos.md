# 12 - Point de vente (POS)

Le **point de vente** sert à encaisser au comptoir. Chaque vente crée une **facture** FactuTrust (comme le wizard bureau), puis éventuellement un **paiement**. Ce n’est pas une caisse fiscale tunisienne certifiée.

Pour ouvrir le POS : menu **Point de vente**, ou l’URL `/pos`. Un **entrepôt** (magasin) doit être sélectionné.

---

## Ouvrir la caisse

Avant de vendre, ouvrez une **vacation** (session de caisse) :

1. Cliquez sur **Ouvrir la caisse** dans l’en-tête.
2. Si le magasin a **plusieurs caisses**, choisissez la caisse.
3. Saisissez le **fond de caisse** déclaré (espèces). Ce montant n’écrit pas d’opération de trésorerie.
4. Cliquez sur **Ouvrir**.

Le fond de caisse et le comptage de clôture sont des **déclarations**, pas des écritures comptables.

L’identifiant affiché du ticket (`POS-…`) n’est **pas** l’identifiant de la vacation.

---

## Vente comptant (client de passage)

1. Laissez le client **Passager** (ou re-sélectionnez-le).
2. Ajoutez des articles (recherche, scan, quantité décimale possible, ex. 0,5 kg).
3. Choisissez **Comptant** et le mode (espèces, carte, virement, chèque, **effet**).
4. Validez. Pour les espèces, le calculateur de rendu s’affiche.
5. Le stock sort **uniquement** à la validation de la facture (lots FEFO).

Le bouton e-mail envoie la facture **après** validation, pas à la place.

---

## Client identifié, tarifs et à terme

Recherchez le client par **nom, NIF, e-mail ou code CLI-…**.

- Les **paliers tarifaires** se recalculent quand la quantité change.
- Une bannière affiche l’**encours**. Si le plafond est dépassé, une confirmation **Continuer quand même** s’affiche : la vente n’est **pas** bloquée.
- **À terme** : client nommé obligatoire. Échéance = délai client (30 jours par défaut). Aucun encaissement à la caisse ; la facture reste due. Interdit pour le passager.

Le paiement **fractionné** (espèces + carte, etc.) s’enregistre en une seule opération.

---

## Avoir (retour)

1. Cliquez sur l’action avoir / remboursement.
2. Recherchez la facture d’origine par **numéro**, **nom de client** ou **référence POS** (`POS-…`).
3. Ajustez les lignes et validez. Le stock est réintégré via l’avoir, comme au bureau.

Ne créez **pas** de bon de livraison depuis le POS : cela ferait sortir le stock deux fois.

---

## Mettre en attente

**Mettre en attente** sauvegarde le ticket sur la **caisse courante**. Rappelez-le depuis le panneau des tickets en attente.

Changer de magasin avec un panier non vide demande confirmation (le panier est lié au magasin).

---

## Rapport X et clôture Z

- **Rapport X** : totaux de la vacation **ouverte**, sans la clôturer.
- **Clôture Z** : saisissez le comptage espèces. L’écart s’affiche. La Z est un **instantané** ; elle n’écrit pas une deuxième fois le compte caisse 5411.
- Une **deuxième clôture** de la même vacation est refusée.
- Une vente **à terme** n’augmente pas le théorique espèces (seuls les paiements espèces comptent).

Dans **Historiques**, la puce **Cette session** filtre les factures du jour liées à la vacation. La section **Clôtures Z (30 jours)** liste les Z du magasin / de la caisse.

Les tickets encore en attente **avertissent** à la clôture mais ne la bloquent pas.

---

## Plusieurs caisses

Un magasin (entrepôt) peut avoir **plusieurs caisses**. Une seule est **défaut** : c’est celle utilisée si aucune caisse n’est choisie (comportement des enseignes à une seule caisse).

Création : **Paramètres → Entrepôts → Caisses POS**.

Chaque caisse a sa propre vacation, son panier et ses tickets en attente. Les ventes d’une caisse n’apparaissent pas sur la Z d’une autre.

---

## Verrouillage inactivité

Après une période d’inactivité, saisissez **votre mot de passe** utilisateur pour reprendre. Il n’y a pas de code PIN générique.

---

## Facturation bureau

Le wizard **Ventes → Factures → Nouvelle facture** (`/invoices/new`) fonctionne **sans** ouvrir une caisse POS. N’ouvrez le POS que pour le comptoir.

---

## Limites

- Pas de mode hors-ligne (vente sans serveur).
- Pas de certification de journal de caisse tunisien.
- L’historique « cette session » autour de minuit est une approximation (intersection journée + ids du rapport X).
