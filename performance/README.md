# Performance Test Pack

> **Start with `bench_analytics.py`** - it is the benchmark whose results are quoted in the root README
> (original pandas service vs the rewritten one, same dataset, equivalence check + load).
> `k6-load.js` below is a generic load script for the C# API; its "monolith" mode **substitutes** the
> analytics scenario with heavy stock reads, so it is **not a like-for-like comparison** - do not quote its uplift.

```bash
psql -f ../backend-python/tests/schema_analytics.sql wms_bench
psql -v products=100000 -v orders=1000000 -f seed_analytics_bench.sql wms_bench
PGTZ=UTC DB_NAME=wms_bench python bench_analytics.py equivalence      # v1 (baseline_v1/) vs v2
PGTZ=UTC DB_NAME=wms_bench python bench_analytics.py bench --version v2 --seconds 30 --threads 16 --mix product
```
`baseline_v1/` is the original service, kept only as the baseline (needs pandas/scikit-learn; use a separate virtualenv).

---

Scripts to benchmark the polyglot double-backend architecture (C# transactional API + Python analytics API) against a single-backend style run. The goal is to stress the system with large synthetic data and compare throughput/latency between:
- **Polyglot mode**: transactional calls go to the C# API, analytics calls go to the Python API.
- **Monolith baseline**: all calls are sent to the C# API (analytics scenario is substituted with heavy stock reads) to mimic an ordinary single-backend WMS.

## Contents
- `data_generator.py` – Bulk loader to create huge mock data in PostgreSQL (products, warehouses, stocks, orders, order items, customers).
- `k6-load.js` – k6 load script with transactional + analytics scenarios; switchable between `polyglot` and `monolith` modes.
- `requirements.txt` – Dependencies for the generator.

## Quick Start
1) Generate data (example: 1M products, 100 warehouses, 5M orders):
```
python performance/data_generator.py ^
  --db-host localhost --db-port 5432 --db-name wms_db --db-user postgres --db-pass <pwd> ^
  --products 1000000 --warehouses 100 --locations-per-warehouse 5 ^
  --customers 200000 --orders 5000000 --max-items-per-order 6 --stock-coverage 0.3
```
Notes:
- Uses PostgreSQL `COPY`-style bulk inserts via `execute_values`.
- Tune `--stock-coverage` (0-1) to control how many product/warehouse pairs get stock rows (1.0 means all pairs, can explode to products×warehouses).
- Run on a machine with enough RAM/IO; the script commits in chunks to avoid huge transactions.

2) Run k6 load (polyglot vs monolith):
```
# Polyglot (default): C# at 5000, Python at 8000
k6 run performance/k6-load.js \
  -e API_BASE=http://localhost:5000 \
  -e ANALYTICS_BASE=http://localhost:8000 \
  -e MODE=polyglot

# Monolith baseline: point analytics to the same backend and switch mode
k6 run performance/k6-load.js \
  -e API_BASE=http://localhost:5000 \
  -e ANALYTICS_BASE=http://localhost:5000 \
  -e MODE=monolith
```

3) Compare uplift:
- Export summary (`-o json=results.json`) or use k6 cloud/grafana agent.
- Uplift (%) = `(polyglot_metric - monolith_metric) / monolith_metric * 100`.
- Focus on transactional p95 latency and throughput while analytics load is present.

## Scenarios in `k6-load.js`
- `transactional_write`: POST `/api/orders` with realistic items.
- `transactional_read`: GET `/api/products`, `/api/stocks`.
- `analytics`: 
  - `polyglot` → GET `/api/analytics/predict/{productId}` and `/api/analytics/optimize/{productId}` on the Python API.
  - `monolith` → GET `/api/stocks` and `/api/orders` on the C# API to simulate analytics pressure on a single backend.

Each scenario ramps virtual users (VUs) up to stress levels; adjust via env vars.

## Suggested Data Scales
- Small shakeout: 100k products, 10 warehouses, 1M orders.
- Medium: 1M products, 100 warehouses, 5M orders.
- Large: 5M products, 500 warehouses, 20M orders (requires strong hardware).

## Environment Variables (k6)
- `API_BASE` (default `http://localhost:5000`)
- `ANALYTICS_BASE` (default `http://localhost:8000`)
- `MODE` (`polyglot` | `monolith`, default `polyglot`)
- `PRODUCT_SAMPLE` (sample size for analytics product IDs, default `5000`)
- `ORDER_TPS_TARGET` (desired orders/sec, default `auto` by VU ramp)

## What to Record
- p50/p95/p99 latency per scenario.
- Throughput (req/s) per scenario.
- Error rate.
- CPU/RAM for each backend and PostgreSQL.
- DB metrics: read/write IOPS, buffer hit ratio.
- Cache hit ratio (if you add Redis later).

## Reading the Results
- Expect lower cross-talk in polyglot mode: analytics CPU and long reads should not hurt transactional p95 as much as in monolith mode.
- Note the saturation point (knee) where latency explodes; compare the VU level or RPS at that point between modes.

## Housekeeping
- Run migrations first (`dotnet ef database update` in `backend-csharp/WMS.API`).
- Ensure PostgreSQL shared buffers/checkpoint settings are sized for the dataset.
- Disable debug logs during load to reduce noise.

## Investigated: "bench_analytics.py gives ~0.7 req/s" reports
If `bench_analytics.py bench` reports sub-1 req/s, check these in order before assuming the
database is slow - every one of them produces symptoms that look like "everything is slow"
from the outside:

1. **Wrong database.** `bench` assumes `DB_NAME` holds the `seed_analytics_bench.sql` dataset
   (100k products) and samples random product IDs in `range(1, 3000)`. Pointed at the small
   demo `wms_db` (a handful of products), ~99.9% of requests 404 fast and the reported
   `req_per_sec` is misleadingly high while `errors` is almost equal to `requests` - read the
   `errors` field, not just throughput.
2. **Stale planner stats after bulk load.** `data_generator.py` now runs `ANALYZE;` after
   seeding (see the "update statistics after bulk generation" commit). Without it, Postgres
   can pick a sequential scan over `Orders`/`OrderItems` instead of `ix_orders_orderdate` /
   `ix_orderitems_product_order`, confirmed with `EXPLAIN (ANALYZE, BUFFERS)` on the
   `/predict` query. If you ever regenerate data with an older copy of the generator, run
   `ANALYZE;` manually before benchmarking.
3. **Thread count vs connection pool.** The benchmark's `--threads` should not exceed
   `DB_POOL_MAX` (6 in `docker-compose.yml` for `wms-analytics`). With more worker threads
   than pool connections, excess requests queue on the pool's semaphore
   (`DB_POOL_WAIT_SECONDS`, default 5s) instead of failing, which can look like "everything
   got slow" under load. Match `--threads` to `DB_POOL_MAX`, or raise `DB_POOL_MAX` for the
   benchmark run.
4. **Where it's being run from.** `bench_analytics.py` starts the service as a native
   subprocess and talks to it with `urllib.request` (new TCP connection per call, no
   keep-alive). Run it from the same OS/filesystem as the service and database - on
   Windows+WSL2, invoking it from PowerShell against a WSL2-hosted Postgres (or vice versa)
   crosses a virtualized network boundary per connection; we could not reproduce this
   ourselves from WSL, so if you still see sub-1 req/s after 1-3, run `bench_analytics.py`
   from inside the same environment (WSL shell) as the one running `docker compose`.

With a correctly-seeded database and current code, measured results on this project's dev
box: 123-193 req/s, p50 35-70ms, 0 errors, 8 threads/VUs - both via the native benchmark
client and via k6 run inside the `wmsfinal` compose network against the containerized
service. We were not able to reproduce a ~0.7 req/s result; if you can, capture
`docker stats --no-stream` and `top -b -n1` during the run and compare against the numbers
above.

