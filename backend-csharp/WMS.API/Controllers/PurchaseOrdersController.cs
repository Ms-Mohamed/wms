using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WMS.Business.Exceptions;
using WMS.Business.Services;
using WMS.Data;
using WMS.Data.Entities;

namespace WMS.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class PurchaseOrdersController : ControllerBase
{
    private readonly WmsDbContext _context;
    private readonly IStockLedger _ledger;
    private readonly ILogger<PurchaseOrdersController> _logger;

    public PurchaseOrdersController(WmsDbContext context, IStockLedger ledger, ILogger<PurchaseOrdersController> logger)
    {
        _context = context;
        _ledger = ledger;
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
                OrderNumber = await _ledger.NextNumberAsync("po"),
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
    /// <summary>
    /// Receives a purchase order into a location. The order is claimed with one conditional UPDATE, so a
    /// double click / retry / two users can never receive (and count) the same goods twice. Each line goes
    /// through the stock ledger (incremental CUMP + ledger row). All-or-nothing.
    /// </summary>
    [HttpPost("{id}/receive")]
    public async Task<IActionResult> ReceiveOrder(int id, [FromQuery] int? locationId, CancellationToken ct)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            var order = await _context.PurchaseOrders.AsNoTracking()
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.Id == id, ct);
            if (order == null) return NotFound();

            var targetLocationId = locationId ?? 1; // legacy default kept for the existing UI
            var location = await _context.Locations.AsNoTracking().FirstOrDefaultAsync(l => l.Id == targetLocationId, ct);
            if (location == null)
            {
                return BadRequest($"L'emplacement ID {targetLocationId} n'existe pas.");
            }

            var claimed = await _context.Database.ExecuteSqlInterpolatedAsync(
                $@"UPDATE ""PurchaseOrders""
                      SET ""Status"" = {(int)PurchaseOrderStatus.Received}, ""ReceivedDate"" = now(), ""UpdatedAt"" = now()
                    WHERE ""Id"" = {id}
                      AND ""Status"" NOT IN ({(int)PurchaseOrderStatus.Received}, {(int)PurchaseOrderStatus.Cancelled})", ct);
            if (claimed == 0)
            {
                return BadRequest("La commande a déjà été réceptionnée (ou annulée).");
            }

            foreach (var item in order.Items)
            {
                // the stock row lives in the location's own warehouse (was hard-coded to warehouse 1)
                var stockId = await _ledger.GetOrCreateStockIdAsync(item.ProductId, location.WarehouseId, location.Id, 10, ct);
                await _ledger.ReceiveAsync(stockId, item.Quantity, item.UnitCost, MovementType.Inbound,
                    order.OrderNumber, $"Réception {order.OrderNumber}", ct);
            }

            await transaction.CommitAsync(ct);
            return Ok(new { message = "Commande réceptionnée avec succès", orderId = id, locationId = targetLocationId });
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or InsufficientStockException)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
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
