using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using WMS.API.Controllers;
using WMS.Business.DTOs;
using WMS.Business.Exceptions;
using WMS.Data.Entities;
using Xunit;

namespace WMS.Tests;

public class StockIntegrityTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _pg;
    public StockIntegrityTests(PostgresFixture pg) => _pg = pg;

    private async Task<OrderDto> CreateOrderAsync(int productId, int warehouseId, decimal qty, string? key = null)
    {
        using var s = _pg.NewScope();
        return await s.Orders.CreateOrderAsync(new CreateOrderDto
        {
            CustomerName = "ACME",
            Items = { new CreateOrderItemDto { ProductId = productId, WarehouseId = warehouseId, Quantity = qty } }
        }, key);
    }

    private static ShipOrderDto ShipAll(OrderDto order, int locationId) => new()
    {
        Items = order.Items.Select(i => new ShipOrderItemDto { OrderItemId = i.Id, LocationId = locationId, Quantity = i.Quantity }).ToList()
    };

    // ---- the headline guarantee ---------------------------------------------------------------
    [Fact]
    public async Task Parallel_shipments_of_the_last_units_never_oversell()
    {
        var (productId, warehouseId, locationId, stockId) = await _pg.SeedStockAsync(5);

        var orders = new List<OrderDto>();
        for (var i = 0; i < 30; i++) orders.Add(await CreateOrderAsync(productId, warehouseId, 1));

        var results = await Task.WhenAll(orders.Select(async o =>
        {
            using var s = _pg.NewScope();
            try { await s.Orders.ShipOrderAsync(o.Id, ShipAll(o, locationId)); return "ok"; }
            catch (InsufficientStockException) { return "refused"; }
        }));

        Assert.Equal(5, results.Count(r => r == "ok"));
        Assert.Equal(25, results.Count(r => r == "refused"));
        Assert.Equal(0m, await _pg.QuantityAsync(stockId));
        Assert.Equal(0, await _pg.DriftRowsAsync());
    }

    [Fact]
    public async Task The_same_order_shipped_twice_at_once_ships_once_and_invoices_once()
    {
        var (productId, warehouseId, locationId, stockId) = await _pg.SeedStockAsync(10);
        var order = await CreateOrderAsync(productId, warehouseId, 4);

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            using var s = _pg.NewScope();
            try { await s.Orders.ShipOrderAsync(order.Id, ShipAll(order, locationId)); return "ok"; }
            catch (InvalidOperationException) { return "already"; }
        }));

        Assert.Equal(1, outcomes.Count(o => o == "ok"));
        Assert.Equal(6m, await _pg.QuantityAsync(stockId));          // 10 - 4, deducted once
        await using var db = _pg.NewContext();
        Assert.Equal(1, await db.Invoices.CountAsync(i => i.OrderId == order.Id));
        Assert.Equal(0, await _pg.DriftRowsAsync());
    }

    [Fact]
    public async Task A_multi_line_shipment_is_all_or_nothing()
    {
        var a = await _pg.SeedStockAsync(5);
        var b = await _pg.SeedStockAsync(1);

        OrderDto order;
        using (var s = _pg.NewScope())
        {
            order = await s.Orders.CreateOrderAsync(new CreateOrderDto
            {
                CustomerName = "ACME",
                Items =
                {
                    new CreateOrderItemDto { ProductId = a.productId, WarehouseId = a.warehouseId, Quantity = 3 },
                    new CreateOrderItemDto { ProductId = b.productId, WarehouseId = b.warehouseId, Quantity = 2 } // only 1 in stock
                }
            });
        }

        var ship = new ShipOrderDto
        {
            Items =
            {
                new ShipOrderItemDto { OrderItemId = order.Items[0].Id, LocationId = a.locationId, Quantity = 3 },
                new ShipOrderItemDto { OrderItemId = order.Items[1].Id, LocationId = b.locationId, Quantity = 2 }
            }
        };

        using (var s = _pg.NewScope())
            await Assert.ThrowsAsync<InsufficientStockException>(() => s.Orders.ShipOrderAsync(order.Id, ship));

        Assert.Equal(5m, await _pg.QuantityAsync(a.stockId));         // line 1 rolled back
        Assert.Equal(1m, await _pg.QuantityAsync(b.stockId));
        using (var s = _pg.NewScope())
            Assert.Equal("Pending", (await s.Orders.GetOrderByIdAsync(order.Id))!.Status);   // claim rolled back
        Assert.Equal(0, await _pg.DriftRowsAsync());
    }

    [Fact]
    public async Task Foreign_lines_and_zero_quantities_are_rejected_before_touching_stock()
    {
        var (productId, warehouseId, locationId, stockId) = await _pg.SeedStockAsync(10);
        var order = await CreateOrderAsync(productId, warehouseId, 4);
        using var s = _pg.NewScope();

        await Assert.ThrowsAsync<InvalidOperationException>(() => s.Orders.ShipOrderAsync(order.Id, new ShipOrderDto
        { Items = { new ShipOrderItemDto { OrderItemId = 999999, LocationId = locationId, Quantity = 4 } } }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.Orders.ShipOrderAsync(order.Id, new ShipOrderDto
        { Items = { new ShipOrderItemDto { OrderItemId = order.Items[0].Id, LocationId = locationId, Quantity = 0 } } }));

        Assert.Equal(10m, await _pg.QuantityAsync(stockId));
    }

    // ---- partial shipments and reservations ---------------------------------------------------
    [Fact]
    public async Task A_partial_shipment_keeps_the_order_open_and_the_last_one_completes_and_invoices_it()
    {
        var (productId, warehouseId, locationId, stockId) = await _pg.SeedStockAsync(10);
        var order = await CreateOrderAsync(productId, warehouseId, 6);

        ShipOrderDto Part(decimal q) => new() { Items = { new ShipOrderItemDto { OrderItemId = order.Items[0].Id, LocationId = locationId, Quantity = q } } };

        using (var s = _pg.NewScope())
        {
            var afterFirst = await s.Orders.ShipOrderAsync(order.Id, Part(4));
            Assert.Equal("PartiallyShipped", afterFirst.Status);
            Assert.Equal(4m, afterFirst.Items[0].ShippedQuantity);
        }
        await using (var db = _pg.NewContext())
            Assert.Equal(0, await db.Invoices.CountAsync(i => i.OrderId == order.Id));   // not invoiced yet

        using (var s = _pg.NewScope())
            Assert.Equal("Shipped", (await s.Orders.ShipOrderAsync(order.Id, Part(2))).Status);

        await using (var db = _pg.NewContext())
            Assert.Equal(1, await db.Invoices.CountAsync(i => i.OrderId == order.Id));
        Assert.Equal(4m, await _pg.QuantityAsync(stockId));
        Assert.Equal(0, await _pg.DriftRowsAsync());
    }

    [Fact]
    public async Task Shipping_more_than_remains_on_a_line_is_refused()
    {
        var (productId, warehouseId, locationId, stockId) = await _pg.SeedStockAsync(10);
        var order = await CreateOrderAsync(productId, warehouseId, 3);
        ShipOrderDto Part(decimal q) => new() { Items = { new ShipOrderItemDto { OrderItemId = order.Items[0].Id, LocationId = locationId, Quantity = q } } };

        using (var s = _pg.NewScope()) await s.Orders.ShipOrderAsync(order.Id, Part(2));
        using (var s = _pg.NewScope())
            await Assert.ThrowsAsync<InvalidOperationException>(() => s.Orders.ShipOrderAsync(order.Id, Part(2)));
        Assert.Equal(8m, await _pg.QuantityAsync(stockId));
    }

    [Fact]
    public async Task Reserved_units_cannot_be_shipped_by_another_order_and_cancel_gives_them_back()
    {
        var (productId, warehouseId, locationId, stockId) = await _pg.SeedStockAsync(5);
        var holder = await CreateOrderAsync(productId, warehouseId, 4);
        var other = await CreateOrderAsync(productId, warehouseId, 3);

        using (var s = _pg.NewScope())
            Assert.Equal(4m, (await s.Orders.ReserveOrderAsync(holder.Id)).Items[0].ReservedQuantity);

        using (var s = _pg.NewScope())
            await Assert.ThrowsAsync<InsufficientStockException>(() => s.Orders.ShipOrderAsync(other.Id, ShipAll(other, locationId)));

        using (var s = _pg.NewScope())
            Assert.Equal("Cancelled", (await s.Orders.CancelOrderAsync(holder.Id)).Status);

        using (var s = _pg.NewScope())
            Assert.Equal("Shipped", (await s.Orders.ShipOrderAsync(other.Id, ShipAll(other, locationId))).Status);
        Assert.Equal(2m, await _pg.QuantityAsync(stockId));
        Assert.Equal(0, await _pg.DriftRowsAsync());
    }

    [Fact]
    public async Task Reserving_more_than_is_free_is_refused_and_holds_nothing()
    {
        var (productId, warehouseId, _, _) = await _pg.SeedStockAsync(2);
        var order = await CreateOrderAsync(productId, warehouseId, 5);

        using (var s = _pg.NewScope())
            await Assert.ThrowsAsync<InsufficientStockException>(() => s.Orders.ReserveOrderAsync(order.Id));
        using (var s = _pg.NewScope())
            Assert.Equal(0m, (await s.Orders.GetOrderByIdAsync(order.Id))!.Items[0].ReservedQuantity);
    }

    // ---- idempotency & numbering --------------------------------------------------------------
    [Fact]
    public async Task Retrying_a_create_with_the_same_idempotency_key_returns_the_same_order()
    {
        var (productId, warehouseId, _, _) = await _pg.SeedStockAsync(5);
        var key = Guid.NewGuid().ToString();

        // Use same hash for all
        var dto = new CreateOrderDto { CustomerName = "Test", Items = { new CreateOrderItemDto { ProductId = productId, WarehouseId = warehouseId, Quantity = 1 } } };
        var requestHash = "hashA";

        var created = await Task.WhenAll(Enumerable.Range(0, 6).Select(async _ =>
        {
            using var scope = _pg.NewScope();
            return await scope.Orders.CreateOrderAsync(dto, key, requestHash);
        }));

        Assert.Single(created.Select(o => o.Id).Distinct());
        await using var db = _pg.NewContext();
        Assert.Equal(1, await db.Orders.CountAsync(o => o.Items.Any(i => i.ProductId == productId)));
    }

    [Fact]
    public async Task Idempotency_key_with_different_body_throws()
    {
        var (productId, warehouseId, _, _) = await _pg.SeedStockAsync(5);
        var key = Guid.NewGuid().ToString();

        var dto1 = new CreateOrderDto { CustomerName = "Test", Items = { new CreateOrderItemDto { ProductId = productId, WarehouseId = warehouseId, Quantity = 1 } } };
        using (var scope1 = _pg.NewScope())
        {
            await scope1.Orders.CreateOrderAsync(dto1, key, "hash1");
        }

        var dto2 = new CreateOrderDto { CustomerName = "Test2", Items = { new CreateOrderItemDto { ProductId = productId, WarehouseId = warehouseId, Quantity = 2 } } };
        using (var scope2 = _pg.NewScope())
        {
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => scope2.Orders.CreateOrderAsync(dto2, key, "hash2"));
            Assert.Contains("Idempotency key reused with different request body", ex.Message);
        }
    }

    [Fact]
    public async Task Concurrent_order_creation_never_produces_duplicate_numbers()
    {
        var (productId, warehouseId, _, _) = await _pg.SeedStockAsync(5);

        var orders = await Task.WhenAll(Enumerable.Range(0, 40).Select(_ => CreateOrderAsync(productId, warehouseId, 1)));

        Assert.Equal(40, orders.Select(o => o.OrderNumber).Distinct().Count());
    }

    [Fact]
    public async Task IdempotencyCleanupService_DeletesExpiredKeys()
    {
        await using var db = _pg.NewContext();
        await db.Database.ExecuteSqlRawAsync("INSERT INTO \"IdempotencyKeys\" (\"Key\", \"Scope\", \"CreatedAt\") VALUES ('old-key', 'order.create', now() - interval '8 days') ON CONFLICT DO NOTHING");
        await db.Database.ExecuteSqlRawAsync("INSERT INTO \"IdempotencyKeys\" (\"Key\", \"Scope\", \"CreatedAt\") VALUES ('new-key', 'order.create', now()) ON CONFLICT DO NOTHING");
        
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddDbContext<WMS.Data.WmsDbContext>(o => o.UseNpgsql(_pg.ConnectionString));
        var serviceProvider = services.BuildServiceProvider();
        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<WMS.API.Services.IdempotencyCleanupService>();
        var cleanupService = new WMS.API.Services.IdempotencyCleanupService(serviceProvider, logger);
        
        var cts = new CancellationTokenSource();
        cts.CancelAfter(500); 
        
        try { await cleanupService.StartAsync(cts.Token); } catch {}
        await Task.Delay(100);
        
        Assert.Equal(0, await db.Database.SqlQuery<int>($"SELECT COUNT(*)::int AS \"Value\" FROM \"IdempotencyKeys\" WHERE \"Key\" = 'old-key'").FirstOrDefaultAsync());
        Assert.Equal(1, await db.Database.SqlQuery<int>($"SELECT COUNT(*)::int AS \"Value\" FROM \"IdempotencyKeys\" WHERE \"Key\" = 'new-key'").FirstOrDefaultAsync());
    }

    // ---- ledger & CUMP via the C# service -----------------------------------------------------
    [Fact]
    public async Task Ledger_service_keeps_average_cost_and_reconciles()
    {
        var (_, _, _, stockId) = await _pg.SeedStockAsync(10, 10m);
        using var s = _pg.NewScope();

        await s.Ledger.ReceiveAsync(stockId, 30, 14m, MovementType.Inbound, "PO", null);       // (10*10 + 30*14) / 40 = 13
        await s.Ledger.IssueAsync(stockId, 20, MovementType.Outbound, "CMD", null);

        var stock = await s.Db.Stocks.AsNoTracking().SingleAsync(x => x.Id == stockId);
        Assert.Equal(13m, stock.AverageCost);
        Assert.Equal(20m, stock.Quantity);
        await Assert.ThrowsAsync<InsufficientStockException>(() => s.Ledger.IssueAsync(stockId, 21, MovementType.Outbound, "CMD", null));
        Assert.Equal(0, await _pg.DriftRowsAsync());
    }

    [Fact]
    public async Task Adjusting_below_zero_is_refused()
    {
        var (_, _, _, stockId) = await _pg.SeedStockAsync(3);
        using var s = _pg.NewScope();
        await Assert.ThrowsAsync<InsufficientStockException>(() => s.Ledger.AdjustAsync(stockId, -4, "oops"));
        Assert.Equal(7m, await s.Ledger.AdjustAsync(stockId, 4, "found"));
    }

    // ---- purchase order receipt ---------------------------------------------------------------
    [Fact]
    public async Task Receiving_a_purchase_order_concurrently_counts_the_goods_once()
    {
        var (productId, _, locationId, stockId) = await _pg.SeedStockAsync(0);
        int poId;
        using (var s = _pg.NewScope())
        {
            var supplier = new Supplier { Name = "Sup " + Guid.NewGuid().ToString("N")[..6] };
            s.Db.Suppliers.Add(supplier);
            await s.Db.SaveChangesAsync();
            var po = new PurchaseOrder
            {
                OrderNumber = await s.Ledger.NextNumberAsync("po"),
                SupplierId = supplier.Id,
                Status = PurchaseOrderStatus.Ordered,
                Items = { new PurchaseOrderItem { ProductId = productId, Quantity = 12, UnitCost = 7m, TotalCost = 84m } }
            };
            s.Db.PurchaseOrders.Add(po);
            await s.Db.SaveChangesAsync();
            poId = po.Id;
        }

        await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            using var s = _pg.NewScope();
            var controller = new PurchaseOrdersController(s.Db, s.Ledger, NullLogger<PurchaseOrdersController>.Instance);
            await controller.ReceiveOrder(poId, locationId, CancellationToken.None);
        }));

        Assert.Equal(12m, await _pg.QuantityAsync(stockId));
        Assert.Equal(0, await _pg.DriftRowsAsync());
    }
}
