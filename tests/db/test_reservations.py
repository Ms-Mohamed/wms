"""
Proof tests for reservations and partial shipments
(backend-csharp/WMS.Data/Sql/stock_reservations.sql). Real PostgreSQL, no mocks.
"""
import threading

import psycopg
import pytest

from test_stock_integrity import ROOT, db, apply_layer, new_stock  # noqa: F401  (db is a fixture)

RES_SQL = ROOT / "backend-csharp" / "WMS.Data" / "Sql" / "stock_reservations.sql"
PENDING, SHIPPED, CANCELLED, PARTIAL = 0, 3, 5, 6


@pytest.fixture()
def layer(db):  # noqa: F811
    apply_layer(db)
    with psycopg.connect(db, autocommit=True) as c:
        c.execute(RES_SQL.read_text())
    return db


def new_order(dsn, lines, product=1, wh=1):
    """lines: list of quantities, one order item each (same product). Returns (order_id, [item_ids])."""
    with psycopg.connect(dsn, autocommit=True) as c:
        oid = c.execute(
            'INSERT INTO "Orders"("OrderNumber","Status","OrderDate") VALUES (%s,0,now()) RETURNING "Id"',
            ("CMD-" + str(_c()),)).fetchone()[0]
        ids = [c.execute('INSERT INTO "OrderItems"("OrderId","ProductId","WarehouseId","Quantity") VALUES (%s,%s,%s,%s) RETURNING "Id"',
                         (oid, product, wh, q)).fetchone()[0] for q in lines]
    return oid, ids


_n = [0]
def _c():
    _n[0] += 1
    return _n[0]


def one(dsn, sql, *args):
    with psycopg.connect(dsn, autocommit=True) as c:
        return c.execute(sql, args).fetchone()


def state(dsn, stock_id):
    q, r = one(dsn, 'SELECT "Quantity","ReservedQuantity" FROM "Stocks" WHERE "Id"=%s', stock_id)
    return float(q), float(r)


def assert_clean(dsn):
    assert one(dsn, "SELECT count(*) FROM wms_stock_drift")[0] == 0
    assert one(dsn, "SELECT count(*) FROM wms_reservation_drift")[0] == 0


def sqlstate(dsn, sql, *args):
    try:
        with psycopg.connect(dsn, autocommit=True) as c:
            c.execute(sql, args)
    except psycopg.errors.Error as e:
        return e.sqlstate
    return None


def test_layer_is_idempotent(layer):
    with psycopg.connect(layer, autocommit=True) as c:
        c.execute(RES_SQL.read_text())
        c.execute(RES_SQL.read_text())


def test_reserve_holds_units_and_blocks_others(layer):
    sid = new_stock(layer, 10)
    oid, _ = new_order(layer, [8])
    assert float(one(layer, "SELECT wms_order_reserve(%s)", oid)[0]) == 8
    assert state(layer, sid) == (10, 8)
    # another caller can only take what is free
    assert sqlstate(layer, "SELECT wms_stock_issue(%s,5,1,'x','x')", sid) == "WMS01"
    assert sqlstate(layer, "SELECT wms_stock_issue(%s,2,1,'x','x')", sid) is None
    assert_clean(layer)


def test_reserve_is_all_or_nothing(layer):
    a = new_stock(layer, 5, product=1, loc=1)
    b = new_stock(layer, 1, product=2, loc=2)
    with psycopg.connect(layer, autocommit=True) as c:
        oid = c.execute('INSERT INTO "Orders"("OrderNumber","Status","OrderDate") VALUES (\'CMD-AON\',0,now()) RETURNING "Id"').fetchone()[0]
        c.execute('INSERT INTO "OrderItems"("OrderId","ProductId","WarehouseId","Quantity") VALUES (%s,1,1,4),(%s,2,1,3)', (oid, oid))
    # the C# service runs this inside one transaction; emulate it
    with psycopg.connect(layer) as c:
        with pytest.raises(psycopg.errors.Error) as e:
            c.execute("SELECT wms_order_reserve(%s)", (oid,))
        assert e.value.sqlstate == "WMS01"
        c.rollback()
    assert state(layer, a) == (5, 0) and state(layer, b) == (1, 0)
    assert_clean(layer)


def test_parallel_reservations_never_oversubscribe(layer):
    sid = new_stock(layer, 10)
    orders = [new_order(layer, [1])[0] for _ in range(30)]
    ok, no = [], []

    def go(o):
        try:
            with psycopg.connect(layer) as c:
                c.execute("SELECT wms_order_reserve(%s)", (o,))
                c.commit()
            ok.append(o)
        except psycopg.errors.Error as e:
            no.append(e.sqlstate)

    ts = [threading.Thread(target=go, args=(o,)) for o in orders]
    [t.start() for t in ts]; [t.join() for t in ts]
    assert len(ok) == 10 and no == ["WMS01"] * 20
    assert state(layer, sid) == (10, 10)
    assert_clean(layer)


def test_partial_shipment_then_completion(layer):
    sid = new_stock(layer, 20)
    oid, (item,) = new_order(layer, [10])
    one(layer, "SELECT wms_order_reserve(%s)", oid)
    assert one(layer, "SELECT wms_order_ship_line(%s,%s,4,'CMD-1')", item, sid)[0] == PARTIAL
    assert state(layer, sid) == (16, 6)               # 4 shipped, 6 still held for this order
    assert one(layer, 'SELECT "Status" FROM "Orders" WHERE "Id"=%s', oid)[0] == PARTIAL
    assert one(layer, "SELECT wms_order_ship_line(%s,%s,6,'CMD-1')", item, sid)[0] == SHIPPED
    assert state(layer, sid) == (10, 0)
    assert float(one(layer, 'SELECT "ShippedQuantity" FROM "OrderItems" WHERE "Id"=%s', item)[0]) == 10
    assert one(layer, 'SELECT "ShippedDate" IS NOT NULL FROM "Orders" WHERE "Id"=%s', oid)[0]
    assert_clean(layer)


def test_ship_without_reservation_still_uses_free_stock(layer):
    sid = new_stock(layer, 5)
    oid, (item,) = new_order(layer, [3])
    assert one(layer, "SELECT wms_order_ship_line(%s,%s,3,'CMD-2')", item, sid)[0] == SHIPPED
    assert state(layer, sid) == (2, 0)
    assert_clean(layer)


def test_cannot_ship_units_reserved_by_someone_else(layer):
    sid = new_stock(layer, 5)
    o1, _ = new_order(layer, [4])
    o2, (i2,) = new_order(layer, [3])
    one(layer, "SELECT wms_order_reserve(%s)", o1)
    assert sqlstate(layer, "SELECT wms_order_ship_line(%s,%s,3,'x')", i2, sid) == "WMS01"
    assert state(layer, sid) == (5, 4)
    assert_clean(layer)


def test_over_shipping_a_line_is_rejected(layer):
    sid = new_stock(layer, 20)
    oid, (item,) = new_order(layer, [5])
    one(layer, "SELECT wms_order_ship_line(%s,%s,3,'x')", item, sid)
    assert sqlstate(layer, "SELECT wms_order_ship_line(%s,%s,3,'x')", item, sid) == "WMS06"
    assert state(layer, sid) == (17, 0)
    assert_clean(layer)


def test_parallel_shipments_of_one_line_cannot_exceed_it(layer):
    sid = new_stock(layer, 50)
    oid, (item,) = new_order(layer, [5])
    results = []

    def go():
        results.append(sqlstate(layer, "SELECT wms_order_ship_line(%s,%s,1,'x')", item, sid))

    ts = [threading.Thread(target=go) for _ in range(20)]
    [t.start() for t in ts]; [t.join() for t in ts]
    assert results.count(None) == 5
    assert set(r for r in results if r) <= {"WMS05", "WMS06"}   # rest: order already complete / line full
    assert state(layer, sid) == (45, 0)
    assert one(layer, 'SELECT "Status" FROM "Orders" WHERE "Id"=%s', oid)[0] == SHIPPED
    assert_clean(layer)


def test_wrong_product_stock_is_rejected(layer):
    other = new_stock(layer, 5, product=2, loc=9)
    oid, (item,) = new_order(layer, [1], product=1)
    assert sqlstate(layer, "SELECT wms_order_ship_line(%s,%s,1,'x')", item, other) == "WMS07"


def test_cancel_releases_reservations(layer):
    sid = new_stock(layer, 10)
    oid, _ = new_order(layer, [7])
    one(layer, "SELECT wms_order_reserve(%s)", oid)
    one(layer, "SELECT wms_order_cancel(%s)", oid)
    assert state(layer, sid) == (10, 0)
    assert one(layer, 'SELECT "Status" FROM "Orders" WHERE "Id"=%s', oid)[0] == CANCELLED
    one(layer, "SELECT wms_order_cancel(%s)", oid)       # idempotent
    assert sqlstate(layer, "SELECT wms_order_reserve(%s)", oid) == "WMS05"
    assert_clean(layer)


def test_cannot_cancel_after_a_shipment(layer):
    sid = new_stock(layer, 10)
    oid, (item,) = new_order(layer, [4])
    one(layer, "SELECT wms_order_reserve(%s)", oid)
    one(layer, "SELECT wms_order_ship_line(%s,%s,1,'x')", item, sid)
    assert sqlstate(layer, "SELECT wms_order_cancel(%s)", oid) == "WMS08"
    assert_clean(layer)


def test_reservation_drift_view_catches_manual_tampering(layer):
    sid = new_stock(layer, 10)
    with psycopg.connect(layer, autocommit=True) as c:
        c.execute('UPDATE "Stocks" SET "ReservedQuantity"=3 WHERE "Id"=%s', (sid,))
    assert one(layer, "SELECT count(*) FROM wms_reservation_drift")[0] == 1
