# ADR: N caisses par entrepôt (une défaut)

## Status
Accepted

## Context

Lot 1 provisionnait **une** `CashRegister` par `Warehouse` (`WH-{code}`). `GetByWarehouseIdAsync` prenait le premier registre. Dès qu’un magasin a deux caisses physiques, open/X/Z/panier visaient le mauvais registre.

Contraintes :

- Magasin = `Warehouse` existant. **Pas** d’entité `Store`.
- Un tenant 1-caisse doit continuer à appeler l’API **sans** `cashRegisterId`.
- Checkout inchangé (`SubmitInvoiceCommand` + `recordPayment(s)`).
- Index unique filtré SQL (EF InMemory ignore `HASFILTER`).

## Decision

- `CashRegister.IsDefault` : au plus une caisse **active** défaut par entrepôt (`IX_CashRegisters_WarehouseId_Default`).
- Provisioning : créer une caisse **seulement** si la liste est vide ; elle est alors défaut.
- API additive :
  - `GET /api/pos/register?warehouseId=` → défaut (comportement historique)
  - `GET /api/pos/registers` / `POST /api/pos/registers`
  - `cashRegisterId?` sur open, open-session, X, Z, cart, held
- Sans `cashRegisterId` → caisse défaut.
- Front : sélecteur si N > 1 ; `localStorage` clé tenant+user+warehouse.
- Settings entrepôts : lister / créer les caisses du magasin.

## Consequences

- Deux vacations Open simultanées sur le même magasin (une par caisse).
- Z d’une caisse n’inclut pas les factures stampées sur une autre.
- Unique défaut **SQL only** (migration `20260825120000_*`), pas en Fluent API.
