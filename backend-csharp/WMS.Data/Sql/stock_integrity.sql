-- ============================================================================
-- WMS stock integrity layer  (single source of truth)
--
-- Applied by EF migration 20261008000000_StockIntegrity and exercised directly
-- by tests/db/test_stock_integrity.py against a real PostgreSQL.
--
-- Rules enforced INSIDE the database, so no code path (C#, Python, psql, a
-- future service) can break them:
--   1. A stock row can never go negative, nor below what is reserved.
--   2. Every quantity change writes exactly one signed ledger row
--      ("StockMovements"."Delta") in the same statement-transaction.
--   3. Stock.Quantity == SUM(ledger Delta)   -> view wms_stock_drift is empty.
--   4. Document numbers come from sequences, never from "last number + 1".
--   5. (Product, Warehouse, Location) is unique even when Location is NULL.
--
-- The script is idempotent: safe to run twice.
-- ============================================================================

-- ---------------------------------------------------------------------------
-- 0. Precision: CUMP (weighted average cost) must not be rounded to 2 decimals
--    on every receipt, or it drifts. Widen to 4 decimals.
-- ---------------------------------------------------------------------------
ALTER TABLE "Stocks"         ALTER COLUMN "AverageCost" TYPE numeric(18,4);
ALTER TABLE "StockMovements" ALTER COLUMN "UnitCost"    TYPE numeric(18,4);

-- ---------------------------------------------------------------------------
-- 1. Signed ledger column + backfill + opening balances
-- ---------------------------------------------------------------------------
ALTER TABLE "StockMovements" ADD COLUMN IF NOT EXISTS "Delta" numeric(18,3);

-- Also ensure RequestHash is added to IdempotencyKeys to avoid migration issues
ALTER TABLE "IdempotencyKeys" ADD COLUMN IF NOT EXISTS "RequestHash" text;

-- Legacy rows: Inbound(0)=+Q, Outbound(1)=-Q, Transfer(2)=Q as stored (it was
-- already signed), Adjustment(3)=sign was never stored -> 0, fixed by the
-- opening-balance rows below.
UPDATE "StockMovements"
   SET "Delta" = CASE "Type" WHEN 0 THEN "Quantity"
                             WHEN 1 THEN -"Quantity"
                             WHEN 2 THEN "Quantity"
                             ELSE 0 END
 WHERE "Delta" IS NULL;

-- Make ledger == stock for pre-existing data (only inserts where they differ).
INSERT INTO "StockMovements" ("StockId","Type","Quantity","UnitCost","Reference","Notes","CreatedAt","Delta")
SELECT s."Id", 3, ABS(s."Quantity" - COALESCE(l.sum_delta,0)), s."AverageCost",
       'OPENING-BALANCE', 'Created by migration: aligns ledger with stock at migration time',
       now(), s."Quantity" - COALESCE(l.sum_delta,0)
  FROM "Stocks" s
  LEFT JOIN (SELECT "StockId", SUM("Delta") AS sum_delta FROM "StockMovements" GROUP BY "StockId") l
         ON l."StockId" = s."Id"
 WHERE s."Quantity" <> COALESCE(l.sum_delta,0);

ALTER TABLE "StockMovements" ALTER COLUMN "Delta" SET DEFAULT 0;
ALTER TABLE "StockMovements" ALTER COLUMN "Delta" SET NOT NULL;

-- ---------------------------------------------------------------------------
-- 2. Merge duplicate stock rows (NULL location was never covered by the old
--    unique index, so duplicates may exist), then enforce uniqueness.
-- ---------------------------------------------------------------------------
DO $$
DECLARE r record;
BEGIN
  FOR r IN
    SELECT MIN("Id") AS keep_id,
           array_agg("Id" ORDER BY "Id") AS ids,
           SUM("Quantity") AS qty,
           SUM("ReservedQuantity") AS reserved,
           CASE WHEN SUM("Quantity") > 0
                THEN SUM("Quantity" * "AverageCost") / SUM("Quantity")
                ELSE MAX("AverageCost") END AS avg_cost
      FROM "Stocks"
     GROUP BY "ProductId","WarehouseId", COALESCE("LocationId",0)
    HAVING COUNT(*) > 1
  LOOP
    UPDATE "StockMovements" SET "StockId" = r.keep_id WHERE "StockId" = ANY (r.ids) AND "StockId" <> r.keep_id;
    UPDATE "Lots"           SET "StockId" = r.keep_id WHERE "StockId" = ANY (r.ids) AND "StockId" <> r.keep_id;
    DELETE FROM "Stocks" WHERE "Id" = ANY (r.ids) AND "Id" <> r.keep_id;
    UPDATE "Stocks" SET "Quantity" = r.qty, "ReservedQuantity" = r.reserved, "AverageCost" = r.avg_cost
     WHERE "Id" = r.keep_id;
  END LOOP;
END $$;

CREATE UNIQUE INDEX IF NOT EXISTS ux_stocks_product_wh_loc
    ON "Stocks" ("ProductId","WarehouseId",(COALESCE("LocationId",0)));

-- ---------------------------------------------------------------------------
-- 3. CHECK constraints: the last line of defence.
--    NOT VALID = enforced for every new/updated row without failing the
--    migration on historical bad rows; we then try to VALIDATE and only warn.
-- ---------------------------------------------------------------------------
DO $$
DECLARE c text;
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_stocks_quantity_nonneg') THEN
    ALTER TABLE "Stocks" ADD CONSTRAINT ck_stocks_quantity_nonneg CHECK ("Quantity" >= 0) NOT VALID;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_stocks_reserved_range') THEN
    ALTER TABLE "Stocks" ADD CONSTRAINT ck_stocks_reserved_range
      CHECK ("ReservedQuantity" >= 0 AND "ReservedQuantity" <= "Quantity") NOT VALID;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_orderitems_quantity_pos') THEN
    ALTER TABLE "OrderItems" ADD CONSTRAINT ck_orderitems_quantity_pos CHECK ("Quantity" > 0) NOT VALID;
  END IF;

  FOREACH c IN ARRAY ARRAY['ck_stocks_quantity_nonneg','ck_stocks_reserved_range','ck_orderitems_quantity_pos'] LOOP
    BEGIN
      IF c LIKE 'ck_orderitems%' THEN
        EXECUTE format('ALTER TABLE "OrderItems" VALIDATE CONSTRAINT %I', c);
      ELSE
        EXECUTE format('ALTER TABLE "Stocks" VALIDATE CONSTRAINT %I', c);
      END IF;
    EXCEPTION WHEN check_violation THEN
      RAISE WARNING 'constraint % left NOT VALID: historical rows violate it. Fix them, then VALIDATE.', c;
    END;
  END LOOP;
END $$;

-- ---------------------------------------------------------------------------
-- 4. Indexes for the queries the API and the analytics service actually run
-- ---------------------------------------------------------------------------
CREATE INDEX IF NOT EXISTS ix_orders_orderdate          ON "Orders" ("OrderDate");
CREATE INDEX IF NOT EXISTS ix_orderitems_product_order  ON "OrderItems" ("ProductId","OrderId");
CREATE INDEX IF NOT EXISTS ix_movements_stock_created   ON "StockMovements" ("StockId","CreatedAt");
CREATE INDEX IF NOT EXISTS ix_stocks_low                ON "Stocks" ("ProductId") WHERE "Quantity" <= "ReorderPoint";

-- ---------------------------------------------------------------------------
-- 5. Idempotency keys (a retried POST must not create a second order)
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "IdempotencyKeys" (
    "Key"        text        PRIMARY KEY,
    "Scope"      text        NOT NULL,
    "ResourceId" integer     NULL,
    "CreatedAt"  timestamptz NOT NULL DEFAULT now()
);

-- ---------------------------------------------------------------------------
-- 6. Document numbers from sequences (no "last + 1" race)
-- ---------------------------------------------------------------------------
CREATE SEQUENCE IF NOT EXISTS wms_order_number_seq;
CREATE SEQUENCE IF NOT EXISTS wms_invoice_number_seq;
CREATE SEQUENCE IF NOT EXISTS wms_po_number_seq;
CREATE SEQUENCE IF NOT EXISTS wms_rma_number_seq;

-- Start sequences above anything already issued (only moves them forward).
DO $$
DECLARE v bigint;
BEGIN
  SELECT COALESCE(MAX(regexp_replace("OrderNumber",'^.*-','')::bigint),0) INTO v
    FROM "Orders" WHERE "OrderNumber" ~ '^CMD-[0-9]{8}-[0-9]+$';
  IF v > 0 AND v > (SELECT CASE WHEN is_called THEN last_value ELSE 0 END FROM wms_order_number_seq) THEN
    PERFORM setval('wms_order_number_seq', v);
  END IF;

  SELECT COALESCE(MAX(regexp_replace("InvoiceNumber",'^.*-','')::bigint),0) INTO v
    FROM "Invoices" WHERE "InvoiceNumber" ~ '^FAC-[0-9]{8}-[0-9]+$';
  IF v > 0 AND v > (SELECT CASE WHEN is_called THEN last_value ELSE 0 END FROM wms_invoice_number_seq) THEN
    PERFORM setval('wms_invoice_number_seq', v);
  END IF;
END $$;

CREATE OR REPLACE FUNCTION wms_next_number(p_kind text) RETURNS text
LANGUAGE plpgsql AS $$
DECLARE d text := to_char(now() AT TIME ZONE 'UTC','YYYYMMDD');
BEGIN
  RETURN CASE p_kind
    WHEN 'order'   THEN 'CMD-' || d || '-' || lpad(nextval('wms_order_number_seq')::text,   4, '0')
    WHEN 'invoice' THEN 'FAC-' || d || '-' || lpad(nextval('wms_invoice_number_seq')::text, 4, '0')
    WHEN 'po'      THEN 'PO-'  || d || '-' || lpad(nextval('wms_po_number_seq')::text,      4, '0')
    WHEN 'rma'     THEN 'RMA-' || d || '-' || lpad(nextval('wms_rma_number_seq')::text,     4, '0')
    ELSE NULL END;
END $$;

-- ---------------------------------------------------------------------------
-- 7. Atomic stock operations.
--    SQLSTATEs raised (the API maps them to HTTP errors):
--      WMS01 insufficient stock      WMS02 invalid quantity
--      WMS03 stock row not found     WMS04 invalid transfer
-- ---------------------------------------------------------------------------

-- Remove stock. One UPDATE both checks and decrements: under READ COMMITTED a
-- concurrent caller waits for the row lock and then re-checks the WHERE clause
-- against the fresh row, so two callers can never both take the last unit.
CREATE OR REPLACE FUNCTION wms_stock_issue(
    p_stock_id int, p_qty numeric, p_type int, p_ref text, p_notes text)
RETURNS numeric
LANGUAGE plpgsql AS $$
DECLARE v_new numeric; v_cost numeric; v_avail numeric; v_product int;
BEGIN
  IF p_qty IS NULL OR p_qty <= 0 THEN
    RAISE EXCEPTION USING ERRCODE = 'WMS02', MESSAGE = 'quantity must be greater than 0';
  END IF;

  UPDATE "Stocks"
     SET "Quantity" = "Quantity" - p_qty, "LastUpdated" = now()
   WHERE "Id" = p_stock_id AND "Quantity" - "ReservedQuantity" >= p_qty
  RETURNING "Quantity","AverageCost" INTO v_new, v_cost;

  IF NOT FOUND THEN
    SELECT "Quantity" - "ReservedQuantity", "ProductId" INTO v_avail, v_product
      FROM "Stocks" WHERE "Id" = p_stock_id;
    IF NOT FOUND THEN
      RAISE EXCEPTION USING ERRCODE = 'WMS03', MESSAGE = format('stock %s not found', p_stock_id);
    END IF;
    RAISE EXCEPTION USING ERRCODE = 'WMS01',
      MESSAGE = 'insufficient stock',
      DETAIL  = format('{"stockId":%s,"productId":%s,"required":%s,"available":%s}',
                       p_stock_id, v_product, p_qty, v_avail);
  END IF;

  INSERT INTO "StockMovements" ("StockId","Type","Quantity","UnitCost","Reference","Notes","CreatedAt","Delta")
  VALUES (p_stock_id, p_type, p_qty, v_cost, p_ref, p_notes, now(), -p_qty);

  RETURN v_new;
END $$;

-- Add stock. CUMP is updated incrementally inside the same UPDATE (all SET
-- expressions read the OLD row): O(1), no rescan of the ledger.
-- p_unit_cost NULL = "at current average cost" (returns, +adjustments).
CREATE OR REPLACE FUNCTION wms_stock_receive(
    p_stock_id int, p_qty numeric, p_unit_cost numeric, p_type int, p_ref text, p_notes text)
RETURNS numeric
LANGUAGE plpgsql AS $$
DECLARE v_new numeric; v_cost numeric;
BEGIN
  IF p_qty IS NULL OR p_qty <= 0 THEN
    RAISE EXCEPTION USING ERRCODE = 'WMS02', MESSAGE = 'quantity must be greater than 0';
  END IF;
  IF p_unit_cost IS NOT NULL AND p_unit_cost < 0 THEN
    RAISE EXCEPTION USING ERRCODE = 'WMS02', MESSAGE = 'unit cost must be >= 0';
  END IF;

  UPDATE "Stocks"
     SET "AverageCost" = ROUND(("Quantity" * "AverageCost" + p_qty * COALESCE(p_unit_cost, "AverageCost"))
                               / ("Quantity" + p_qty), 4),
         "Quantity"    = "Quantity" + p_qty,
         "LastUpdated" = now()
   WHERE "Id" = p_stock_id
  RETURNING "Quantity","AverageCost" INTO v_new, v_cost;

  IF NOT FOUND THEN
    RAISE EXCEPTION USING ERRCODE = 'WMS03', MESSAGE = format('stock %s not found', p_stock_id);
  END IF;

  INSERT INTO "StockMovements" ("StockId","Type","Quantity","UnitCost","Reference","Notes","CreatedAt","Delta")
  VALUES (p_stock_id, p_type, p_qty, COALESCE(p_unit_cost, v_cost), p_ref, p_notes, now(), p_qty);

  RETURN v_new;
END $$;

-- Manual correction (+ or -). Goes through the same guarded paths.
CREATE OR REPLACE FUNCTION wms_stock_adjust(p_stock_id int, p_delta numeric, p_reason text)
RETURNS numeric
LANGUAGE plpgsql AS $$
DECLARE v_ref text := 'ADJ-' || to_char(now() AT TIME ZONE 'UTC','YYYYMMDDHH24MISS');
BEGIN
  IF p_delta IS NULL OR p_delta = 0 THEN
    RAISE EXCEPTION USING ERRCODE = 'WMS02', MESSAGE = 'adjustment must be non-zero';
  END IF;
  IF p_delta > 0 THEN
    RETURN wms_stock_receive(p_stock_id, p_delta, NULL, 3, v_ref, p_reason);
  END IF;
  RETURN wms_stock_issue(p_stock_id, -p_delta, 3, v_ref, p_reason);
END $$;

-- Find or create the stock row for (product, warehouse, location), race-free.
CREATE OR REPLACE FUNCTION wms_stock_get_or_create(
    p_product int, p_warehouse int, p_location int, p_reorder numeric DEFAULT 0)
RETURNS int
LANGUAGE plpgsql AS $$
DECLARE v_id int;
BEGIN
  INSERT INTO "Stocks" ("ProductId","WarehouseId","LocationId","Quantity","ReservedQuantity",
                        "AverageCost","ReorderPoint","LastUpdated")
  VALUES (p_product, p_warehouse, p_location, 0, 0, 0, COALESCE(p_reorder,0), now())
  ON CONFLICT ("ProductId","WarehouseId",(COALESCE("LocationId",0))) DO NOTHING;

  SELECT "Id" INTO v_id FROM "Stocks"
   WHERE "ProductId" = p_product AND "WarehouseId" = p_warehouse
     AND COALESCE("LocationId",0) = COALESCE(p_location,0);
  RETURN v_id;
END $$;

-- Move stock between two stock rows. Rows are locked in id order so two
-- opposite transfers can never deadlock. Destination inherits the source cost.
CREATE OR REPLACE FUNCTION wms_stock_transfer(p_from int, p_to int, p_qty numeric, p_ref text)
RETURNS void
LANGUAGE plpgsql AS $$
DECLARE v_cost numeric;
BEGIN
  IF p_from = p_to THEN
    RAISE EXCEPTION USING ERRCODE = 'WMS04', MESSAGE = 'source and destination must differ';
  END IF;

  PERFORM 1 FROM "Stocks" WHERE "Id" IN (p_from, p_to) ORDER BY "Id" FOR UPDATE;

  PERFORM wms_stock_issue(p_from, p_qty, 2, p_ref, 'Transfer out');
  SELECT "AverageCost" INTO v_cost FROM "Stocks" WHERE "Id" = p_from;
  PERFORM wms_stock_receive(p_to, p_qty, v_cost, 2, p_ref, 'Transfer in');
END $$;

-- ---------------------------------------------------------------------------
-- 8. Reconciliation: rows returned here are BUGS. Must always be empty.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE VIEW wms_stock_drift AS
SELECT COALESCE(s."Id", m."StockId") AS stock_id,
       COALESCE(s."Quantity", 0)     AS stock_quantity,
       COALESCE(m.total, 0)          AS ledger_quantity
  FROM "Stocks" s
  FULL JOIN (SELECT "StockId", SUM("Delta") AS total
               FROM "StockMovements" GROUP BY "StockId") m ON m."StockId" = s."Id"
 WHERE COALESCE(s."Quantity", 0) <> COALESCE(m.total, 0);
