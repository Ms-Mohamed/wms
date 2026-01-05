using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WMS.Data;
using WMS.Data.Entities;

namespace WMS.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class PurchaseOrdersController : ControllerBase
{
    private readonly WmsDbContext _context;
    private readonly ILogger<PurchaseOrdersController> _logger;

    public PurchaseOrdersController(WmsDbContext context, ILogger<PurchaseOrdersController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // GET: api/PurchaseOrders
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PurchaseOrder>>> GetPurchaseOrders()
    {
        try
        {
            return await _context.PurchaseOrders
                .Include(p => p.Supplier)
                .Include(p => p.Items)
                .OrderByDescending(p => p.OrderDate)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting purchase orders");
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    // GET: api/PurchaseOrders/5
    [HttpGet("{id}")]
    public async Task<ActionResult<PurchaseOrder>> GetPurchaseOrder(int id)
    {
        var purchaseOrder = await _context.PurchaseOrders
            .Include(p => p.Supplier)
            .Include(p => p.Items)
            .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (purchaseOrder == null)
        {
            return NotFound();
        }

        return purchaseOrder;
    }

    // POST: api/PurchaseOrders
    [HttpPost]
    public async Task<ActionResult<PurchaseOrder>> PostPurchaseOrder(CreatePurchaseOrderDto dto)
    {
        try
        {
            var purchaseOrder = new PurchaseOrder
            {
                OrderNumber = $"PO-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}",
                SupplierId = dto.SupplierId,
                ExpectedDate = dto.ExpectedDate.HasValue ? DateTime.SpecifyKind(dto.ExpectedDate.Value, DateTimeKind.Utc) : null,
                Notes = dto.Notes,
                Status = PurchaseOrderStatus.Ordered,
                Items = dto.Items.Select(i => new PurchaseOrderItem
                {
                    ProductId = i.ProductId,
                    Quantity = i.Quantity,
                    UnitCost = i.UnitCost,
                    TotalCost = i.Quantity * i.UnitCost
                }).ToList()
            };

            purchaseOrder.TotalAmount = purchaseOrder.Items.Sum(i => i.TotalCost);

            _context.PurchaseOrders.Add(purchaseOrder);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetPurchaseOrder", new { id = purchaseOrder.Id }, purchaseOrder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating purchase order");
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    // POST: api/PurchaseOrders/5/receive
    [HttpPost("{id}/receive")]
    public async Task<IActionResult> ReceiveOrder(int id, [FromQuery] int? locationId)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var order = await _context.PurchaseOrders
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (order == null) return NotFound();

            if (order.Status == PurchaseOrderStatus.Received)
            {
                return BadRequest("La commande a déjà été réceptionnée.");
            }

            // Default location logic
            int targetWarehouseId = 1;
            int targetLocationId = locationId ?? 1; // Default to 1 if not provided

            // Check if target location exists
            var locationExists = await _context.Locations.AnyAsync(l => l.Id == targetLocationId);
            if (!locationExists)
            {
                 return BadRequest($"L'emplacement ID {targetLocationId} n'existe pas.");
            }

            // Réceptionner chaque article
            foreach (var item in order.Items)
            {
                var stock = await _context.Stocks
                    .FirstOrDefaultAsync(s => s.ProductId == item.ProductId && s.LocationId == targetLocationId);

                if (stock == null)
                {
                    stock = new Stock
                    {
                        ProductId = item.ProductId,
                        WarehouseId = targetWarehouseId,
                        LocationId = targetLocationId,
                        Quantity = 0,
                        ReservedQuantity = 0,
                        ReorderPoint = 10, // Valeur par défaut
                        LastUpdated = DateTime.UtcNow
                    };
                    _context.Stocks.Add(stock);
                }

                // Mettre à jour le coût moyen (Weighted Average Cost)
                decimal totalValue = (stock.Quantity * stock.AverageCost) + (item.Quantity * item.UnitCost);
                decimal totalQuantity = stock.Quantity + item.Quantity;
                
                if (totalQuantity > 0)
                {
                    stock.AverageCost = totalValue / totalQuantity;
                }

                // Augmenter la quantité
                stock.Quantity += item.Quantity;
                stock.LastUpdated = DateTime.UtcNow;
            }

            order.Status = PurchaseOrderStatus.Received;
            order.ReceivedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new { message = "Commande réceptionnée avec succès", orderId = id, locationId = targetLocationId });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Erreur lors de la réception de la commande {OrderId}", id);
            return StatusCode(500, "Une erreur s'est produite lors de la réception.");
        }
    }
}

public class CreatePurchaseOrderDto
{
    public int SupplierId { get; set; }
    public DateTime? ExpectedDate { get; set; }
    public string? Notes { get; set; }
    public List<CreatePurchaseOrderItemDto> Items { get; set; } = new();
}

public class CreatePurchaseOrderItemDto
{
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
}
