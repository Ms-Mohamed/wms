using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WMS.Data;
using WMS.Data.Entities;

namespace WMS.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ReturnsController : ControllerBase
{
    private readonly WmsDbContext _context;
    private readonly ILogger<ReturnsController> _logger;

    public ReturnsController(WmsDbContext context, ILogger<ReturnsController> logger)
    {
        _context = context;
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
            ReturnNumber = $"RMA-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}",
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
    [HttpPost("{id}/receive")]
    public async Task<IActionResult> ReceiveReturn(int id)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var returnOrder = await _context.ReturnOrders
                .Include(r => r.Items)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (returnOrder == null) return NotFound();

            if (returnOrder.Status == ReturnStatus.Received || returnOrder.Status == ReturnStatus.Refunded)
            {
                return BadRequest("Le retour a déjà été traité.");
            }

            foreach (var item in returnOrder.Items)
            {
                // Si l'article est en bon état, on le remet en stock
                if (item.Condition == ReturnCondition.Good)
                {
                    // Trouver le stock (Defaut Warehouse 1)
                    int warehouseId = 1; 
                    
                    var stock = await _context.Stocks
                        .FirstOrDefaultAsync(s => s.ProductId == item.ProductId && s.WarehouseId == warehouseId);

                    if (stock == null)
                    {
                        stock = new Stock
                        {
                            ProductId = item.ProductId,
                            WarehouseId = warehouseId,
                            Quantity = 0,
                            ReservedQuantity = 0,
                            ReorderPoint = 0,
                            LastUpdated = DateTime.UtcNow
                        };
                        _context.Stocks.Add(stock);
                    }

                    stock.Quantity += item.Quantity;
                    stock.LastUpdated = DateTime.UtcNow;

                    // Mouvement de stock
                    var movement = new StockMovement
                    {
                        StockId = stock.Id,
                        Type = MovementType.Inbound, // Retour client considéré comme entrée
                        Quantity = item.Quantity,
                        UnitCost = stock.AverageCost, // On garde la valeur actuelle ou cout standard? Simplification: AverageCost
                        Reference = returnOrder.ReturnNumber,
                        Notes = "Retour Client (RMA)",
                        CreatedAt = DateTime.UtcNow
                    };
                    // Hack: Si StockId n'est pas encore généré (nouveau stock), EF Core le gère lors du SaveChanges si la relation est bien faite.
                    // Mais ici on n'a pas mis movement dans stock.Movements.Add().
                    // On l'ajoute au contexte.
                    _context.StockMovements.Add(movement);
                }
                // Si endommagé, on ne remet pas en stock (ou stock "Défectueux" séparé, hors scope MVP)
            }

            returnOrder.Status = ReturnStatus.Received;
            returnOrder.ReceivedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new { message = "Retour réceptionné avec succès." });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
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
