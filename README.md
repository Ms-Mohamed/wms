# WMS — Warehouse Management System with a tested stock-integrity core

A stock-management platform whose one job is to **never lose or invent stock**, and to stay fast on weak hardware.

- **C# / ASP.NET Core 8 + PostgreSQL** — transactions: orders, shipments, receipts, returns, invoices.
- **Python / FastAPI** — read-only analytics: demand forecast, reorder point, EOQ, dashboard aggregates.
- **React + TypeScript** — UI (FR/EN).

## What is actually proven

Every claim below has a test or a measurement you can re-run. Nothing here is "trust me".

| Claim | Proof | Status |
|---|---|---|
| Stock never oversells under concurrency | 100 parallel shippers on a 5-unit stock → **exactly 5 succeed**, 95 refused, stock = 0 (`tests/db`) | verified, 21 DB tests pass |
| The test is meaningful | Control experiment: the **original** read-check-write logic lets **20 of 20** shipments through on that same 5-unit stock | verified |
| Ledger always equals stock | `Stock.Quantity = SUM(StockMovements.Delta)`; random multi-threaded receive/issue/adjust/transfer, then the `wms_stock_drift` view must be empty | verified |
| No negative stock, even from raw SQL | `CHECK` constraints on `Stocks` | verified |
| No deadlock on opposite transfers | 8 threads × 100 transfers A↔B | verified |
| No duplicate document numbers | Postgres sequences, 40 concurrent callers | verified |
| Same order shipped twice → once | Conditional `UPDATE ... WHERE Status IN (...)` claim + 8 parallel callers | xUnit test written (see note) |
| Retried POST creates one order | `Idempotency-Key` header | xUnit test written (see note) |
| Analytics service is light | 63–66 MB RAM vs 193–257 MB for the pandas/scikit-learn original, 3.4× throughput | measured, see below |
| Analytics service cannot write | read-only DB role + read-only sessions; test attempts an `UPDATE` | verified, 21 analytics tests pass |

> **Note on the C# tests.** The 9 xUnit tests in `backend-csharp/WMS.Tests` run the real EF migrations on a real PostgreSQL. They were written in an environment that could not download NuGet packages, so they were **type-checked but not executed there**. The database behaviour they rely on is executed by the Python tests above (same SQL file). CI (`.github/workflows/ci.yml`) runs them on every push — look at the badge/Actions tab for the real result.

## How the integrity works

The rules live **in the database**, so no code path (C#, Python, `psql`, a future service) can break them. Single source of truth: [`backend-csharp/WMS.Data/Sql/stock_integrity.sql`](backend-csharp/WMS.Data/Sql/stock_integrity.sql), applied by an EF migration and executed directly by the tests.

1. **Atomic stock decrement.** `wms_stock_issue()` is one `UPDATE ... WHERE Quantity - Reserved >= @q`. Under READ COMMITTED a concurrent caller waits for the row lock, then re-checks the condition on the fresh row — two callers can never take the last unit.
2. **Signed ledger.** Every quantity change writes a `StockMovements` row with a signed `Delta` in the same transaction. `wms_stock_drift` must always be empty.
3. **Constraints as the last line of defence.** `Quantity >= 0`, `0 <= Reserved <= Quantity`.
4. **Claim-before-act on documents.** Shipping, receiving a purchase order and receiving a return first *claim* the document with a conditional `UPDATE` (status transition). Double-click, retry or two users can't process it twice.
5. **Incremental weighted average cost (CUMP)** computed inside the same `UPDATE` (4-decimal precision; the old 2-decimal rounding drifted).
6. **Sequences for document numbers**, a race-free `get_or_create` for stock rows (and a unique index that also covers `NULL` locations — the old one didn't, so duplicate rows were possible), lock ordering for transfers.
7. A shipment is **all-or-nothing**: if line 2 lacks stock, line 1, the status claim and the invoice roll back.

```
React (nginx) ──/api──────────► C# API ──────► PostgreSQL ◄── read-only role ── Python analytics
              └─/python-api──────────────────────────────────────────────────────►
                 (JWT shared by both services)
```

Why two languages? C# owns the transactional core. Python owns analytics. Today the analytics maths is a least-squares trend and the EOQ formula, so it uses **no pandas/scikit-learn** (that's why it is small); the Python service is the place where heavier models would go if the data justified them.

## Performance (measured)

Dataset: 100,000 products, 1,000,000 orders, 2,000,000 order lines (343 MB). 16 concurrent clients, 25 s, mix of `/predict` and `/optimize`. **Server, PostgreSQL and the load generator shared the same 2-vCPU / 8 GB cloud sandbox**, so absolute numbers are conservative; compare the two rows to each other, not to your hardware. Reproduce with `performance/bench_analytics.py`.

| | Original (pandas + scikit-learn) | Now |
|---|---|---|
| Throughput | 50 req/s | **173 req/s** |
| p50 / p95 latency | 317 ms / 361 ms | **85 ms / 164 ms** |
| RAM (RSS) | 192–197 MB | **63–66 MB** |
| Python dependencies | pandas, scikit-learn, numpy… | FastAPI, psycopg2, PyJWT |

Equivalence check on 60 products against the original: reorder point / EOQ / demand identical (±0.01); 3-month forecasts within 1.7 % (the original spaced months by 30.44 days, the new one by calendar month and treats months without sales as zero).

Honest limits of that benchmark:
- Dashboard aggregates over 1 M orders take ~0.3–0.45 s of database CPU each. They are served from a 10-second cache; **uncached, the same mix dropped to 17 req/s** on this machine. If you need real-time totals at this size, add a summary table.
- This is the analytics service only. The C# API under load was **not** benchmarked here (no .NET packages available) — `performance/k6-load.js` is there for that, but its "monolith" mode is not a like-for-like comparison and should not be quoted.
- "Low-end PC" is shown by memory footprint and CPU cgroup limits in `docker-compose.yml` (db 384 MB / 1 CPU, API 384 MB / 1 CPU, analytics 128 MB / 0.5 CPU, frontend 64 MB). It has **not** been run on a physical low-end machine.

## Bugs found and fixed while hardening

- **Overselling** on concurrent shipments (read-check-write).
- **Duplicate stock rows** possible when location is `NULL` (unique index ignores NULLs).
- **Shipping twice**: two requests could both pass the "already shipped" check.
- **Purchase-order receipt hard-coded warehouse 1** and could be applied twice.
- **Duplicate order/invoice numbers** (`last + 1`), and PO/RMA numbers from 4 random hex characters.
- **`/predict` returned HTTP 500** when the 12-month window crossed a Morocco clock change (mixed UTC offsets → pandas error).
- **Analytics proxy in the dev stack pointed at `/api/analytics/...`**, a path the service never served.
- **Anyone could self-register** and get full API access; JWT signing key fell back to a known string; admin seeded with `password123`; database password committed in an example file and docs (these remain in git history — **rotate any password that was ever real**).
- Analytics endpoints were `async def` doing blocking DB calls (blocked the event loop) and opened a new connection per request.
- List endpoints returned entire tables; the dashboard downloaded every row to count them.

## Run it

```bash
cp .env.example .env        # set the passwords and a >= 32 char JWT_SECRET
docker compose up -d --build
# UI: http://localhost   (user: admin / the WMS_ADMIN_PASSWORD you set)
```

Development with hot reload: `docker compose -f docker-compose.yml -f docker-compose.dev.yml up`.

### Tests

```bash
# database integrity (real PostgreSQL; set WMS_TEST_DSN, default uses a local socket)
pip install "psycopg[binary]" pytest && pytest tests/db -v

# analytics service
pip install -r backend-python/requirements-dev.txt && pytest backend-python/tests -v

# C# (needs .NET 8 SDK and a PostgreSQL; creates and drops throw-away databases)
export WMS_TEST_CONNECTION="Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=postgres"
dotnet test backend-csharp/WMS.sln
```

### Benchmark

```bash
psql -f backend-python/tests/schema_analytics.sql wms_bench
psql -v products=100000 -v orders=1000000 -f performance/seed_analytics_bench.sql wms_bench
PGTZ=UTC DB_NAME=wms_bench python performance/bench_analytics.py equivalence
PGTZ=UTC DB_NAME=wms_bench python performance/bench_analytics.py bench --version v2 --mix product
```

## API (C#, `/api`, JWT required except login)

`POST auth/login` · `POST auth/register` (Admin only) · `GET products|stocks|orders` (paged: `?page=&pageSize=`, max 500, total in `X-Total-Count`) · `POST orders` (`Idempotency-Key` header supported) · `POST orders/{id}/ship` · `POST purchaseorders/{id}/receive` · `POST returns/{id}/receive` · `POST inventory/adjust` · `POST inventory/transfer` · `GET /health`.

Analytics (`/python-api` via nginx, same JWT): `GET stats` · `sales-history` · `low-stock?limit=` · `predict/{productId}` · `optimize/{productId}` · `health`.

## Known limitations (not hidden)

- **Reservations are not implemented.** `ReservedQuantity` is enforced by the guards, but nothing reserves stock when an order is created; stock is checked and removed at shipment.
- **No partial shipments**: every order line must ship in full (the old code allowed it but invoiced the full amount).
- **The React tables have no pagination UI yet.** The API caps at 500 rows per request, so screens beyond 500 rows need pagination added in the UI. The dashboard no longer depends on it.
- Roles: only account creation is Admin-restricted; other endpoints are open to any authenticated user.
- Lots / serial numbers exist in the schema but are not wired into the ledger.
- The test suites ran on PostgreSQL 16 only. `docker-compose.yml` pins `postgres:14-alpine` so existing data volumes keep working (Postgres cannot open a volume written by another major version); the SQL uses nothing newer than 14, but run the three suites against 14 before relying on it. To move an existing volume to 16: `pg_dump` from the 14 container, start a fresh 16 volume, restore.
- Older design notes in `ARCHITECTURE.md`, `HOW_IT_WORKS.md` and friends predate this hardening and may disagree with this README — this file is the reference.
