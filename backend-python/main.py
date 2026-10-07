"""
WMS Analytics Service - read-only analytics for the WMS platform.

Design goals (this service must stay light on a low-end machine):
  * no pandas / scikit-learn / numpy: the maths here is a least-squares line over <= 12 points and the
    EOQ formula, which is a few microseconds of plain Python. Aggregation is done by PostgreSQL.
  * blocking DB calls run in FastAPI's thread pool (plain `def` endpoints) - never on the event loop.
  * pooled, read-only database connections (no connect/auth round trip per request).
  * small TTL cache on the dashboard aggregates.
  * the service can never write: every connection is opened READ ONLY.
  * JWT (same secret as the C# API) is required, errors never leak tracebacks.
"""
import logging
import math
import os
import threading
import time
from contextlib import contextmanager
from datetime import date
from typing import Callable, List, Optional

import jwt
import psycopg2
from dotenv import load_dotenv
from fastapi import Depends, FastAPI, HTTPException, Query, Request
from fastapi.middleware.cors import CORSMiddleware
from fastapi.middleware.gzip import GZipMiddleware
from fastapi.responses import JSONResponse
from fastapi.security import HTTPAuthorizationCredentials, HTTPBearer
from psycopg2.extras import RealDictCursor
from psycopg2.pool import PoolError, ThreadedConnectionPool
from pydantic import BaseModel

load_dotenv()  # local development convenience; in Docker the environment is already set
log = logging.getLogger("wms.analytics")
logging.basicConfig(level=os.getenv("LOG_LEVEL", "INFO"), format="%(asctime)s %(levelname)s %(name)s %(message)s")

# ----------------------------------------------------------------------------------------------
# Configuration
# ----------------------------------------------------------------------------------------------
ORDER_STATUS_CANCELLED = 5  # WMS.Data.Entities.OrderStatus.Cancelled


def _env_float(name: str, default: float) -> float:
    return float(os.getenv(name, default))


JWT_SECRET = os.getenv("JWT_SECRET") or os.getenv("JwtSettings__SecretKey")
AUTH_DISABLED = os.getenv("AUTH_DISABLED", "false").lower() == "true"
if not JWT_SECRET and not AUTH_DISABLED:
    raise RuntimeError(
        "JWT_SECRET is not set (must equal the C# API's JwtSettings__SecretKey). "
        "For local experiments only, set AUTH_DISABLED=true."
    )

CACHE_TTL = _env_float("CACHE_TTL_SECONDS", 10)
LEAD_TIME_DAYS = int(os.getenv("LEAD_TIME_DAYS", "7"))
ORDERING_COST = _env_float("ORDERING_COST", 50.0)
HOLDING_COST_RATE = _env_float("HOLDING_COST_RATE", 0.20)
SAFETY_STOCK_MULTIPLIER = _env_float("SAFETY_STOCK_MULTIPLIER", 1.5)
CONSUMPTION_WINDOW_DAYS = 90

# ----------------------------------------------------------------------------------------------
# Database: pooled READ ONLY connections
# ----------------------------------------------------------------------------------------------
_pool: Optional[ThreadedConnectionPool] = None
_pool_lock = threading.Lock()
POOL_MAX = int(os.getenv("DB_POOL_MAX", "8"))
POOL_WAIT_SECONDS = _env_float("DB_POOL_WAIT_SECONDS", 5)
# psycopg2's pool raises immediately when it is empty. A bounded semaphore makes a burst of requests
# queue (up to POOL_WAIT_SECONDS) instead of failing - and caps DB connections at POOL_MAX.
_pool_slots = threading.BoundedSemaphore(POOL_MAX)


def _get_pool() -> ThreadedConnectionPool:
    global _pool
    if _pool is None:
        with _pool_lock:
            if _pool is None:
                _pool = ThreadedConnectionPool(
                    minconn=int(os.getenv("DB_POOL_MIN", "1")),
                    maxconn=POOL_MAX,
                    host=os.getenv("DB_HOST", "localhost"),
                    port=os.getenv("DB_PORT", "5432"),
                    dbname=os.getenv("DB_NAME", "wms_db"),
                    user=os.getenv("DB_USER", "postgres"),
                    password=os.getenv("DB_PASSWORD", ""),
                    connect_timeout=5,
                    options="-c default_transaction_read_only=on -c statement_timeout=15000",
                )
    return _pool


@contextmanager
def read_cursor():
    """A read-only cursor from the pool. The connection ALWAYS goes back to the pool (even when the
    endpoint raises HTTPException); a connection that broke is closed instead of recycled."""
    if not _pool_slots.acquire(timeout=POOL_WAIT_SECONDS):
        raise PoolError("no database connection available")
    try:
        pool = _get_pool()
        conn = pool.getconn()
        broken = False
        try:
            conn.set_session(readonly=True, autocommit=True)
            with conn.cursor(cursor_factory=RealDictCursor) as cur:
                yield cur
        except (psycopg2.OperationalError, psycopg2.InterfaceError):
            broken = True
            raise
        finally:
            pool.putconn(conn, close=broken)
    finally:
        _pool_slots.release()


# ----------------------------------------------------------------------------------------------
# Tiny TTL cache (thread-safe) for dashboard aggregates
# ----------------------------------------------------------------------------------------------
_cache: dict = {}
_cache_lock = threading.Lock()


def cached(key: str, loader: Callable[[], object]):
    if CACHE_TTL <= 0:
        return loader()
    now = time.monotonic()
    with _cache_lock:
        hit = _cache.get(key)
        if hit and now - hit[0] < CACHE_TTL:
            return hit[1]
    value = loader()
    with _cache_lock:
        _cache[key] = (now, value)
    return value


# ----------------------------------------------------------------------------------------------
# App, auth, errors
# ----------------------------------------------------------------------------------------------
app = FastAPI(
    title="WMS Analytics Service",
    description="Read-only forecasting and purchase-optimisation service",
    version="2.0.0",
)
app.add_middleware(GZipMiddleware, minimum_size=500)
app.add_middleware(
    CORSMiddleware,
    allow_origins=[o.strip() for o in os.getenv(
        "CORS_ORIGINS", "http://localhost:3000,http://localhost:5173,http://localhost").split(",") if o.strip()],
    allow_credentials=True,
    allow_methods=["GET"],
    allow_headers=["Authorization", "Content-Type"],
)

_bearer = HTTPBearer(auto_error=False)


def require_user(creds: Optional[HTTPAuthorizationCredentials] = Depends(_bearer)) -> Optional[dict]:
    if AUTH_DISABLED:
        return None
    if creds is None:
        raise HTTPException(status_code=401, detail="Authentication required")
    try:
        # .NET writes HS256 tokens signed with the UTF-8 bytes of the secret; `exp` is validated.
        return jwt.decode(creds.credentials, JWT_SECRET, algorithms=["HS256"],
                          options={"verify_aud": False, "verify_iss": False})
    except jwt.PyJWTError:
        raise HTTPException(status_code=401, detail="Invalid or expired token")


@app.exception_handler(psycopg2.Error)
async def db_error_handler(request: Request, exc: psycopg2.Error):
    log.error("database error on %s: %s", request.url.path, exc)
    return JSONResponse(status_code=503, content={"detail": "Database unavailable"})


@app.exception_handler(PoolError)
async def pool_error_handler(request: Request, exc: PoolError):
    log.error("connection pool exhausted on %s", request.url.path)
    return JSONResponse(status_code=503, content={"detail": "Service busy, retry shortly"}, headers={"Retry-After": "1"})


@app.exception_handler(Exception)
async def unhandled(request: Request, exc: Exception):
    log.exception("unhandled error on %s", request.url.path)  # traceback stays in the server log
    return JSONResponse(status_code=500, content={"detail": "Internal server error"})


# ----------------------------------------------------------------------------------------------
# Models (response contracts are unchanged from v1 - the React app depends on them)
# ----------------------------------------------------------------------------------------------
class ForecastMonth(BaseModel):
    mois: str  # 'YYYY-MM'
    prediction: float


class DemandForecastResponse(BaseModel):
    product_id: int
    product_code: str
    product_name: str
    forecasts: List[ForecastMonth]
    message: Optional[str] = None


class OptimizationResponse(BaseModel):
    product_id: int
    product_code: str
    product_name: str
    current_stock: float
    reorder_point: float
    eoq: float
    average_daily_demand: float
    lead_time_days: int
    safety_stock: float
    unit_cost: float


class StatsResponse(BaseModel):
    total_stock_value: float
    total_products: int
    low_stock_count: int
    stock_value_currency: str = "€"


class SalesHistoryItem(BaseModel):
    month: str
    total_revenue: float
    total_orders: int


class LowStockItem(BaseModel):
    id: int
    product_name: str
    product_code: str
    current_stock: float
    reorder_point: float
    unit_cost: float


class LowStockResponse(BaseModel):
    items: List[LowStockItem]


# ----------------------------------------------------------------------------------------------
# Pure maths (unit-tested without a database)
# ----------------------------------------------------------------------------------------------
def linear_fit(ys: List[float]) -> tuple:
    """Ordinary least squares y = a + b*x for x = 0..n-1. Returns (a, b)."""
    n = len(ys)
    mean_x = (n - 1) / 2.0
    mean_y = sum(ys) / n
    sxx = sum((i - mean_x) ** 2 for i in range(n))
    sxy = sum((i - mean_x) * (y - mean_y) for i, y in enumerate(ys))
    b = sxy / sxx if sxx else 0.0
    return mean_y - b * mean_x, b


def month_add(d: date, months: int) -> date:
    idx = d.year * 12 + (d.month - 1) + months
    return date(idx // 12, idx % 12 + 1, 1)


def fill_missing_months(rows: List[tuple]) -> List[float]:
    """rows = [(date(first of month), qty), ...] sorted. Months without sales count as 0, not as 'missing'
    (v1 skipped them, which biased the trend upwards for slow movers)."""
    by_month = {(d.year, d.month): float(q) for d, q in rows}
    first, last = rows[0][0], rows[-1][0]
    out, cur = [], first
    while cur <= last:
        out.append(by_month.get((cur.year, cur.month), 0.0))
        cur = month_add(cur, 1)
    return out


def forecast_next_months(rows: List[tuple], horizon: int = 3) -> List[dict]:
    series = fill_missing_months(rows)
    a, b = linear_fit(series)
    last = rows[-1][0]
    n = len(series)
    return [
        {"mois": month_add(last, i).strftime("%Y-%m"),
         "prediction": round(max(0.0, a + b * (n - 1 + i)), 2)}
        for i in range(1, horizon + 1)
    ]


def reorder_figures(total_consumption: float, unit_cost: float) -> dict:
    avg_daily = total_consumption / CONSUMPTION_WINDOW_DAYS
    safety = SAFETY_STOCK_MULTIPLIER * avg_daily * LEAD_TIME_DAYS
    reorder_point = avg_daily * LEAD_TIME_DAYS + safety
    annual = avg_daily * 365
    holding = unit_cost * HOLDING_COST_RATE
    if holding > 0 and annual > 0:
        eoq = math.sqrt((2 * annual * ORDERING_COST) / holding)
    else:
        eoq = max(10.0, avg_daily * 30)
    return {"average_daily_demand": avg_daily, "safety_stock": safety, "reorder_point": reorder_point, "eoq": eoq}


# ----------------------------------------------------------------------------------------------
# Endpoints
# ----------------------------------------------------------------------------------------------
@app.get("/")
def root():
    return {"service": "WMS Analytics Service", "status": "ok", "version": app.version}


@app.get("/health")
def health():
    """Liveness + database readiness (used by the docker healthcheck)."""
    with read_cursor() as cur:
        cur.execute("SELECT 1")
    return {"status": "ok"}


@app.get("/stats", response_model=StatsResponse)
def get_stats(_user=Depends(require_user)):
    def load():
        with read_cursor() as cur:
            # one pass over Stocks; stock is valued at the weighted average cost (CUMP)
            cur.execute("""
                SELECT COALESCE(SUM("Quantity" * "AverageCost"), 0)           AS total_value,
                       COUNT(*) FILTER (WHERE "Quantity" <= "ReorderPoint")  AS low_stock
                  FROM "Stocks"
            """)
            s = cur.fetchone()
            cur.execute('SELECT COUNT(*) AS n FROM "Products"')
            n = cur.fetchone()["n"]
        return StatsResponse(total_stock_value=round(float(s["total_value"]), 2),
                             total_products=int(n), low_stock_count=int(s["low_stock"]))
    return cached("stats", load)


@app.get("/sales-history", response_model=List[SalesHistoryItem])
def get_sales_history(_user=Depends(require_user)):
    def load():
        with read_cursor() as cur:
            cur.execute("""
                SELECT TO_CHAR("OrderDate", 'YYYY-MM') AS month,
                       SUM("TotalAmount")              AS total_revenue,
                       COUNT(*)                        AS total_orders
                  FROM "Orders"
                 WHERE "OrderDate" >= date_trunc('month', NOW()) - INTERVAL '5 months'
                   AND "Status" <> %s
                 GROUP BY 1 ORDER BY 1
            """, (ORDER_STATUS_CANCELLED,))
            return [SalesHistoryItem(month=r["month"], total_revenue=float(r["total_revenue"] or 0),
                                     total_orders=int(r["total_orders"])) for r in cur.fetchall()]
    return cached("sales", load)


@app.get("/low-stock", response_model=LowStockResponse)
def get_low_stock(limit: int = Query(200, ge=1, le=1000), _user=Depends(require_user)):
    def load():
        with read_cursor() as cur:
            cur.execute("""
                SELECT p."Id", p."Name", p."Code", s."Quantity", s."ReorderPoint", p."CostPrice"
                  FROM "Stocks" s JOIN "Products" p ON p."Id" = s."ProductId"
                 WHERE s."Quantity" <= s."ReorderPoint"
                 ORDER BY s."Quantity" ASC, p."Id"
                 LIMIT %s
            """, (limit,))
            return LowStockResponse(items=[LowStockItem(
                id=r["Id"], product_name=r["Name"], product_code=r["Code"],
                current_stock=float(r["Quantity"] or 0), reorder_point=float(r["ReorderPoint"] or 0),
                unit_cost=float(r["CostPrice"] or 0)) for r in cur.fetchall()])
    return cached(f"low:{limit}", load)


@app.get("/predict/{product_id}", response_model=DemandForecastResponse)
def predict_demand(product_id: int, _user=Depends(require_user)):
    """Next-3-months demand: least-squares trend over the last 12 months of sales (min 3 months with sales)."""
    with read_cursor() as cur:
        cur.execute('SELECT "Id","Code","Name" FROM "Products" WHERE "Id" = %s', (product_id,))
        product = cur.fetchone()
        if not product:
            raise HTTPException(status_code=404, detail=f"Produit avec ID {product_id} introuvable")

        cur.execute("""
            SELECT date_trunc('month', o."OrderDate")::date AS month, SUM(oi."Quantity") AS qty
              FROM "OrderItems" oi JOIN "Orders" o ON o."Id" = oi."OrderId"
             WHERE oi."ProductId" = %s
               AND o."OrderDate" >= NOW() - INTERVAL '12 months'
               AND o."Status" <> %s
             GROUP BY 1 ORDER BY 1
        """, (product_id, ORDER_STATUS_CANCELLED))
        rows = [(r["month"], float(r["qty"])) for r in cur.fetchall()]

    base = dict(product_id=product_id, product_code=product["Code"], product_name=product["Name"])
    if len(rows) < 3:
        return DemandForecastResponse(
            **base, forecasts=[],
            message="Pas assez de données historiques pour effectuer une prévision (minimum 3 mois requis)")
    return DemandForecastResponse(**base, forecasts=[ForecastMonth(**f) for f in forecast_next_months(rows)])


@app.get("/optimize/{product_id}", response_model=OptimizationResponse)
def optimize_purchases(product_id: int, _user=Depends(require_user)):
    """Reorder point and EOQ from the last 90 days of consumption."""
    with read_cursor() as cur:
        cur.execute("""
            SELECT p."Id", p."Code", p."Name", p."CostPrice",
                   COALESCE((SELECT SUM("Quantity") FROM "Stocks" WHERE "ProductId" = p."Id"), 0) AS stock,
                   COALESCE((SELECT SUM(oi."Quantity")
                               FROM "OrderItems" oi JOIN "Orders" o ON o."Id" = oi."OrderId"
                              WHERE oi."ProductId" = p."Id"
                                AND o."OrderDate" >= NOW() - make_interval(days => %s)
                                AND o."Status" <> %s), 0) AS consumption
              FROM "Products" p WHERE p."Id" = %s
        """, (CONSUMPTION_WINDOW_DAYS, ORDER_STATUS_CANCELLED, product_id))
        r = cur.fetchone()
    if not r:
        raise HTTPException(status_code=404, detail=f"Produit avec ID {product_id} introuvable")

    unit_cost = float(r["CostPrice"] or 0) or 10.0  # default when the product has no cost yet
    f = reorder_figures(float(r["consumption"]), unit_cost)
    return OptimizationResponse(
        product_id=product_id, product_code=r["Code"], product_name=r["Name"],
        current_stock=round(float(r["stock"]), 2), reorder_point=round(f["reorder_point"], 2),
        eoq=round(f["eoq"], 2), average_daily_demand=round(f["average_daily_demand"], 2),
        lead_time_days=LEAD_TIME_DAYS, safety_stock=round(f["safety_stock"], 2), unit_cost=round(unit_cost, 2))


if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=int(os.getenv("PORT", "8000")))
