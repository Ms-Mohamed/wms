using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WMS.Business.Services;
using WMS.Data;
using WMS.Data.Entities;

namespace WMS.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ReturnsController : ControllerBase
{
    private readonly WmsDbContext _context;
    private readonly IStockLedger _ledger;
    private readonly ILogger<ReturnsController> _logger;

    public ReturnsController(WmsDbContext context, IStockLedger ledger, ILogger<ReturnsController> logger)
    {
        _context = context;
        _ledger = ledger;
        _logger = logger;
    }

    // POST: api/Returns
    [HttpPost]
    public async Task<ActionResult<ReturnOrder>> CreateReturn(CreateReturnDto dto)
    {
        // Vérifier si la commande existe
        var order = await _context.Orders.FindAsync(dto.OrderId);
        if (order == null) return NotFound("Commande introuvable.");

        var returnOrder = new ReturnOrder
        {
            ReturnNumber = await _ledger.NextNumberAsync("rma"),
            OrderId = dto.OrderId,
            Reason = dto.Reason,
            Status = ReturnStatus.Requested,
            Items = dto.Items.Select(i => new ReturnOrderItem
            {
                ProductId = i.ProductId,
                Quantity = i.Quantity,
                Condition = i.Condition
            }).ToList()
        };

        _context.ReturnOrders.Add(returnOrder);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetReturn), new { id = returnOrder.Id }, returnOrder);
    }

    // GET: api/Returns/5
    [HttpGet("{id}")]
    public async Task<ActionResult<ReturnOrder>> GetReturn(int id)
    {
        var ret = await _context.ReturnOrders
            .Include(r => r.Items)
            .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (ret == null) return NotFound();

        return ret;
    }

    // POST: api/Returns/5/receive
    /// <summary>Receives a customer return. Claimed atomically (cannot be received twice); good items go back to stock through the ledger.</summary>
    [HttpPost("{id}/receive")]
    public async Task<IActionResult> ReceiveReturn(int id, CancellationToken ct)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            var returnOrder = await _context.ReturnOrders.AsNoTracking()
                .Include(r => r.Items)
                .FirstOrDefaultAsync(r => r.Id == id, ct);
            if (returnOrder == null) return NotFound();

            var claimed = await _context.Database.ExecuteSqlInterpolatedAsync(
                $@"UPDATE ""ReturnOrders""
                      SET ""Status"" = {(int)ReturnStatus.Received}, ""ReceivedDate"" = now(), ""UpdatedAt"" = now()
                    WHERE ""Id"" = {id}
                      AND ""Status"" NOT IN ({(int)ReturnStatus.Received}, {(int)ReturnStatus.Refunded}, {(int)ReturnStatus.Rejected})", ct);
            if (claimed == 0)
            {
                return BadRequest("Le retour a déjà été traité.");
            }

            // warehouse each product was sold from (fallback: warehouse 1, as before)
            var soldFrom = await _context.OrderItems.AsNoTracking()
                .Where(oi => oi.OrderId == returnOrder.OrderId)
                .Select(oi => new { oi.ProductId, oi.WarehouseId })
                .ToListAsync(ct);

            foreach (var item in returnOrder.Items.Where(i => i.Condition == ReturnCondition.Good))
            {
                var warehouseId = soldFrom.FirstOrDefault(x => x.ProductId == item.ProductId)?.WarehouseId ?? 1;

                var stockId = await _context.Stocks.AsNoTracking()
                    .Where(s => s.ProductId == item.ProductId && s.WarehouseId == warehouseId)
                    .OrderBy(s => s.Id)
                    .Select(s => (int?)s.Id)
                    .FirstOrDefaultAsync(ct)
                    ?? await _ledger.GetOrCreateStockIdAsync(item.ProductId, warehouseId, null, 0, ct);

                // back into stock at the current average cost (the average does not move)
                await _ledger.ReceiveAsync(stockId, item.Quantity, null, MovementType.Inbound,
                    returnOrder.ReturnNumber, "Retour Client (RMA)", ct);
            }
            // damaged items are not restocked (out of MVP scope)

            await transaction.CommitAsync(ct);
            return Ok(new { message = "Retour réceptionné avec succès." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la réception du retour.");
            return StatusCode(500, "Erreur interne.");
        }
    }
}

public class CreateReturnDto
{
    public int OrderId { get; set; }
    public string? Reason { get; set; }
    public List<CreateReturnItemDto> Items { get; set; } = new();
}

public class CreateReturnItemDto
{
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public ReturnCondition Condition { get; set; }
}
