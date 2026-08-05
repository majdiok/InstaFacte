# Échanges — Baseline performance

Date: 2026-08-02 (updated 2026-08-05)  
Scope: cold load of `/exchanges` (company) and `/firm/exchanges` (cabinet).

## Measurement protocol

1. Open Chrome DevTools → Network + Performance.
2. Hard reload after login; navigate to Échanges.
3. Record wall-clock until « Chargement… » disappears and conversation is interactive.
4. Count HTTP calls under `/api/exchanges` and `/api/firm-assignments` until first paint.
5. Backend (dev): enable `Microsoft.EntityFrameworkCore.Database.Command` Information logs; count SQL statements for `List` / `Ensure` / `Messages` / `UnreadSummary`.

Frontend marks (dev console): `exchange.bootstrap.start`, `exchange.thread.ready`, `exchange.messages.ready`, `exchange.tti`.

Backend log: `Exchange bootstrap {Kind} completed in {ElapsedMs}ms success={Success}`.

### Company scenario matrix (fill measured values)

| Scenario | Thread | Messages | Liaison | Bootstrap TTFB | TTI (`tti`−`start`) | SQL count | Notes |
|----------|--------|----------|---------|----------------|---------------------|-----------|-------|
| S1 — first access | created by ensure | 0 | Active | _TBD_ | _TBD_ | _TBD_ | Write path EnsureThread |
| S2 — recurrent | exists | &lt; 50 | Active | _TBD_ | _TBD_ | _TBD_ | Fast-path read-only (expected) |
| S3 — heavy thread | exists | 200+ | Active | _TBD_ | _TBD_ | _TBD_ | Page limit=50 |
| S4 — return after 5 min | exists | poll growth | Active | _TBD_ | _TBD_ | _TBD_ | Cap `after` to MaxMessagePageSize |

How to capture:

1. Login as company Administrator with an active firm assignment.
2. DevTools Network → filter `bootstrap` → note Waiting (TTFB) and Content Download.
3. Console → `[exchanges-perf] exchange.bootstrap.start` then `exchange.tti`; delta = client TTI.
4. API logs → count `Executed DbCommand` between bootstrap start/end for one request.

## Pre-fix baseline (estimated from code path)

| Scenario | HTTP calls (typical) | Notes |
|----------|----------------------|-------|
| Company `/exchanges` | 9–15 + N mark-read | Waterfall: listThreads → assignment → ensure → navigate → getThread → forkJoin×5 → badge |
| Firm `/firm/exchanges` | 8–12 + N mark-read | forkJoin threads+clients → ensure/select → loadAll×5 |
| Badge poll (every 60s) | 1 | `GET /unread-summary` re-scans all messages |

| Endpoint | Cost driver |
|----------|-------------|
| `GET /exchanges` | Load all message IDs across threads for unread |
| `POST /exchanges/ensure` | N+1 `GetRolesAsync` (up to 40 users) |
| `GET .../messages` | Full history, no limit |
| `GET /unread-summary` | Calls `ListThreads` + second message/read scan |

### Target SLA (acceptance)

| Metric | Target |
|--------|--------|
| TTI company (≤100 messages) | < 1.5 s p95 |
| TTI firm (50 clients) | < 2.5 s p95 |
| Initial HTTP calls | ≤ 4 (+ 1 read-batch) |
| Initial messages payload (500 msgs thread) | < 200 KB (paged) |
| SQL statements per company bootstrap (recurrent) | ≤ 5 (target after fast-path) |

## Post-fix results

Implementation landed 2026-08-02; company cold-load follow-up 2026-08-05:

| Change | Effect |
|--------|--------|
| `GET /exchanges/bootstrap` (+ FE flag `exchangeBootstrapV2`) | Company/firm cold load → **1** HTTP call for shell data |
| Conversation-first + tab lazy load | No requests/tasks/documents/history on conversation tab |
| SQL aggregated unread | `ListThreads` / `UnreadSummary` no longer load all message rows |
| Batch roles in `MapDetailAsync` | Removes Identity N+1 on ensure/getThread |
| `limit=50` messages + `read-batch` | Smaller payload; 1 write for marks |
| Incremental poll via `after` | Polling no longer reloads full history |
| **Company bootstrap fast-path** | Skip `EnsureThread`/`SaveChanges` when thread already exists |
| **Unread LEFT JOIN aggregation** | Replaces correlated `NOT EXISTS` subquery |
| **Perf indexes** | `(UserId, MessageId)`, `(CompanyTenantId, LastActivityAt)`, `(ThreadId, Visibility, SentAt)` |
| **Capped `after` poll** | Max 100 messages per incremental fetch |
| **Badge poll delay** | First `unread-summary` at 15s (not t=0 collision with bootstrap) |
| **Bootstrap timeout + skeleton** | 30s timeout with retry; progressive shell while loading |

| Scenario | TTI | HTTP calls | Notes |
|----------|-----|------------|-------|
| Company cold (recurrent) | Target <1.5s | ≤ 2 (bootstrap + optional read-batch) | Fast-path read-only; measure and fill matrix above |
| Company cold (first access) | Target <1.5s | ≤ 2 | EnsureThread write once |
| Firm cold | Target <2.5s | ≤ 2 | Same |
| Unit tests | — | — | `ExchangeServiceTests` (+ guardrail) 20/20; `exchange-shell` + badge + notification specs 21/21 |
