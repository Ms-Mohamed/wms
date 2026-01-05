import argparse
import random
from datetime import datetime, timedelta

import psycopg2
from psycopg2.extras import execute_values


def parse_args():
    parser = argparse.ArgumentParser(
        description="Generate large mock data for WMS performance tests."
    )
    parser.add_argument("--db-host", default="localhost")
    parser.add_argument("--db-port", type=int, default=5432)
    parser.add_argument("--db-name", default="wms_db")
    parser.add_argument("--db-user", default="postgres")
    parser.add_argument("--db-pass", default="postgres")

    parser.add_argument("--products", type=int, default=100_000)
    parser.add_argument("--warehouses", type=int, default=50)
    parser.add_argument("--locations-per-warehouse", type=int, default=5)
    parser.add_argument("--customers", type=int, default=50_000)
    parser.add_argument("--orders", type=int, default=1_000_000)
    parser.add_argument("--max-items-per-order", type=int, default=5)
    parser.add_argument(
        "--stock-coverage",
        type=float,
        default=0.2,
        help="Fraction of product/warehouse pairs that get stock rows (0-1).",
    )
    parser.add_argument(
        "--chunk-size",
        type=int,
        default=10_000,
        help="Rows per bulk insert chunk.",
    )
    parser.add_argument(
        "--days",
        type=int,
        default=120,
        help="Spread orders across the last N days.",
    )
    return parser.parse_args()


def random_code(prefix: str, idx: int, width: int = 7) -> str:
    return f"{prefix}-{idx:0{width}d}"


def random_name(prefix: str, idx: int) -> str:
    return f"{prefix} {idx}"


def random_email(idx: int) -> str:
    return f"user{idx}@example.com"


def chunked(iterable, size):
    for i in range(0, len(iterable), size):
        yield iterable[i : i + size]


def bulk_insert(conn, table, columns, rows, page_size):
    if not rows:
        return
    with conn.cursor() as cur:
        execute_values(
            cur,
            f'INSERT INTO "{table}" ({",".join(f"""\"{c}\"""" for c in columns)}) VALUES %s',
            rows,
            page_size=page_size,
        )
    conn.commit()


def table_count(conn, table):
    with conn.cursor() as cur:
        cur.execute(f'SELECT COUNT(*) FROM "{table}"')
        return cur.fetchone()[0]


def next_id_range(conn, table, count):
    with conn.cursor() as cur:
        cur.execute(f'SELECT COALESCE(MAX("Id"), 0) FROM "{table}"')
        start = cur.fetchone()[0]
    return list(range(start + 1, start + count + 1))


def seed_warehouses(conn, count, chunk_size, start_offset):
    rows = [
        (
            random_code("WH", start_offset + i + 1),
            random_name("Entrepot", start_offset + i + 1),
            "1 Rue de l'Industrie",
            "Paris",
            "France",
            True,
        )
        for i in range(count)
    ]
    columns = ["Code", "Name", "Address", "City", "Country", "IsActive"]
    for chunk in chunked(rows, chunk_size):
        bulk_insert(conn, "Warehouses", columns, chunk, page_size=chunk_size)


def seed_locations(conn, warehouse_ids, per_warehouse, chunk_size):
    rows = []
    for wid in warehouse_ids:
        for i in range(per_warehouse):
            code = f"A-{i+1:02d}"
            rows.append(
                (
                    wid,
                    code,
                    f"Zone A - Rack {i+1:02d}",
                    "A",
                    True,
                )
            )
    columns = ["WarehouseId", "Code", "Name", "Zone", "IsActive"]
    for chunk in chunked(rows, chunk_size):
        bulk_insert(conn, "Locations", columns, chunk, page_size=chunk_size)


def seed_products(conn, count, chunk_size, start_offset):
    units = ["PIECE", "BOX", "PALLET"]
    rows = []
    for i in range(count):
        price = round(random.uniform(5, 1500), 2)
        cost = round(price * random.uniform(0.5, 0.85), 2)
        rows.append(
            (
                random_code("PROD", start_offset + i + 1),
                random_name("Produit", start_offset + i + 1),
                "Produit généré pour test performance",
                price,
                cost,
                random.choice(units),
                False,
                False,
                datetime.utcnow(),
                None,
            )
        )
    columns = [
        "Code",
        "Name",
        "Description",
        "UnitPrice",
        "CostPrice",
        "Unit",
        "RequiresLotTracking",
        "RequiresSerialTracking",
        "CreatedAt",
        "UpdatedAt",
    ]
    for chunk in chunked(rows, chunk_size):
        bulk_insert(conn, "Products", columns, chunk, page_size=chunk_size)


def seed_customers(conn, count, chunk_size, start_offset):
    rows = [
        (
            random_code("CUST", start_offset + i + 1),
            random_name("Client", start_offset + i + 1),
            random_email(start_offset + i + 1),
        )
        for i in range(count)
    ]
    columns = ["Code", "Name", "Email"]
    for chunk in chunked(rows, chunk_size):
        bulk_insert(conn, "Customers", columns, chunk, page_size=chunk_size)


def seed_stocks(conn, product_ids, warehouse_ids, coverage, chunk_size):
    total_pairs = len(product_ids) * len(warehouse_ids)
    target = int(total_pairs * coverage)
    rows = []
    for _ in range(target):
        pid = random.choice(product_ids)
        wid = random.choice(warehouse_ids)
        qty = round(random.uniform(50, 5000), 2)
        reorder = round(qty * 0.1, 2)
        avg_cost = round(random.uniform(5, 800), 2)
        rows.append(
            (
                pid,
                wid,
                None,
                qty,
                0,
                avg_cost,
                reorder,
                datetime.utcnow(),
            )
        )
    columns = [
        "ProductId",
        "WarehouseId",
        "LocationId",
        "Quantity",
        "ReservedQuantity",
        "AverageCost",
        "ReorderPoint",
        "LastUpdated",
    ]
    for chunk in chunked(rows, chunk_size):
        bulk_insert(conn, "Stocks", columns, chunk, page_size=chunk_size)


def seed_orders(conn, product_ids, warehouse_ids, customer_ids, orders_count, max_items, days, chunk_size):
    order_rows = []
    item_rows_by_order = []
    now = datetime.utcnow()
    for i in range(orders_count):
        order_date = now - timedelta(days=random.randint(0, days))
        subtotal = 0
        items = random.randint(1, max_items)
        chosen_products = random.sample(product_ids, min(items, len(product_ids)))
        item_rows = []
        for pid in chosen_products:
            quantity = round(random.uniform(1, 20), 2)
            unit_price = round(random.uniform(5, 1500), 2)
            line_total = round(quantity * unit_price, 2)
            subtotal += line_total
            item_rows.append(
                (
                    None,  # OrderId placeholder
                    pid,
                    random.choice(warehouse_ids),
                    quantity,
                    unit_price,
                    unit_price,
                    0,
                    line_total,
                    datetime.utcnow(),
                )
            )
        tax_rate = 0.2
        tax_amount = round(subtotal * tax_rate, 2)
        total = round(subtotal + tax_amount, 2)
        order_rows.append(
            (
                random_code("CMD", i + 1),
                random.choice(customer_ids) if customer_ids else None,
                random_name("Client", random.randint(1, len(customer_ids) or 1)),
                random_email(random.randint(1, len(customer_ids) or 1)),
                "Adresse inconnue",
                1,  # Status = Confirmed
                order_date,
                None,
                None,
                subtotal,
                tax_rate,
                tax_amount,
                total,
                None,
                datetime.utcnow(),
                None,
            )
        )
        item_rows_by_order.append(item_rows)

    order_columns = [
        "OrderNumber",
        "CustomerId",
        "CustomerName",
        "CustomerEmail",
        "CustomerAddress",
        "Status",
        "OrderDate",
        "ShippedDate",
        "DeliveredDate",
        "SubTotal",
        "TaxRate",
        "TaxAmount",
        "TotalAmount",
        "Notes",
        "CreatedAt",
        "UpdatedAt",
    ]

    item_columns = [
        "OrderId",
        "ProductId",
        "WarehouseId",
        "Quantity",
        "UnitPrice",
        "UnitPriceAtSale",
        "Discount",
        "LineTotal",
        "CreatedAt",
    ]

    start_idx = 0
    for order_chunk in chunked(order_rows, chunk_size):
        chunk_len = len(order_chunk)
        with conn.cursor() as cur:
            execute_values(
                cur,
                f'INSERT INTO "Orders" ({",".join(f"""\"{c}\"""" for c in order_columns)}) VALUES %s RETURNING "Id"',
                order_chunk,
                page_size=chunk_size,
            )
            ids = [row[0] for row in cur.fetchall()]
        conn.commit()

        item_buffer = []
        for idx, order_id in enumerate(ids):
            items = item_rows_by_order[start_idx + idx]
            for item in items:
                item_buffer.append((order_id,) + item[1:])  # replace placeholder
            if len(item_buffer) >= chunk_size:
                bulk_insert(conn, "OrderItems", item_columns, item_buffer, page_size=chunk_size)
                item_buffer = []
        if item_buffer:
            bulk_insert(conn, "OrderItems", item_columns, item_buffer, page_size=chunk_size)
        start_idx += chunk_len


def main():
    args = parse_args()
    conn = psycopg2.connect(
        host=args.db_host,
        port=args.db_port,
        dbname=args.db_name,
        user=args.db_user,
        password=args.db_pass,
    )

    print("[INFO] Seeding warehouses...")
    wh_offset = table_count(conn, "Warehouses")
    warehouse_ids = next_id_range(conn, "Warehouses", args.warehouses)
    seed_warehouses(conn, args.warehouses, args.chunk_size, wh_offset)

    print("[INFO] Seeding locations...")
    seed_locations(conn, warehouse_ids, args.locations_per_warehouse, args.chunk_size)

    print("[INFO] Seeding products...")
    prod_offset = table_count(conn, "Products")
    product_ids = next_id_range(conn, "Products", args.products)
    seed_products(conn, args.products, args.chunk_size, prod_offset)

    print("[INFO] Seeding customers...")
    cust_offset = table_count(conn, "Customers")
    customer_ids = next_id_range(conn, "Customers", args.customers)
    seed_customers(conn, args.customers, args.chunk_size, cust_offset)

    print("[INFO] Seeding stocks...")
    seed_stocks(conn, product_ids, warehouse_ids, args.stock_coverage, args.chunk_size)

    print("[INFO] Seeding orders and order items...")
    seed_orders(
        conn,
        product_ids,
        warehouse_ids,
        customer_ids,
        args.orders,
        args.max_items_per_order,
        args.days,
        args.chunk_size,
    )

    conn.close()
    print("[DONE] Data generation completed.")


if __name__ == "__main__":
    main()


