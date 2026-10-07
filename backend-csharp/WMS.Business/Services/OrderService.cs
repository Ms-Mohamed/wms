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
                        return await GetOrderByIdAsync(existing.ResourceId.Value, ct) ?? throw new InvalidOperationException("Commande introuvable");
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
    /// Concurrency model:
    ///  1. The order is claimed with one conditional UPDATE (Pending/Confirmed/Processing -> Shipped).
    ///     Two simultaneous ship requests: one gets 1 row, the other 0 and is refused. No double shipment.
    ///  2. Each line is removed from stock by an atomic statement (IStockLedger.IssueAsync). Two orders
    ///     competing for the last units: exactly one wins, the other gets InsufficientStockException.
    ///  3. Everything is one transaction: any failure rolls back the claim, all issued lines, and the invoice.
    /// Partial shipments are not supported: every order line must be shipped in full.
    /// </summary>
    public async Task<OrderDto> ShipOrderAsync(int orderId, ShipOrderDto dto, CancellationToken ct = default)
    {
        if (dto.Items == null || dto.Items.Count == 0)
            throw new InvalidOperationException("Aucun article à expédier");

        var order = await _context.Orders.AsNoTracking()
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order == null) throw new ArgumentException("Order not found");

        // Validate the request against the order before touching anything.
        var shippedPerItem = new Dictionary<int, decimal>();
        foreach (var line in dto.Items)
        {
            var orderItem = order.Items.FirstOrDefault(i => i.Id == line.OrderItemId)
                ?? throw new InvalidOperationException($"La ligne {line.OrderItemId} n'appartient pas à cette commande");
            if (line.Quantity <= 0)
                throw new InvalidOperationException("Les quantités expédiées doivent être supérieures à 0");
            shippedPerItem[orderItem.Id] = shippedPerItem.GetValueOrDefault(orderItem.Id) + line.Quantity;
        }
        foreach (var orderItem in order.Items)
        {
            if (shippedPerItem.GetValueOrDefault(orderItem.Id) != orderItem.Quantity)
                throw new InvalidOperationException(
                    $"Expédition partielle non supportée: la ligne {orderItem.Id} doit être expédiée en totalité ({orderItem.Quantity})");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(ct);

        var claimed = await _context.Database.ExecuteSqlInterpolatedAsync(
            $@"UPDATE ""Orders""
                  SET ""Status"" = {(int)OrderStatus.Shipped}, ""ShippedDate"" = now(), ""UpdatedAt"" = now()
                WHERE ""Id"" = {orderId}
                  AND ""Status"" IN ({(int)OrderStatus.Pending}, {(int)OrderStatus.Confirmed}, {(int)OrderStatus.Processing})", ct);
        if (claimed == 0)
            throw new InvalidOperationException("Order is already shipped or cancelled");

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
                await _ledger.IssueAsync(stockId.Value, line.Quantity, MovementType.Outbound,
                    order.OrderNumber, $"Shipment - Order {order.OrderNumber}", ct);
            }
            catch (InsufficientStockException ex)
            {
                // add the human-readable product code the database does not know
                throw new InsufficientStockException(orderItem.ProductId, orderItem.Product.Code, ex.RequiredQuantity, ex.AvailableQuantity);
            }
        }

        await CreateInvoiceForOrderAsync(order, ct);

        await transaction.CommitAsync(ct);

        return await GetOrderByIdAsync(orderId, ct) ?? throw new InvalidOperationException("Error retrieving updated order");
    }

    public async Task<OrderDto?> GetOrderByIdAsync(int orderId, CancellationToken ct = default)
    {
        var order = await _context.Orders.AsNoTracking()
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .Include(o => o.Items).ThenInclude(i => i.Warehouse)
            .AsSplitQuery()
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);

        return order == null ? null : ToDto(order);
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

        return new PagedResult<OrderDto>
        {
            Items = orders.Select(ToDto).ToList(),
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

    private static OrderDto ToDto(Order order) => new()
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
            UnitPrice = i.UnitPrice,
            Discount = i.Discount,
            LineTotal = i.LineTotal
        }).ToList()
    };
}
