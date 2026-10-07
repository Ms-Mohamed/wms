using Microsoft.EntityFrameworkCore;
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
    public async Task Partial_shipments_and_foreign_lines_are_rejected_before_touching_stock()
    {
        var (productId, warehouseId, locationId, stockId) = await _pg.SeedStockAsync(10);
        var order = await CreateOrderAsync(productId, warehouseId, 4);
        using var s = _pg.NewScope();

        await Assert.ThrowsAsync<InvalidOperationException>(() => s.Orders.ShipOrderAsync(order.Id, new ShipOrderDto
        { Items = { new ShipOrderItemDto { OrderItemId = order.Items[0].Id, LocationId = locationId, Quantity = 2 } } }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.Orders.ShipOrderAsync(order.Id, new ShipOrderDto
        { Items = { new ShipOrderItemDto { OrderItemId = 999999, LocationId = locationId, Quantity = 4 } } }));

        Assert.Equal(10m, await _pg.QuantityAsync(stockId));
    }

    // ---- idempotency & numbering --------------------------------------------------------------
    [Fact]
    public async Task Retrying_a_create_with_the_same_idempotency_key_returns_the_same_order()
    {
        var (productId, warehouseId, _, _) = await _pg.SeedStockAsync(5);
        var key = Guid.NewGuid().ToString();

        var created = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => CreateOrderAsync(productId, warehouseId, 1, key)));

        Assert.Single(created.Select(o => o.Id).Distinct());
        await using var db = _pg.NewContext();
        Assert.Equal(1, await db.Orders.CountAsync(o => o.Items.Any(i => i.ProductId == productId)));
    }

    [Fact]
    public async Task Concurrent_order_creation_never_produces_duplicate_numbers()
    {
        var (productId, warehouseId, _, _) = await _pg.SeedStockAsync(5);

        var orders = await Task.WhenAll(Enumerable.Range(0, 40).Select(_ => CreateOrderAsync(productId, warehouseId, 1)));

        Assert.Equal(40, orders.Select(o => o.OrderNumber).Distinct().Count());
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
