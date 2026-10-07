"""
Proof tests for the WMS stock integrity layer (backend-csharp/WMS.Data/Sql/stock_integrity.sql).

They run against a REAL PostgreSQL (no mocks). Point them at a server with:

    WMS_TEST_DSN="host=localhost port=5432 user=postgres password=... dbname=postgres" pytest tests/db -v

Each test creates and drops its own throw-away database.
"""
import os
import threading
import uuid
from pathlib import Path

import psycopg
import pytest

ROOT = Path(__file__).resolve().parents[2]
SQL_FILE = ROOT / "backend-csharp" / "WMS.Data" / "Sql" / "stock_integrity.sql"
SCHEMA = Path(__file__).with_name("schema_min.sql")
ADMIN_DSN = os.environ.get("WMS_TEST_DSN", "host=/tmp port=5433 user=postgres dbname=postgres")

OUTBOUND, INBOUND, TRANSFER, ADJUSTMENT = 1, 0, 2, 3


def _dsn_for(dbname):
    parts = [p for p in ADMIN_DSN.split() if not p.startswith("dbname=")]
    return " ".join(parts + [f"dbname={dbname}"])


@pytest.fixture()
def db():
    name = "wms_t_" + uuid.uuid4().hex[:10]
    with psycopg.connect(ADMIN_DSN, autocommit=True) as admin:
        admin.execute(f'CREATE DATABASE "{name}"')
    dsn = _dsn_for(name)
    with psycopg.connect(dsn, autocommit=True) as c:
        c.execute(SCHEMA.read_text())
    yield dsn
    with psycopg.connect(ADMIN_DSN, autocommit=True) as admin:
        admin.execute(f'DROP DATABASE "{name}" WITH (FORCE)')


def apply_layer(dsn):
    with psycopg.connect(dsn, autocommit=True) as c:
        c.execute(SQL_FILE.read_text())


def new_stock(dsn, qty, cost=10, product=1, wh=1, loc=1, reserved=0):
    with psycopg.connect(dsn, autocommit=True) as c:
        sid = c.execute(
            'INSERT INTO "Stocks"("ProductId","WarehouseId","LocationId","Quantity","ReservedQuantity",'
            '"AverageCost","ReorderPoint","LastUpdated") VALUES (%s,%s,%s,%s,%s,%s,0,now()) RETURNING "Id"',
            (product, wh, loc, qty, reserved, cost)).fetchone()[0]
        if qty:
            # opening receipt so the ledger starts coherent with the stock row
            c.execute('INSERT INTO "StockMovements"("StockId","Type","Quantity","UnitCost","CreatedAt","Delta")'
                      ' VALUES (%s,0,%s,%s,now(),%s)', (sid, qty, cost, qty))
        return sid


def one(dsn, sql, *params):
    with psycopg.connect(dsn, autocommit=True) as c:
        return c.execute(sql, params).fetchone()


def drift(dsn):
    with psycopg.connect(dsn, autocommit=True) as c:
        return c.execute("SELECT * FROM wms_stock_drift").fetchall()


# --------------------------------------------------------------------------
# The headline guarantee
# --------------------------------------------------------------------------
@pytest.mark.parametrize("workers", [20, 100])
def test_parallel_shipments_never_oversell(db, workers):
    """`workers` threads each try to ship 1 unit of a stock that has only 5."""
    apply_layer(db)
    sid = new_stock(db, 5)
    ok, refused, errors = [], [], []
    barrier = threading.Barrier(workers)

    def ship():
        try:
            with psycopg.connect(db, autocommit=False) as c:
                barrier.wait()
                try:
                    c.execute("SELECT wms_stock_issue(%s, 1, %s, 'T', 'race')", (sid, OUTBOUND))
                    c.commit()
                    ok.append(1)
                except psycopg.errors.Error as e:
                    c.rollback()
                    (refused if e.sqlstate == "WMS01" else errors).append(e)
        except Exception as e:  # noqa: BLE001
            errors.append(e)

    ts = [threading.Thread(target=ship) for _ in range(workers)]
    [t.start() for t in ts]
    [t.join() for t in ts]

    assert not errors, errors
    assert len(ok) == 5, f"expected exactly 5 successful shipments, got {len(ok)}"
    assert len(refused) == workers - 5
    assert one(db, 'SELECT "Quantity" FROM "Stocks" WHERE "Id"=%s', sid)[0] == 0
    assert drift(db) == []


def test_old_read_check_write_pattern_oversells_proving_the_test_is_meaningful(db):
    """Control experiment: the ORIGINAL C# logic (read, check, subtract) loses updates.
    Without this, a green concurrency test above could just mean 'the test is too gentle'."""
    apply_layer(db)  # constraint disabled below to show what the old code did to the data
    with psycopg.connect(db, autocommit=True) as c:
        c.execute('ALTER TABLE "Stocks" DROP CONSTRAINT ck_stocks_quantity_nonneg')
        c.execute('ALTER TABLE "Stocks" DROP CONSTRAINT ck_stocks_reserved_range')
    sid = new_stock(db, 5)
    workers = 20
    barrier = threading.Barrier(workers)
    shipped = []

    def old_ship():
        with psycopg.connect(db, autocommit=False) as c:
            qty = c.execute('SELECT "Quantity" FROM "Stocks" WHERE "Id"=%s', (sid,)).fetchone()[0]
            barrier.wait()                      # everybody has read 5
            if qty >= 1:                        # ...everybody passes the check
                c.execute('UPDATE "Stocks" SET "Quantity"=%s WHERE "Id"=%s', (qty - 1, sid))
                c.commit()
                shipped.append(1)

    ts = [threading.Thread(target=old_ship) for _ in range(workers)]
    [t.start() for t in ts]
    [t.join() for t in ts]
    assert len(shipped) == workers  # 20 shipments of a 5-unit stock "succeeded"


# --------------------------------------------------------------------------
# Constraints & ledger
# --------------------------------------------------------------------------
def test_constraints_refuse_negative_stock_even_for_raw_sql(db):
    apply_layer(db)
    sid = new_stock(db, 3)
    with psycopg.connect(db, autocommit=True) as c:
        with pytest.raises(psycopg.errors.CheckViolation):
            c.execute('UPDATE "Stocks" SET "Quantity" = -1 WHERE "Id"=%s', (sid,))
        with pytest.raises(psycopg.errors.CheckViolation):
            c.execute('UPDATE "Stocks" SET "ReservedQuantity" = 4 WHERE "Id"=%s', (sid,))


def test_issue_respects_reserved_quantity(db):
    apply_layer(db)
    sid = new_stock(db, 10, reserved=8)
    with psycopg.connect(db, autocommit=True) as c:
        with pytest.raises(psycopg.Error) as e:
            c.execute("SELECT wms_stock_issue(%s, 3, 1, 'T', '')", (sid,))
        assert e.value.sqlstate == "WMS01"
        c.execute("SELECT wms_stock_issue(%s, 2, 1, 'T', '')", (sid,))
    assert drift(db) == []


def test_insufficient_error_carries_machine_readable_detail(db):
    apply_layer(db)
    sid = new_stock(db, 2)
    with psycopg.connect(db, autocommit=True) as c:
        with pytest.raises(psycopg.Error) as e:
            c.execute("SELECT wms_stock_issue(%s, 5, 1, 'T', '')", (sid,))
    assert e.value.sqlstate == "WMS01"
    assert '"required":5' in e.value.diag.message_detail and '"available":2' in e.value.diag.message_detail


@pytest.mark.parametrize("bad", [0, -1])
def test_invalid_quantities_rejected(db, bad):
    apply_layer(db)
    sid = new_stock(db, 5)
    with psycopg.connect(db, autocommit=True) as c:
        for sql in ("SELECT wms_stock_issue(%s, %s, 1, 'T', '')",
                    "SELECT wms_stock_receive(%s, %s, 1, 0, 'T', '')"):
            with pytest.raises(psycopg.Error) as e:
                c.execute(sql, (sid, bad))
            assert e.value.sqlstate == "WMS02"


def test_cump_incremental_matches_definition_and_ledger_reconciles(db):
    apply_layer(db)
    sid = new_stock(db, 10, cost=10)
    with psycopg.connect(db, autocommit=True) as c:
        c.execute("SELECT wms_stock_receive(%s, 30, 14, 0, 'PO-1', '')", (sid,))   # (10*10+30*14)/40 = 13
        assert c.execute('SELECT "AverageCost" FROM "Stocks" WHERE "Id"=%s', (sid,)).fetchone()[0] == 13
        c.execute("SELECT wms_stock_issue(%s, 20, 1, 'CMD', '')", (sid,))          # issuing keeps the average
        assert c.execute('SELECT "AverageCost","Quantity" FROM "Stocks" WHERE "Id"=%s', (sid,)).fetchone() == (13, 20)
        c.execute("SELECT wms_stock_receive(%s, 20, 7, 0, 'PO-2', '')", (sid,))    # (20*13+20*7)/40 = 10
        assert c.execute('SELECT "AverageCost" FROM "Stocks" WHERE "Id"=%s', (sid,)).fetchone()[0] == 10
    assert drift(db) == []


def test_cump_no_rounding_drift_over_many_receipts(db):
    apply_layer(db)
    sid = new_stock(db, 0, cost=0)
    with psycopg.connect(db, autocommit=True) as c:
        total_cost = 0
        for i in range(1, 201):
            cost = 3 + (i % 7) / 3          # awkward thirds
            c.execute("SELECT wms_stock_receive(%s, 7, %s::numeric, 0, 'P', '')", (sid, cost))
            total_cost += 7 * cost
        avg = float(c.execute('SELECT "AverageCost" FROM "Stocks" WHERE "Id"=%s', (sid,)).fetchone()[0])
    assert abs(avg - total_cost / 1400) < 0.001


def test_return_at_current_cost_does_not_change_average(db):
    apply_layer(db)
    sid = new_stock(db, 10, cost=12.5)
    with psycopg.connect(db, autocommit=True) as c:
        c.execute("SELECT wms_stock_receive(%s, 3, NULL, 0, 'RMA', '')", (sid,))
        assert c.execute('SELECT "AverageCost","Quantity" FROM "Stocks" WHERE "Id"=%s', (sid,)).fetchone() == (12.5, 13)
    assert drift(db) == []


def test_adjust_up_down_and_refuse_below_zero(db):
    apply_layer(db)
    sid = new_stock(db, 4)
    with psycopg.connect(db, autocommit=True) as c:
        assert c.execute("SELECT wms_stock_adjust(%s, 6, 'found')", (sid,)).fetchone()[0] == 10
        assert c.execute("SELECT wms_stock_adjust(%s, -10, 'broken')", (sid,)).fetchone()[0] == 0
        with pytest.raises(psycopg.Error) as e:
            c.execute("SELECT wms_stock_adjust(%s, -1, 'x')", (sid,))
        assert e.value.sqlstate == "WMS01"
        with pytest.raises(psycopg.Error) as e:
            c.execute("SELECT wms_stock_adjust(%s, 0, 'x')", (sid,))
        assert e.value.sqlstate == "WMS02"
    assert drift(db) == []


def test_failed_operation_leaves_no_ledger_row(db):
    """Atomicity: when the issue is refused, no half-written movement may remain."""
    apply_layer(db)
    sid = new_stock(db, 1)
    before = one(db, 'SELECT count(*) FROM "StockMovements"')[0]
    with psycopg.connect(db, autocommit=True) as c:
        with pytest.raises(psycopg.Error):
            c.execute("SELECT wms_stock_issue(%s, 9, 1, 'T', '')", (sid,))
    assert one(db, 'SELECT count(*) FROM "StockMovements"')[0] == before


def test_multi_line_shipment_is_all_or_nothing(db):
    """Line 1 fits, line 2 does not -> the whole shipment (one tx) must roll back."""
    apply_layer(db)
    a, b = new_stock(db, 5, product=1, loc=1), new_stock(db, 1, product=2, loc=2)
    with psycopg.connect(db, autocommit=False) as c:
        c.execute("SELECT wms_stock_issue(%s, 3, 1, 'CMD', '')", (a,))
        with pytest.raises(psycopg.Error):
            c.execute("SELECT wms_stock_issue(%s, 2, 1, 'CMD', '')", (b,))
        c.rollback()
    assert one(db, 'SELECT "Quantity" FROM "Stocks" WHERE "Id"=%s', a)[0] == 5
    assert drift(db) == []


# --------------------------------------------------------------------------
# Transfers, get_or_create, uniqueness
# --------------------------------------------------------------------------
def test_transfer_moves_stock_and_cost_and_reconciles(db):
    apply_layer(db)
    src = new_stock(db, 10, cost=8, wh=1, loc=1)
    dst = new_stock(db, 10, cost=4, wh=2, loc=2)
    with psycopg.connect(db, autocommit=True) as c:
        c.execute("SELECT wms_stock_transfer(%s,%s,10,'TRF')", (src, dst))
        assert c.execute('SELECT "Quantity" FROM "Stocks" WHERE "Id"=%s', (src,)).fetchone()[0] == 0
        assert c.execute('SELECT "Quantity","AverageCost" FROM "Stocks" WHERE "Id"=%s', (dst,)).fetchone() == (20, 6)
        with pytest.raises(psycopg.Error) as e:
            c.execute("SELECT wms_stock_transfer(%s,%s,1,'TRF')", (src, dst))
        assert e.value.sqlstate == "WMS01"
        with pytest.raises(psycopg.Error) as e:
            c.execute("SELECT wms_stock_transfer(%s,%s,1,'TRF')", (src, src))
        assert e.value.sqlstate == "WMS04"
    assert drift(db) == []


def test_opposite_transfers_do_not_deadlock_and_conserve_total(db):
    apply_layer(db)
    a, b = new_stock(db, 500, wh=1, loc=1), new_stock(db, 500, wh=2, loc=2)
    errors = []

    def run(src, dst):
        try:
            with psycopg.connect(db, autocommit=False) as c:
                for _ in range(100):
                    c.execute("SELECT wms_stock_transfer(%s,%s,1,'TRF')", (src, dst))
                    c.commit()
        except Exception as e:  # noqa: BLE001
            errors.append(e)

    ts = [threading.Thread(target=run, args=(a, b)) for _ in range(4)] + \
         [threading.Thread(target=run, args=(b, a)) for _ in range(4)]
    [t.start() for t in ts]
    [t.join() for t in ts]
    assert not errors, errors
    assert one(db, 'SELECT sum("Quantity") FROM "Stocks"')[0] == 1000
    assert drift(db) == []


def test_get_or_create_is_race_free_with_null_location(db):
    apply_layer(db)
    ids, errors = [], []
    barrier = threading.Barrier(30)

    def go():
        try:
            with psycopg.connect(db, autocommit=True) as c:
                barrier.wait()
                ids.append(c.execute("SELECT wms_stock_get_or_create(7, 1, NULL)").fetchone()[0])
        except Exception as e:  # noqa: BLE001
            errors.append(e)

    ts = [threading.Thread(target=go) for _ in range(30)]
    [t.start() for t in ts]
    [t.join() for t in ts]
    assert not errors, errors
    assert len(set(ids)) == 1
    assert one(db, 'SELECT count(*) FROM "Stocks" WHERE "ProductId"=7')[0] == 1


def test_duplicate_null_location_rows_are_now_impossible(db):
    apply_layer(db)
    with psycopg.connect(db, autocommit=True) as c:
        c.execute('INSERT INTO "Stocks"("ProductId","WarehouseId","LocationId","Quantity","ReservedQuantity",'
                  '"AverageCost","ReorderPoint","LastUpdated") VALUES (1,1,NULL,0,0,0,0,now())')
        with pytest.raises(psycopg.errors.UniqueViolation):
            c.execute('INSERT INTO "Stocks"("ProductId","WarehouseId","LocationId","Quantity","ReservedQuantity",'
                      '"AverageCost","ReorderPoint","LastUpdated") VALUES (1,1,NULL,0,0,0,0,now())')


# --------------------------------------------------------------------------
# Document numbers
# --------------------------------------------------------------------------
def test_numbers_are_unique_under_concurrency(db):
    apply_layer(db)
    out, errors = [], []
    barrier = threading.Barrier(40)

    def go(kind):
        try:
            with psycopg.connect(db, autocommit=True) as c:
                barrier.wait()
                out.append(c.execute("SELECT wms_next_number(%s)", (kind,)).fetchone()[0])
        except Exception as e:  # noqa: BLE001
            errors.append(e)

    ts = [threading.Thread(target=go, args=("order",)) for _ in range(40)]
    [t.start() for t in ts]
    [t.join() for t in ts]
    assert not errors and len(out) == len(set(out)) == 40
    assert all(n.startswith("CMD-") for n in out)


# --------------------------------------------------------------------------
# Migration behaviour on dirty, pre-existing data
# --------------------------------------------------------------------------
def test_migration_on_legacy_data_merges_duplicates_aligns_ledger_and_is_idempotent(db):
    with psycopg.connect(db, autocommit=True) as c:
        # legacy dirty data: two rows for the same (product, wh, NULL location) + an unsigned adjustment
        c.execute('INSERT INTO "Stocks"("ProductId","WarehouseId","LocationId","Quantity","ReservedQuantity",'
                  '"AverageCost","ReorderPoint","LastUpdated") VALUES (1,1,NULL,10,0,2,0,now()),(1,1,NULL,30,0,4,0,now())')
        c.execute('INSERT INTO "StockMovements"("StockId","Type","Quantity","UnitCost","CreatedAt")'
                  " VALUES (1,0,10,2,now()),(2,0,40,4,now()),(2,1,10,4,now()),(2,3,5,4,now())")
        # historical orders/invoices so sequences must start above them
        c.execute("INSERT INTO \"Orders\"(\"OrderNumber\",\"Status\",\"OrderDate\") VALUES ('CMD-20251203-0042',0,now())")
        c.execute("INSERT INTO \"Invoices\"(\"InvoiceNumber\") VALUES ('FAC-20251203-0007')")

    apply_layer(db)
    apply_layer(db)  # second run must be a no-op, not an error

    with psycopg.connect(db, autocommit=True) as c:
        rows = c.execute('SELECT "Id","Quantity","AverageCost" FROM "Stocks"').fetchall()
        assert len(rows) == 1 and rows[0][1] == 40 and rows[0][2] == 3.5   # (10*2+30*4)/40
        assert c.execute("SELECT count(*) FROM wms_stock_drift").fetchone()[0] == 0
        n = c.execute("SELECT wms_next_number('order')").fetchone()[0]
        assert int(n.rsplit("-", 1)[1]) > 42
        n = c.execute("SELECT wms_next_number('invoice')").fetchone()[0]
        assert int(n.rsplit("-", 1)[1]) > 7


def test_property_random_operations_keep_ledger_equal_to_stock(db):
    """Random mix of receive/issue/adjust/transfer from many threads; invariants must hold at the end."""
    import random
    apply_layer(db)
    ids = [new_stock(db, 50, product=p, loc=p) for p in range(1, 6)]

    def worker(seed):
        rnd = random.Random(seed)
        with psycopg.connect(db, autocommit=True) as c:
            for _ in range(150):
                s, t = rnd.sample(ids, 2)
                q = rnd.randint(1, 8)
                try:
                    op = rnd.choice(["in", "out", "adj+", "adj-", "trf"])
                    if op == "in":
                        c.execute("SELECT wms_stock_receive(%s,%s,%s,0,'R','')", (s, q, rnd.randint(1, 20)))
                    elif op == "out":
                        c.execute("SELECT wms_stock_issue(%s,%s,1,'R','')", (s, q))
                    elif op == "adj+":
                        c.execute("SELECT wms_stock_adjust(%s,%s,'r')", (s, q))
                    elif op == "adj-":
                        c.execute("SELECT wms_stock_adjust(%s,%s,'r')", (s, -q))
                    else:
                        c.execute("SELECT wms_stock_transfer(%s,%s,%s,'R')", (s, t, q))
                except psycopg.Error as e:
                    assert e.sqlstate == "WMS01", e   # only business refusals are allowed

    ts = [threading.Thread(target=worker, args=(i,)) for i in range(8)]
    [t.start() for t in ts]
    [t.join() for t in ts]
    assert drift(db) == []
    assert one(db, 'SELECT min("Quantity") FROM "Stocks"')[0] >= 0
