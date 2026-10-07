# WMS Analytics Service

Read-only FastAPI service: dashboard aggregates, 3-month demand forecast, reorder point and EOQ.

- No pandas / scikit-learn / numpy — plain Python maths + PostgreSQL aggregation (≈65 MB RAM).
- Pooled **read-only** database connections (use the `wms_ro` role from `db/init`).
- Verifies the **same JWT** as the C# API (`JWT_SECRET` = the API's `JwtSettings__SecretKey`).
- Endpoints: `GET /health`, `/stats`, `/sales-history`, `/low-stock?limit=`, `/predict/{id}`, `/optimize/{id}`.

```bash
cp .env.example .env            # edit
pip install -r requirements.txt
uvicorn main:app --reload
pip install -r requirements-dev.txt && pytest tests -v     # needs a PostgreSQL, see tests/conftest.py
```

Tuning: `DB_POOL_MAX`, `CACHE_TTL_SECONDS` (dashboard aggregates), `LEAD_TIME_DAYS`, `ORDERING_COST`, `HOLDING_COST_RATE`, `SAFETY_STOCK_MULTIPLIER`.

Formulas — forecast: least-squares line over the monthly totals of the last 12 months (months without sales count as 0, cancelled orders excluded, minimum 3 months with sales), clamped at 0. Optimize: daily demand = last-90-days consumption / 90; safety stock = multiplier × demand × lead time; reorder point = demand × lead time + safety stock; EOQ = √(2·D·S / H) with D = 365 × daily demand.
