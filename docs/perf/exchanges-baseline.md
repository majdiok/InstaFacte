# Échanges — Baseline performance

Date: 2026-08-02  
Scope: cold load of `/exchanges` (company) and `/firm/exchanges` (cabinet).

## Measurement protocol

1. Open Chrome DevTools → Network + Performance.
2. Hard reload after login; navigate to Échanges.
3. Record wall-clock until « Chargement… » disappears and conversation is interactive.
4. Count HTTP calls under `/api/exchanges` and `/api/firm-assignments` until first paint.
5. Backend (dev): enable `Microsoft.EntityFrameworkCore.Database.Command` Information logs; count SQL statements for `List` / `Ensure` / `Messages` / `UnreadSummary`.

Frontend marks (dev console): `exchange.bootstrap.start`, `exchange.thread.ready`, `exchange.messages.ready`, `exchange.tti`.

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

## Post-fix results

Implementation landed 2026-08-02:

| Change | Effect |
|--------|--------|
| `GET /exchanges/bootstrap` (+ FE flag `exchangeBootstrapV2`) | Company/firm cold load → **1** HTTP call for shell data |
| Conversation-first + tab lazy load | No requests/tasks/documents/history on conversation tab |
| SQL aggregated unread | `ListThreads` / `UnreadSummary` no longer load all message rows |
| Batch roles in `MapDetailAsync` | Removes Identity N+1 on ensure/getThread |
| `limit=50` messages + `read-batch` | Smaller payload; 1 write for marks |
| Incremental poll via `after` | Polling no longer reloads full history |

| Scenario | TTI | HTTP calls | Notes |
|----------|-----|------------|-------|
| Company cold | Target <1.5s | ≤ 2 (bootstrap + optional read-batch) | Restart API to pick up DLL if process was locking build |
| Firm cold | Target <2.5s | ≤ 2 | Same |
| Unit tests | — | — | `ExchangeServiceTests` 6/6; `exchange-shell.component.spec` 10/10 |
