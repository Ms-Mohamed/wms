import math
import threading
import time
from datetime import date, datetime, timedelta, timezone

import jwt
import pytest

from conftest import SECRET


# ---------------------------------------------------------------------------------- pure maths
def test_linear_fit_recovers_a_perfect_line():
    import main
    a, b = main.linear_fit([10, 12, 14, 16])
    assert a == pytest.approx(10) and b == pytest.approx(2)


def test_linear_fit_flat_and_single_point():
    import main
    assert main.linear_fit([5, 5, 5]) == pytest.approx((5, 0))
    assert main.linear_fit([7]) == pytest.approx((7, 0))


def test_fill_missing_months_counts_gaps_as_zero():
    import main
    rows = [(date(2026, 1, 1), 10), (date(2026, 4, 1), 40)]
    assert main.fill_missing_months(rows) == [10, 0, 0, 40]


def test_month_add_crosses_year_boundary():
    import main
    assert main.month_add(date(2026, 11, 1), 3) == date(2027, 2, 1)


def test_forecast_never_negative():
    import main
    rows = [(date(2026, 1, 1), 30), (date(2026, 2, 1), 20), (date(2026, 3, 1), 10)]
    out = main.forecast_next_months(rows)
    assert [f["mois"] for f in out] == ["2026-04", "2026-05", "2026-06"]
    assert out[0]["prediction"] == 0 and all(f["prediction"] >= 0 for f in out)


def test_eoq_and_reorder_point_match_the_textbook_formulas():
    import main
    f = main.reorder_figures(total_consumption=900, unit_cost=20)   # 10 / day
    assert f["average_daily_demand"] == pytest.approx(10)
    assert f["safety_stock"] == pytest.approx(1.5 * 10 * 7)
    assert f["reorder_point"] == pytest.approx(10 * 7 + 1.5 * 10 * 7)
    assert f["eoq"] == pytest.approx(math.sqrt(2 * 3650 * 50 / (20 * 0.20)))


def test_eoq_fallback_when_no_demand():
    import main
    assert main.reorder_figures(0, 20)["eoq"] == 10.0


# ---------------------------------------------------------------------------------- data helpers
def add_product(conn, code, cost=20):
    cur = conn.cursor()
    cur.execute('INSERT INTO "Products"("Code","Name","CostPrice") VALUES (%s,%s,%s) RETURNING "Id"', (code, code, cost))
    return cur.fetchone()[0]


def add_sale(conn, pid, qty, when, status=4, total=100):
    cur = conn.cursor()
    cur.execute('INSERT INTO "Orders"("OrderNumber","Status","OrderDate","TotalAmount") VALUES (%s,%s,%s,%s) RETURNING "Id"',
                (f"O{pid}-{when.timestamp()}-{qty}", status, when, total))
    oid = cur.fetchone()[0]
    cur.execute('INSERT INTO "OrderItems"("OrderId","ProductId","Quantity") VALUES (%s,%s,%s)', (oid, pid, qty))


def month_start(offset):
    now = datetime.now(timezone.utc)
    idx = now.year * 12 + now.month - 1 - offset
    return datetime(idx // 12, idx % 12 + 1, 10, 12, tzinfo=timezone.utc)


# ---------------------------------------------------------------------------------- auth
def test_endpoints_require_a_token(client):
    assert client.get("/stats").status_code == 401
    assert client.get("/stats", headers={"Authorization": "Bearer nope"}).status_code == 401


def test_token_signed_with_another_secret_or_expired_is_refused(client):
    bad = jwt.encode({"exp": int(time.time()) + 60}, "another-secret-another-secret-another-secret", algorithm="HS256")
    old = jwt.encode({"exp": int(time.time()) - 60}, SECRET, algorithm="HS256")
    for t in (bad, old):
        assert client.get("/stats", headers={"Authorization": f"Bearer {t}"}).status_code == 401


def test_health_is_public_and_checks_the_database(client):
    r = client.get("/health")
    assert r.status_code == 200 and r.json() == {"status": "ok"}


# ---------------------------------------------------------------------------------- endpoints
def test_stats_values_stock_at_average_cost(client, auth, conn):
    p = add_product(conn, "STAT1", cost=999)   # CostPrice must NOT be used for valuation
    cur = conn.cursor()
    cur.execute('INSERT INTO "Stocks"("ProductId","Quantity","AverageCost","ReorderPoint") VALUES (%s,10,2.5,20)', (p,))
    r = client.get("/stats", headers=auth).json()
    assert r["low_stock_count"] >= 1 and r["total_products"] >= 1
    cur.execute('SELECT SUM("Quantity"*"AverageCost") FROM "Stocks"')
    assert r["total_stock_value"] == pytest.approx(float(cur.fetchone()[0]), abs=0.01)


def test_predict_trend_with_enough_history(client, auth, conn):
    p = add_product(conn, "FC1")
    for i, qty in enumerate([10, 20, 30, 40]):          # +10 / month, oldest first
        add_sale(conn, p, qty, month_start(3 - i))
    body = client.get(f"/predict/{p}", headers=auth).json()
    assert [f["prediction"] for f in body["forecasts"]] == pytest.approx([50, 60, 70], abs=0.01)
    assert body["message"] is None


def test_predict_with_too_little_history_says_so(client, auth, conn):
    p = add_product(conn, "FC2")
    add_sale(conn, p, 5, month_start(1))
    body = client.get(f"/predict/{p}", headers=auth).json()
    assert body["forecasts"] == [] and "minimum 3 mois" in body["message"]


def test_predict_ignores_cancelled_orders(client, auth, conn):
    p = add_product(conn, "FC3")
    for i, qty in enumerate([10, 20, 30]):
        add_sale(conn, p, qty, month_start(2 - i))
    add_sale(conn, p, 9999, month_start(0), status=5)       # cancelled: must not count
    out = client.get(f"/predict/{p}", headers=auth).json()["forecasts"]
    assert out[0]["prediction"] == pytest.approx(40, abs=0.01)


def test_unknown_product_is_404_and_does_not_leak_a_connection(client, auth, app_module):
    for _ in range(30):                                      # pool max is 8: a leak would exhaust it
        assert client.get("/predict/999999", headers=auth).status_code == 404
        assert client.get("/optimize/999999", headers=auth).status_code == 404
    assert client.get("/stats", headers=auth).status_code == 200


def test_optimize_matches_formulas(client, auth, conn):
    p = add_product(conn, "OPT1", cost=20)
    add_sale(conn, p, 450, datetime.now(timezone.utc) - timedelta(days=10))
    add_sale(conn, p, 450, datetime.now(timezone.utc) - timedelta(days=40))
    cur = conn.cursor()
    cur.execute('INSERT INTO "Stocks"("ProductId","Quantity") VALUES (%s,33)', (p,))
    body = client.get(f"/optimize/{p}", headers=auth).json()
    assert body["average_daily_demand"] == pytest.approx(10)
    assert body["current_stock"] == 33
    assert body["eoq"] == pytest.approx(math.sqrt(2 * 3650 * 50 / (20 * 0.2)), abs=0.01)
    assert body["reorder_point"] == pytest.approx(70 + 105)


def test_low_stock_is_bounded(client, auth, conn):
    cur = conn.cursor()
    for i in range(25):
        p = add_product(conn, f"LOW{i}")
        cur.execute('INSERT INTO "Stocks"("ProductId","Quantity","ReorderPoint") VALUES (%s,1,5)', (p,))
    assert len(client.get("/low-stock?limit=10", headers=auth).json()["items"]) == 10
    assert client.get("/low-stock?limit=100000", headers=auth).status_code == 422


def test_sales_history_excludes_cancelled(client, auth, conn):
    p = add_product(conn, "SH1")
    add_sale(conn, p, 1, month_start(0), status=4, total=100)
    add_sale(conn, p, 1, month_start(0), status=5, total=5000)
    rows = client.get("/sales-history", headers=auth).json()
    assert rows and all(r["total_revenue"] < 5000 for r in rows)


# ---------------------------------------------------------------------------------- safety
def test_service_cannot_write_even_if_code_tried(app_module):
    import psycopg2
    with pytest.raises(psycopg2.errors.ReadOnlySqlTransaction):
        with app_module.read_cursor() as cur:
            cur.execute('UPDATE "Products" SET "Name" = \'x\'')


def test_database_errors_do_not_leak_details(app_module, auth, monkeypatch):
    from fastapi.testclient import TestClient
    import psycopg2

    def boom():
        raise psycopg2.OperationalError("password authentication failed for user postgres")
    monkeypatch.setattr(app_module, "_get_pool", boom)
    c = TestClient(app_module.app, raise_server_exceptions=False)
    r = c.get("/stats", headers=auth)
    assert r.status_code == 503 and "password" not in r.text


def test_concurrent_requests_share_a_small_pool(client, auth):
    results = []

    def go():
        results.append(client.get("/stats", headers=auth).status_code)

    ts = [threading.Thread(target=go) for _ in range(40)]
    [t.start() for t in ts]
    [t.join() for t in ts]
    assert results.count(200) == 40      # a burst of 40 over a pool of 8 queues, it does not fail
