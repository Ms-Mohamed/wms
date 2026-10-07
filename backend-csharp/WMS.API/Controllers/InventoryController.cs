using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WMS.Business.Exceptions;
using WMS.Business.Services;
using WMS.Data;

namespace WMS.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class InventoryController : ControllerBase
{
    private readonly WmsDbContext _context;
    private readonly IStockLedger _ledger;
    private readonly ILogger<InventoryController> _logger;

    public InventoryController(WmsDbContext context, IStockLedger ledger, ILogger<InventoryController> logger)
    {
        _context = context;
        _ledger = ledger;
        _logger = logger;
    }

    /// <summary>Signed manual correction. Never lets the stock go below zero (checked atomically in the database).</summary>
    [HttpPost("adjust")]
    public async Task<IActionResult> AdjustStock(StockAdjustmentDto request, CancellationToken ct)
    {
        try
        {
            var newQuantity = await _ledger.AdjustAsync(request.StockId, request.QuantityDelta, request.Reason, ct);
            return Ok(new { message = "Stock ajusté avec succès", newQuantity });
        }
        catch (InsufficientStockException)
        {
            return BadRequest("L'ajustement résulterait en une quantité négative.");
        }
        catch (KeyNotFoundException)
        {
            return NotFound("Stock introuvable.");
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de l'ajustement de stock");
            return StatusCode(500, "Une erreur est survenue.");
        }
    }

    /// <summary>Transfer between warehouses. Destination inherits the source's average cost (CUMP is recomputed).</summary>
    [HttpPost("transfer")]
    public async Task<IActionResult> TransferStock(StockTransferDto request, CancellationToken ct)
    {
        if (request.Quantity <= 0) return BadRequest("La quantité doit être positive.");
        if (request.FromWarehouseId == request.ToWarehouseId) return BadRequest("L'entrepôt de destination doit être différent.");

        await using var transaction = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            var sourceId = await _context.Stocks.AsNoTracking()
                .Where(s => s.ProductId == request.ProductId && s.WarehouseId == request.FromWarehouseId)
                .OrderBy(s => s.Id)
                .Select(s => (int?)s.Id)
                .FirstOrDefaultAsync(ct);
            if (sourceId == null) return BadRequest("Stock insuffisant dans l'entrepôt source.");

            var destId = await _context.Stocks.AsNoTracking()
                .Where(s => s.ProductId == request.ProductId && s.WarehouseId == request.ToWarehouseId)
                .OrderBy(s => s.Id)
                .Select(s => (int?)s.Id)
                .FirstOrDefaultAsync(ct)
                ?? await _ledger.GetOrCreateStockIdAsync(request.ProductId, request.ToWarehouseId, null, 0, ct);

            var reference = $"TRF-{DateTime.UtcNow:yyyyMMddHHmmss}";
            await _ledger.TransferAsync(sourceId.Value, destId, request.Quantity, reference, ct);

            await transaction.CommitAsync(ct);
            return Ok(new { message = "Transfert effectué avec succès", reference });
        }
        catch (InsufficientStockException)
        {
            return BadRequest("Stock insuffisant dans l'entrepôt source.");
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors du transfert");
            return StatusCode(500, "Erreur lors du transfert.");
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
