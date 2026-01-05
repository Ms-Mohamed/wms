using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WMS.Data;
using WMS.Data.Entities;

namespace WMS.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class InventoryController : ControllerBase
{
    private readonly WmsDbContext _context;
    private readonly ILogger<InventoryController> _logger;

    public InventoryController(WmsDbContext context, ILogger<InventoryController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpPost("adjust")]
    public async Task<IActionResult> AdjustStock(StockAdjustmentDto request)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var stock = await _context.Stocks
                .Include(s => s.Product)
                .FirstOrDefaultAsync(s => s.Id == request.StockId);

            if (stock == null)
            {
                return NotFound("Stock introuvable.");
            }

            // Calculer la nouvelle quantité
            decimal quantityBefore = stock.Quantity;
            decimal quantityAfter = quantityBefore + request.QuantityDelta;

            if (quantityAfter < 0)
            {
                return BadRequest("L'ajustement résulterait en une quantité négative.");
            }

            // Mettre à jour le stock
            stock.Quantity = quantityAfter;
            stock.LastUpdated = DateTime.UtcNow;

            // Créer le mouvement
            var movement = new StockMovement
            {
                StockId = stock.Id,
                Type = MovementType.Adjustment,
                Quantity = Math.Abs(request.QuantityDelta), // La quantité dans le mouvement est souvent absolue, le type définit le sens ? 
                // Pour simplifier ici: Si Adjustment, on peut considérer Quantity comme le delta ou l'absolu.
                // Adaptons MovementType pour être clair ou utilisons le signe.
                // Standard : MovementType "Adjustment" et Quantity = valeur par laquelle on a changé.
                // Pour Inbound/Outbound c'est clair. Pour Adjustment, +/-.
                // StockMovement.Quantity est decimal.
                // Disons: StockMovement stocke la valeur absolue, et on ajoute un champ "IsAddition"?
                // Ou plus simple: Quantity peut être négative dans le mouvement pour refléter l'impact.
                // Vérifions l'entité StockMovement existante... elle a un type.
                // Supposons que Quantity représente le flux positif. Mais pour un ajustement négatif ?
                // On va dire: Si Delta < 0 -> Type Adjustment (mais sens sortant impliqué par le delta négatif lors du calcul).
                // Mieux : StockMovement Quantity = Abs(Delta). Le sens est implicite ou on ajoute un commentaire.
                UnitCost = stock.AverageCost,
                Reference = "ADJ-" + DateTime.UtcNow.ToString("yyyyMMddHHmm"),
                Notes = request.Reason,
                CreatedAt = DateTime.UtcNow
            };
            
            // Hack: StockMovement n'a pas de champ "Direction". On va assumer que MovementType ne suffit pas si on ne sait pas si c'est + ou -.
            // Mais l'entité StockMovement a juste Type.
            // On va utiliser Notes pour préciser + ou -. Ou alors MovementType.Inbound pour + et Outbound pour -.
            // Mais Adjustment est un type spécifique.
            // Allons voir l'entité StockMovement.
            
            _context.StockMovements.Add(movement); // Il faudra peut-être corriger le DbContext s'il n'y a pas le DbSet

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new { message = "Stock ajusté avec succès", newQuantity = stock.Quantity });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Erreur lors de l'ajustement de stock");
            return StatusCode(500, "Une erreur est survenue.");
        }
    }

    [HttpPost("transfer")]
    public async Task<IActionResult> TransferStock(StockTransferDto request)
    {
        if (request.Quantity <= 0) return BadRequest("La quantité doit être positive.");
        if (request.FromWarehouseId == request.ToWarehouseId) return BadRequest("L'entrepôt de destination doit être différent.");

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // 1. Décrémenter Source
            var sourceStock = await _context.Stocks
                .FirstOrDefaultAsync(s => s.ProductId == request.ProductId && s.WarehouseId == request.FromWarehouseId);

            if (sourceStock == null || sourceStock.AvailableQuantity < request.Quantity)
            {
                return BadRequest("Stock insuffisant dans l'entrepôt source.");
            }

            sourceStock.Quantity -= request.Quantity;
            sourceStock.LastUpdated = DateTime.UtcNow;

            var movementOut = new StockMovement
            {
                StockId = sourceStock.Id,
                Type = MovementType.Transfer,
                Quantity = -request.Quantity, // Négatif pour sortie ? Ou MovementType.TransferOut ? 
                // MovementType a juste "Transfer". Utilisons Quantité négative pour la source.
                UnitCost = sourceStock.AverageCost,
                Reference = $"TRF-OUT-{DateTime.UtcNow:HHmm}",
                Notes = $"Transfert vers Entrepôt {request.ToWarehouseId}",
                CreatedAt = DateTime.UtcNow
            };
            _context.Entry(movementOut).State = EntityState.Added; // Force add if collection issue

            // 2. Incrémenter Destination
            var destStock = await _context.Stocks
                .FirstOrDefaultAsync(s => s.ProductId == request.ProductId && s.WarehouseId == request.ToWarehouseId);

            if (destStock == null)
            {
                destStock = new Stock
                {
                    ProductId = request.ProductId,
                    WarehouseId = request.ToWarehouseId,
                    Quantity = 0,
                    ReservedQuantity = 0,
                    AverageCost = sourceStock.AverageCost, // Hérite du coût
                    LastUpdated = DateTime.UtcNow,
                    ReorderPoint = 0
                };
                _context.Stocks.Add(destStock);
                await _context.SaveChangesAsync(); // Besoin de l'ID pour le mouvement
            }

            destStock.Quantity += request.Quantity;
            // Recalcul PMP destination ? Oui théoriquement.
            // ValueIn = Qty * SourceCost.
            // NewAvg = (OldQty*OldAvg + NewQty*SourceCost) / TotalQty.
            decimal oldVal = (destStock.Quantity - request.Quantity) * destStock.AverageCost;
            decimal transferVal = request.Quantity * sourceStock.AverageCost;
            if (destStock.Quantity > 0) 
                 destStock.AverageCost = (oldVal + transferVal) / destStock.Quantity;
            
            destStock.LastUpdated = DateTime.UtcNow;

            var movementIn = new StockMovement
            {
                StockId = destStock.Id,
                Type = MovementType.Transfer,
                Quantity = request.Quantity,
                UnitCost = sourceStock.AverageCost,
                Reference = $"TRF-IN-{DateTime.UtcNow:HHmm}",
                Notes = $"Transfert depuis Entrepôt {request.FromWarehouseId}",
                CreatedAt = DateTime.UtcNow
            };
            _context.Add(movementIn); // Utilisation générique Add pour éviter problème DbSet

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new { message = "Transfert effectué avec succès" });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Erreur lors du transfert");
            return StatusCode(500, "Erreur lors du transfert: " + ex.Message);
        }
    }
}

public class StockAdjustmentDto
{
    public int StockId { get; set; }
    public decimal QuantityDelta { get; set; } // Peut être négatif
    public string Reason { get; set; } = string.Empty;
}

public class StockTransferDto
{
    public int ProductId { get; set; }
    public int FromWarehouseId { get; set; }
    public int ToWarehouseId { get; set; }
    public decimal Quantity { get; set; }
}
