# ADR: POS cash-register sessions and immutable Z-close

## Status
Accepted

## Context

The POS screen created invoices through the existing wizard (`SubmitInvoiceCommand`) then optionally recorded payments. Cart state lived in a process `ConcurrentDictionary` keyed only by user id, held tickets lived in IndexedDB, and the client `sessionId` (`POS-{ticketId}`) was a sale ticket — not a cashier shift.

That model could not survive an API restart, multiple API instances, or a real end-of-day close (fond de caisse, rapport X, clôture Z with variance).

Constraints that must not be broken:

- POS is **not** a backend domain module (`AppModule.POS` must not be introduced).
- Checkout stays `SubmitInvoiceCommand` + `recordPayment(s)`.
- No Redis.
- No second general-ledger posting at Z-close (cash 5411 is already posted by invoice cash payments).
- Bureau wizard submit must not require a cash-register session.

## Decision

We persist a tenant cash-register model next to the existing invoice/payment/cash journal:

| Aggregate | Role |
|---|---|
| `CashRegister` | One default register per warehouse (`WH-{code}`, truncated to 20). |
| `CashRegisterSession` | Cashier shift (vacation). At most one **Open** session per register (filtered unique index). A second open is **idempotent** (return existing, do not change float). |
| `ZReport` | Immutable JSON snapshot of the close (numbering `NumberingDocumentType.ZReport = 17`, appended, never reindexed). |
| `PosCartDraft` | Server cart for the user + register. |
| `PosHeldTicket` | Held tickets in SQL; one-shot IndexedDB import. |

Front `PosState.sessionId` remains the **ticket id**. Backend `CashRegisterSession.Id` is the **vacation**. The two names must never be merged.

Opening float and counted cash are **declarations**, not `CashOperation` rows.

Z-close:

- server recomputes expected cash (opening float + signed cash `Payment`s + manual cash ops; auto `Origin=InvoicePayment` ignored to avoid double-count);
- persists the snapshot;
- does **not** post 5411.

Invoice / payment / cash operation receive an optional `CashRegisterSessionId` (nullable, no FK). The wizard stamps it only when metadata provides a session id. Payments inherit the invoice session when the body omits it.

Feature flag `Features:PosRegisterSessions` (default **true**):

- `true`: POS overlay requires an open session before selling.
- `false`: POS can sell without a session; cart and held tickets still persist in SQL.

Lot 1 permissions reuse existing grants: `invoices:create` (operate) / `payments:create` (open/close).

## Accounting Safety

- Invoice cash payments already generate GL + optional auto-caisse (`Origin=InvoicePayment`).
- Z-close is a **read model snapshot**, not a posting event.
- Card/cheque/transfer totals on the Z come from `Payment` rows, not from account 5411.

## Consequences

Positives:

- multi-instance POS cart and held tickets;
- one vacation per register with X report and Z close;
- POS sales can be filtered by session without changing the invoice pipeline.

Trade-offs:

- Lot 1 shares one register (and therefore one session) per warehouse;
- `CashRegisterSession.Version` is **not** an EF concurrency token: double-close is prevented by the filtered unique Open index, unique `ZReport.CashRegisterSessionId`, and `GetOpen` returning null;
- history chip « Cette session » intersects today's invoice list with X-report ids (cross-midnight approximation);
- held tickets warn on close but do not block it.
