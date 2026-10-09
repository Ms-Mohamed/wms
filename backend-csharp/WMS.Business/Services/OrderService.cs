using Microsoft.EntityFrameworkCore;
using WMS.Business.DTOs;
using WMS.Business.Exceptions;
using WMS.Data;
using WMS.Data.Entities;

namespace WMS.Business.Services;

public class OrderService : IOrderService
{
    private readonly WmsDbContext _context;
    private readonly IStockLedger _ledger;

    public OrderService(WmsDbContext context, IStockLedger ledger)
    {
        _context = context;
        _ledger = ledger;
    }

    public async Task<OrderDto> CreateOrderAsync(CreateOrderDto dto, string? idempotencyKey = null, string? requestHash = null, CancellationToken ct = default)
    {
        if (dto.Items == null || dto.Items.Count == 0)
            throw new ArgumentException("Une commande doit contenir au moins un article");
        if (dto.Items.Any(i => i.Quantity <= 0))
            throw new ArgumentException("Les quantités doivent être supérieures à 0");

        await using var transaction = await _context.Database.BeginTransactionAsync(ct);

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            // If a concurrent request with the same key is still running, this INSERT waits for it to
            // commit and then inserts nothing: we return the order it created.
            var inserted = await _context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO \"IdempotencyKeys\" (\"Key\", \"Scope\", \"RequestHash\", \"CreatedAt\") VALUES ({idempotencyKey}, 'order.create', {requestHash}, now()) ON CONFLICT (\"Key\") DO NOTHING", ct);
            if (inserted == 0)
            {
                var existing = await _context.Database.SqlQuery<WMS.Data.Entities.IdempotencyKey>(
                    $"SELECT * FROM \"IdempotencyKeys\" WHERE \"Key\" = {idempotencyKey}")
                    .FirstOrDefaultAsync(ct);
                if (existing != null)
                {
                    if (existing.RequestHash != null && existing.RequestHash != requestHash)
                        throw new ArgumentException("Idempotency key reused with different request body");
                    if (existing.ResourceId > 0)
                    {
                        await transaction.DisposeAsync();
                        return await GetOrderByIdAsync(existing.ResourceId.Value, ct) ?? throw new InvalidOperationException("Commande introuvable");
                    }
                }
                // Unreachable: ResourceId is always set in the same transaction that created the key.
            }
        }

        var productIds = dto.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _context.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        var order = new Order
        {
            OrderNumber = await _ledger.NextNumberAsync("order", ct),
            CustomerName = dto.CustomerName,
            CustomerEmail = dto.CustomerEmail,
            CustomerAddress = dto.CustomerAddress,
            TaxRate = dto.TaxRate,
            Notes = dto.Notes,
            Status = OrderStatus.Pending,
            OrderDate = DateTime.UtcNow
        };

        decimal subTotal = 0;
        foreach (var itemDto in dto.Items)
        {
            if (!products.TryGetValue(itemDto.ProductId, out var product))
                throw new ArgumentException($"Produit avec ID {itemDto.ProductId} introuvable");

            // Stock is checked and deducted atomically at shipment, not here.
            var unitPrice = itemDto.UnitPrice ?? product.UnitPrice;
            var lineTotal = (itemDto.Quantity * unitPrice) - itemDto.Discount;

            order.Items.Add(new OrderItem
            {
                Order = order,
                ProductId = itemDto.ProductId,
                WarehouseId = itemDto.WarehouseId,
                Quantity = itemDto.Quantity,
                UnitPrice = unitPrice,
                UnitPriceAtSale = unitPrice,
                Discount = itemDto.Discount,
                LineTotal = lineTotal
            });
            subTotal += lineTotal;
        }

        order.SubTotal = subTotal;
        order.TaxAmount = subTotal * order.TaxRate;
        order.TotalAmount = order.SubTotal + order.TaxAmount;

        _context.Orders.Add(order);
        await _context.SaveChangesAsync(ct);

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"IdempotencyKeys\" SET \"ResourceId\" = {order.Id} WHERE \"Key\" = {idempotencyKey}", ct);
        }

        await transaction.CommitAsync(ct);

        return await GetOrderByIdAsync(order.Id, ct) ?? throw new InvalidOperationException("Erreur lors de la récupération de la commande créée");
    }

    /// <summary>
    /// Ships some or all of an order. Partial shipments are supported: each request lists order lines
    /// with the quantity to ship now, and the order stays PartiallyShipped until every line is complete.
    ///
    /// Concurrency model (all enforced by wms_order_ship_line in PostgreSQL, see stock_reservations.sql):
    ///  1. The order row is locked, so two simultaneous shipments of the same order are serialized and only
    ///     one of them can be the one that completes it (and so creates the invoice).
    ///  2. A line can never ship more than remains on it.
    ///  3. Units reserved for this order on that stock row are consumed first; extra units come from free stock.
    ///     Two orders competing for the last units: exactly one wins, the other gets InsufficientStockException.
    ///  4. The whole request is one transaction: any failure rolls back every line of this request.
    /// One invoice is issued, when the last line completes (it covers the whole order).
    /// </summary>
    public async Task<OrderDto> ShipOrderAsync(int orderId, ShipOrderDto dto, CancellationToken ct = default)
    {
        if (dto.Items == null || dto.Items.Count == 0)
            throw new InvalidOperationException("Aucun article à expédier");

        var order = await _context.Orders.AsNoTracking()
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order == null) throw new ArgumentException("Order not found");

        foreach (var line in dto.Items)
        {
            if (order.Items.All(i => i.Id != line.OrderItemId))
                throw new InvalidOperationException($"La ligne {line.OrderItemId} n'appartient pas à cette commande");
            if (line.Quantity <= 0)
                throw new InvalidOperationException("Les quantités expédiées doivent être supérieures à 0");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(ct);

        var status = order.Status;
        foreach (var line in dto.Items)
        {
            var orderItem = order.Items.First(i => i.Id == line.OrderItemId);

            var stockId = await _context.Stocks.AsNoTracking()
                .Where(s => s.ProductId == orderItem.ProductId && s.LocationId == line.LocationId)
                .Select(s => (int?)s.Id)
                .FirstOrDefaultAsync(ct);

            if (stockId == null)
                throw new InsufficientStockException(orderItem.ProductId, orderItem.Product.Code, line.Quantity, 0);

            try
            {
                status = await _ledger.ShipOrderLineAsync(orderItem.Id, stockId.Value, line.Quantity, order.OrderNumber, ct);
            }
            catch (InsufficientStockException ex)
            {
                // add the human-readable product code the database does not know
                throw new InsufficientStockException(orderItem.ProductId, orderItem.Product.Code, ex.RequiredQuantity, ex.AvailableQuantity);
            }
        }

        if (status == OrderStatus.Shipped)
            await CreateInvoiceForOrderAsync(order, ct);

        await transaction.CommitAsync(ct);

        return await GetOrderByIdAsync(orderId, ct) ?? throw new InvalidOperationException("Error retrieving updated order");
    }

    public async Task<OrderDto> ReserveOrderAsync(int orderId, CancellationToken ct = default)
    {
        try
        {
            await _ledger.ReserveOrderAsync(orderId, ct);
        }
        catch (InsufficientStockException ex)
        {
            var code = await _context.Products.AsNoTracking()
                .Where(p => p.Id == ex.ProductId).Select(p => p.Code).FirstOrDefaultAsync(ct);
            throw new InsufficientStockException(ex.ProductId, code ?? ex.ProductId.ToString(), ex.RequiredQuantity, ex.AvailableQuantity);
        }
        return await GetOrderByIdAsync(orderId, ct) ?? throw new ArgumentException("Order not found");
    }

    public async Task<OrderDto> CancelOrderAsync(int orderId, CancellationToken ct = default)
    {
        await _ledger.CancelOrderAsync(orderId, ct);
        return await GetOrderByIdAsync(orderId, ct) ?? throw new ArgumentException("Order not found");
    }

    public async Task<OrderDto?> GetOrderByIdAsync(int orderId, CancellationToken ct = default)
    {
        var order = await _context.Orders.AsNoTracking()
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .Include(o => o.Items).ThenInclude(i => i.Warehouse)
            .AsSplitQuery()
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);

        return order == null ? null : ToDto(order, await LoadReservedAsync(order.Items.Select(i => i.Id), ct));
    }

    public async Task<PagedResult<OrderDto>> GetOrdersAsync(int? page, int? pageSize, CancellationToken ct = default)
    {
        var (p, size) = Paging.Normalize(page, pageSize);

        var total = await _context.Orders.CountAsync(ct);
        var orders = await _context.Orders.AsNoTracking()
            .OrderByDescending(o => o.OrderDate).ThenByDescending(o => o.Id)
            .Skip((p - 1) * size).Take(size)
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .Include(o => o.Items).ThenInclude(i => i.Warehouse)
            .AsSplitQuery()
            .ToListAsync(ct);

        var reserved = await LoadReservedAsync(orders.SelectMany(o => o.Items).Select(i => i.Id), ct);

        return new PagedResult<OrderDto>
        {
            Items = orders.Select(o => ToDto(o, reserved)).ToList(),
            Page = p,
            PageSize = size,
            TotalCount = total
        };
    }

    private async Task CreateInvoiceForOrderAsync(Order order, CancellationToken ct)
    {
        // The order claim in ShipOrderAsync already guarantees this runs at most once per order;
        // this check is belt and braces (Invoice.OrderId is also unique in the model).
        if (await _context.Invoices.AnyAsync(i => i.OrderId == order.Id, ct)) return;

        var invoice = new Invoice
        {
            OrderId = order.Id,
            InvoiceNumber = await _ledger.NextNumberAsync("invoice", ct),
            InvoiceDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(30),
            Status = InvoiceStatus.Issued,
            SubTotal = order.SubTotal,
            TaxRate = order.TaxRate,
            TaxAmount = order.TaxAmount,
            TotalAmount = order.TotalAmount,
            Notes = order.Notes
        };

        foreach (var orderItem in order.Items)
        {
            invoice.Items.Add(new InvoiceItem
            {
                Invoice = invoice,
                ProductId = orderItem.ProductId,
                ProductCode = orderItem.Product.Code,
                ProductName = orderItem.Product.Name,
                Quantity = orderItem.Quantity,
                UnitPrice = orderItem.UnitPrice,
                Discount = orderItem.Discount,
                TaxRate = order.TaxRate,
                LineTotal = orderItem.LineTotal
            });
        }

        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync(ct);
    }

    private sealed class ReservedRow
    {
        public int OrderItemId { get; set; }
        public decimal Qty { get; set; }
    }

    private async Task<Dictionary<int, decimal>> LoadReservedAsync(IEnumerable<int> itemIds, CancellationToken ct)
    {
        var ids = itemIds.Distinct().ToArray();
        if (ids.Length == 0) return new Dictionary<int, decimal>();
        var rows = await _context.Database.SqlQuery<ReservedRow>(
            $"SELECT \"OrderItemId\", SUM(\"Quantity\") AS \"Qty\" FROM \"OrderItemReservations\" WHERE \"OrderItemId\" = ANY({ids}) GROUP BY \"OrderItemId\"")
            .ToListAsync(ct);
        return rows.ToDictionary(r => r.OrderItemId, r => r.Qty);
    }

    private static OrderDto ToDto(Order order, IReadOnlyDictionary<int, decimal> reserved) => new()
    {
        Id = order.Id,
        OrderNumber = order.OrderNumber,
        CustomerName = order.CustomerName,
        CustomerEmail = order.CustomerEmail,
        CustomerAddress = order.CustomerAddress,
        Status = order.Status.ToString(),
        OrderDate = order.OrderDate,
        SubTotal = order.SubTotal,
        TaxAmount = order.TaxAmount,
        TotalAmount = order.TotalAmount,
        Items = order.Items.Select(i => new OrderItemDto
        {
            Id = i.Id,
            ProductId = i.ProductId,
            ProductCode = i.Product.Code,
            ProductName = i.Product.Name,
            WarehouseId = i.WarehouseId,
            WarehouseName = i.Warehouse.Name,
            Quantity = i.Quantity,
            ShippedQuantity = i.ShippedQuantity,
            ReservedQuantity = reserved.GetValueOrDefault(i.Id),
            UnitPrice = i.UnitPrice,
            Discount = i.Discount,
            LineTotal = i.LineTotal
        }).ToList()
    };
}
