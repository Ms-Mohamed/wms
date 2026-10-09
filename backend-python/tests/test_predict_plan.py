"""The covering index must be usable by the /predict query (index-only probe instead of scanning Orders).

Whether the planner PICKS it depends on table size (measured: at 600k orders it does and the query is ~3x faster;
at 300k it still prefers a parallel scan, which is also fine). So this test proves the index fits the query by
forbidding sequential scans, not that the planner always chooses it."""
import uuid

import psycopg2

from conftest import ADMIN, SCHEMA


def test_predict_query_can_use_the_covering_index():
    name = "wms_plan_" + uuid.uuid4().hex[:8]
    admin = psycopg2.connect(dbname="postgres", **ADMIN)
    admin.autocommit = True
    admin.cursor().execute(f'CREATE DATABASE "{name}"')
    try:
        c = psycopg2.connect(dbname=name, **ADMIN)
        c.autocommit = True
        cur = c.cursor()
        cur.execute(SCHEMA.read_text())
        cur.execute("""INSERT INTO "Orders"("OrderNumber","Status","OrderDate","TotalAmount")
                       SELECT 'CMD-' || g, 4, now() - (random() * 300 || ' days')::interval, 10
                         FROM generate_series(1, 300000) g""")
        # product 1 is the popular one: ~8% of all lines; the others share the rest
        cur.execute("""INSERT INTO "OrderItems"("OrderId","ProductId","Quantity")
                       SELECT o, CASE WHEN random() < 0.08 THEN 1 ELSE 2 + floor(random() * 5000)::int END, 1 + floor(random() * 9)
                         FROM generate_series(1, 300000) o""")
        cur.execute('VACUUM ANALYZE "Orders"')
        cur.execute('VACUUM ANALYZE "OrderItems"')

        import main
        cur.execute("SET enable_seqscan = off")
        cur.execute("EXPLAIN (FORMAT TEXT) " + main.PREDICT_SQL, (1, main.ORDER_STATUS_CANCELLED))
        plan = "\n".join(r[0] for r in cur.fetchall())
        assert 'Index Only Scan using ix_orders_id_cover' in plan, plan
        c.close()
    finally:
        admin.cursor().execute(f'DROP DATABASE "{name}" WITH (FORCE)')
        admin.close()
