-- Synthetic dataset for the analytics benchmark (Products/Stocks/Orders/OrderItems only).
-- Usage: psql -v products=100000 -v orders=1000000 -f seed_analytics_bench.sql <db>   (schema first: backend-python/tests/schema_analytics.sql)
INSERT INTO "Products"("Code","Name","CostPrice")
SELECT 'P-' || lpad(g::text, 7, '0'), 'Product ' || g, 5 + (g % 200) FROM generate_series(1, :products) g;

INSERT INTO "Stocks"("ProductId","WarehouseId","LocationId","Quantity","AverageCost","ReorderPoint")
SELECT g, 1 + (g % 5), 1 + (g % 5), (g * 7) % 300, 5 + (g % 200), (g * 3) % 120 FROM generate_series(1, :products) g;

-- orders spread over the last 400 days
INSERT INTO "Orders"("OrderNumber","Status","OrderDate","TotalAmount")
SELECT 'CMD-' || g, CASE WHEN g % 50 = 0 THEN 5 ELSE 4 END,
       now() - (random() * 400 || ' days')::interval, (random() * 500)::numeric(18,2)
  FROM generate_series(1, :orders) g;

-- 1-3 lines per order, products skewed (popular items) so forecasts have real history
INSERT INTO "OrderItems"("OrderId","ProductId","Quantity")
SELECT o, 1 + floor(power(random(), 2.5) * :products)::int, 1 + floor(random() * 20)
  FROM generate_series(1, :orders) o, generate_series(1, 1 + (o % 3)) l;
ANALYZE;
