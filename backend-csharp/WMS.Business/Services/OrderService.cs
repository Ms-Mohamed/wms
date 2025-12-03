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
            // Générer le numéro de commande
            var orderNumber = await GenerateOrderNumberAsync();

            // Créer la commande
            var order = new Order
            {
                OrderNumber = orderNumber,
                CustomerName = createOrderDto.CustomerName,
                CustomerEmail = createOrderDto.CustomerEmail,
                CustomerAddress = createOrderDto.CustomerAddress,
                TaxRate = createOrderDto.TaxRate,
                Notes = createOrderDto.Notes,
                Status = OrderStatus.Confirmed,
                OrderDate = DateTime.UtcNow
            };

            decimal subTotal = 0;

            // Traiter chaque ligne de commande
            foreach (var itemDto in createOrderDto.Items)
            {
                // Vérifier le produit
                var product = await _context.Products
                    .FirstOrDefaultAsync(p => p.Id == itemDto.ProductId);

                if (product == null)
                {
                    throw new ArgumentException($"Produit avec ID {itemDto.ProductId} introuvable");
                }

                // Vérifier le stock disponible
                var stock = await _context.Stocks
                    .Include(s => s.Product)
                    .Include(s => s.Warehouse)
                    .FirstOrDefaultAsync(s => s.ProductId == itemDto.ProductId && s.WarehouseId == itemDto.WarehouseId);

                if (stock == null || stock.AvailableQuantity < itemDto.Quantity)
                {
                    var availableQty = stock?.AvailableQuantity ?? 0;
                    throw new InsufficientStockException(
                        itemDto.ProductId,
                        product.Code,
                        itemDto.Quantity,
                        availableQty
                    );
                }

                // Calculer le prix unitaire (utiliser celui fourni ou celui du produit)
                var unitPrice = itemDto.UnitPrice ?? product.UnitPrice;

                // Calculer le total de la ligne
                var lineTotal = (itemDto.Quantity * unitPrice) - itemDto.Discount;

                // Créer la ligne de commande
                var orderItem = new OrderItem
                {
                    Order = order,
                    ProductId = itemDto.ProductId,
                    WarehouseId = itemDto.WarehouseId,
                    Quantity = itemDto.Quantity,
                    UnitPrice = unitPrice,
                    UnitPriceAtSale = unitPrice, // Prix au moment de la vente
                    Discount = itemDto.Discount,
                    LineTotal = lineTotal
                };

                order.Items.Add(orderItem);
                subTotal += lineTotal;

                // Décrémenter le stock (dans la transaction)
                stock.Quantity -= itemDto.Quantity;
                stock.LastUpdated = DateTime.UtcNow;

                // Enregistrer le mouvement de stock
                var movement = new StockMovement
                {
                    StockId = stock.Id,
                    Type = MovementType.Outbound,
                    Quantity = itemDto.Quantity,
                    UnitCost = stock.AverageCost,
                    Reference = orderNumber,
                    Notes = $"Vente - Commande {orderNumber}"
                };

                _context.StockMovements.Add(movement);
            }

            // Calculer les totaux
            order.SubTotal = subTotal;
            order.TaxAmount = subTotal * order.TaxRate;
            order.TotalAmount = order.SubTotal + order.TaxAmount;

            // Sauvegarder la commande
            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            // Créer automatiquement la facture
            await CreateInvoiceForOrderAsync(order.Id);

            await transaction.CommitAsync();

            return await GetOrderByIdAsync(order.Id) ?? throw new Exception("Erreur lors de la récupération de la commande créée");
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

