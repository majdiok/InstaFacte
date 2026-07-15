# ADR: Auto-Cash Operation From Invoice Cash Payment

## Status
Accepted

## Context

`Payment` (encaissement de facture) et `CashOperation` (caisse) etaient traites par deux flux separes:

- le paiement client mettait a jour la facture et la comptabilite;
- la caisse etait alimentee uniquement par les operations saisies dans le module Caisse.

Consequence: un paiement facture en especes n'alimentait pas automatiquement le solde de caisse.

## Decision

Nous etendons `CashOperation` avec une tracabilite d'origine metier:

- `Origin` (`Manual`, `InvoicePayment`)
- `SourceType`
- `SourceId`

Un handler applicatif (`CreateCashOperationOnInvoicePaymentHandler`) ecoute `InvoicePaymentRecordedNotification` et cree automatiquement une `CashOperation` de type `Credit` pour les paiements `PaymentMethod.Cash`.

L'activation est protegee par un feature flag backend:

- `Features:AutoCashFromInvoicePayment`
- active en developpement, desactive par defaut en production.

## Accounting Safety

Le paiement facture genere deja une ecriture comptable. Pour eviter toute double ecriture:

- le handler d'auto-caisse ne publie pas la notification comptable de creation d'operation de caisse;
- `GenerateJournalEntryOnCashOperationHandler` ignore toute operation avec `Origin = InvoicePayment`.

## Data Integrity And Idempotence

- index unique filtre sur `(Origin, SourceType, SourceId)` quand `SourceId` est non-null;
- verification applicative `ExistsBySourceAsync` avant insertion.

## Consequences

Positives:

- les encaissements especes des factures sont visibles automatiquement dans Trésorerie > Caisse;
- meilleure traçabilite entre paiement et mouvement de caisse;
- reduction des oublis de saisie manuelle.

Trade-offs:

- flux payment -> cash operation non transactionnel cross-repository (deux sauvegardes distinctes);
- mitigation par idempotence, logging, et piste outbox/reconciliation en amelioration future.
