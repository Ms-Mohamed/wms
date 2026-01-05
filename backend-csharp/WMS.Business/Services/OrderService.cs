using Microsoft.EntityFrameworkCore;
using WMS.Business.DTOs;
using WMS.Business.Exceptions;
using WMS.Data;
using WMS.Data.Entities;

namespace WMS.Business.Services;

public class OrderService : IOrderService
{
    private readonly WmsDbContext _context;

    public OrderService(WmsDbContext context)
    {
        _context = context;
    }

    public async Task<OrderDto> CreateOrderAsync(CreateOrderDto createOrderDto)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var orderNumber = await GenerateOrderNumberAsync();

            var order = new Order
            {
                OrderNumber = orderNumber,
                CustomerName = createOrderDto.CustomerName,
                CustomerEmail = createOrderDto.CustomerEmail,
                CustomerAddress = createOrderDto.CustomerAddress,
                TaxRate = createOrderDto.TaxRate,
                Notes = createOrderDto.Notes,
                Status = OrderStatus.Pending, // Changed to Pending
                OrderDate = DateTime.UtcNow
            };

            decimal subTotal = 0;

            foreach (var itemDto in createOrderDto.Items)
            {
                var product = await _context.Products
                    .FirstOrDefaultAsync(p => p.Id == itemDto.ProductId);

                if (product == null)
                {
                    throw new ArgumentException($"Produit avec ID {itemDto.ProductId} introuvable");
                }

                // Removed strict stock validation here since we will validate at shipment
                // Removed stock deduction logic
                // Removed StockMovement creation

                var unitPrice = itemDto.UnitPrice ?? product.UnitPrice;
                var lineTotal = (itemDto.Quantity * unitPrice) - itemDto.Discount;

                var orderItem = new OrderItem
                {
                    Order = order,
                    ProductId = itemDto.ProductId,
                    WarehouseId = itemDto.WarehouseId,
                    Quantity = itemDto.Quantity,
                    UnitPrice = unitPrice,
                    UnitPriceAtSale = unitPrice,
                    Discount = itemDto.Discount,
                    LineTotal = lineTotal
                };

                order.Items.Add(orderItem);
                subTotal += lineTotal;
            }

            order.SubTotal = subTotal;
            order.TaxAmount = subTotal * order.TaxRate;
            order.TotalAmount = order.SubTotal + order.TaxAmount;

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            // Invoice creation removed from here (moved to shipment)

            await transaction.CommitAsync();

            return await GetOrderByIdAsync(order.Id) ?? throw new Exception("Erreur lors de la récupération de la commande créée");
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<OrderDto> ShipOrderAsync(int orderId, ShipOrderDto shipOrderDto)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null) throw new ArgumentException("Order not found");
            if (order.Status == OrderStatus.Shipped || order.Status == OrderStatus.Delivered)
                throw new InvalidOperationException("Order is already shipped");

            foreach (var shipItem in shipOrderDto.Items)
            {
                var orderItem = order.Items.FirstOrDefault(i => i.Id == shipItem.OrderItemId);
                if (orderItem == null) continue;

                // Find stock in the specified location
                var stock = await _context.Stocks
                    .FirstOrDefaultAsync(s => s.ProductId == orderItem.ProductId && s.LocationId == shipItem.LocationId);

                if (stock == null || stock.AvailableQuantity < shipItem.Quantity)
                {
                    throw new InsufficientStockException(orderItem.ProductId, "Unknown", shipItem.Quantity, stock?.AvailableQuantity ?? 0);
                }

                // Deduct Stock
                stock.Quantity -= shipItem.Quantity;
                stock.LastUpdated = DateTime.UtcNow;

                // Create Movement
                var movement = new StockMovement
                {
                    StockId = stock.Id,
                    Type = MovementType.Outbound,
                    Quantity = shipItem.Quantity,
                    UnitCost = stock.AverageCost,
                    Reference = order.OrderNumber,
                    Notes = $"Shipment - Order {order.OrderNumber}"
                };
                _context.StockMovements.Add(movement);
            }

            order.Status = OrderStatus.Shipped;
            order.ShippedDate = DateTime.UtcNow;
            
            await _context.SaveChangesAsync();

            await CreateInvoiceForOrderAsync(order.Id);

            await transaction.CommitAsync();

            return await GetOrderByIdAsync(order.Id) ?? throw new Exception("Error retrieving updated order");
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<OrderDto?> GetOrderByIdAsync(int orderId)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .Include(o => o.Items)
                .ThenInclude(i => i.Warehouse)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order == null)
            return null;

        return new OrderDto
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

    public async Task<List<OrderDto>> GetAllOrdersAsync()
    {
        var orders = await _context.Orders
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .Include(o => o.Items)
                .ThenInclude(i => i.Warehouse)
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync();

        return orders.Select(order => new OrderDto
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
        }).ToList();
    }

    private async Task<string> GenerateOrderNumberAsync()
    {
        var today = DateTime.UtcNow;
        var prefix = $"CMD-{today:yyyyMMdd}-";
        var lastOrder = await _context.Orders
            .Where(o => o.OrderNumber.StartsWith(prefix))
            .OrderByDescending(o => o.OrderNumber)
            .FirstOrDefaultAsync();

        int sequence = 1;
        if (lastOrder != null)
        {
            var lastSequence = lastOrder.OrderNumber.Substring(prefix.Length);
            if (int.TryParse(lastSequence, out var lastSeq))
            {
                sequence = lastSeq + 1;
            }
        }

        return $"{prefix}{sequence:D4}";
    }

    private async Task CreateInvoiceForOrderAsync(int orderId)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order == null || order.Invoice != null)
            return;

        var invoiceNumber = await GenerateInvoiceNumberAsync();

        var invoice = new Invoice
        {
            OrderId = orderId,
            InvoiceNumber = invoiceNumber,
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
            var invoiceItem = new InvoiceItem
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
            };

            invoice.Items.Add(invoiceItem);
        }

        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync();
    }

    private async Task<string> GenerateInvoiceNumberAsync()
    {
        var today = DateTime.UtcNow;
        var prefix = $"FAC-{today:yyyyMMdd}-";
        var lastInvoice = await _context.Invoices
            .Where(i => i.InvoiceNumber.StartsWith(prefix))
            .OrderByDescending(i => i.InvoiceNumber)
            .FirstOrDefaultAsync();

        int sequence = 1;
        if (lastInvoice != null)
        {
            var lastSequence = lastInvoice.InvoiceNumber.Substring(prefix.Length);
            if (int.TryParse(lastSequence, out var lastSeq))
            {
                sequence = lastSeq + 1;
            }
        }

        return $"{prefix}{sequence:D4}";
    }
}

