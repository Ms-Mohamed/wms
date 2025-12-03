using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WMS.Data;

namespace WMS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StocksController : ControllerBase
{
    private readonly WmsDbContext _context;
    private readonly ILogger<StocksController> _logger;

    public StocksController(WmsDbContext context, ILogger<StocksController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult> GetAllStocks()
    {
        try
        {
            var stocks = await _context.Stocks
                .Include(s => s.Product)
                .Include(s => s.Warehouse)
                .Include(s => s.Location)
                .Select(s => new
                {
                    s.Id,
                    ProductId = s.ProductId,
                    ProductCode = s.Product.Code,
                    ProductName = s.Product.Name,
                    WarehouseId = s.WarehouseId,
                    WarehouseName = s.Warehouse.Name,
                    LocationId = s.LocationId,
                    LocationName = s.Location != null ? s.Location.Name : null,
                    s.Quantity,
                    s.ReservedQuantity,
                    s.AvailableQuantity,
                    s.AverageCost,
                    s.ReorderPoint,
                    s.LastUpdated
                })
                .ToListAsync();

            return Ok(stocks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la récupération du stock");
            return StatusCode(500, new { error = "Une erreur est survenue lors de la récupération du stock", details = ex.Message });
        }
    }

    [HttpGet("product/{productId}/warehouse/{warehouseId}")]
    public async Task<ActionResult> GetStock(int productId, int warehouseId)
    {
        var stock = await _context.Stocks
            .Include(s => s.Product)
            .Include(s => s.Warehouse)
            .Include(s => s.Location)
            .FirstOrDefaultAsync(s => s.ProductId == productId && s.WarehouseId == warehouseId);

        if (stock == null)
        {
            return NotFound();
        }

        return Ok(new
        {
            stock.Id,
            ProductId = stock.ProductId,
            ProductCode = stock.Product.Code,
            ProductName = stock.Product.Name,
            WarehouseId = stock.WarehouseId,
            WarehouseName = stock.Warehouse.Name,
            LocationId = stock.LocationId,
            LocationName = stock.Location != null ? stock.Location.Name : null,
            stock.Quantity,
            stock.ReservedQuantity,
            stock.AvailableQuantity,
            stock.AverageCost,
            stock.ReorderPoint,
            stock.LastUpdated
        });
    }
}

