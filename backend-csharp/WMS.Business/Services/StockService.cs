using Microsoft.EntityFrameworkCore;
using WMS.Data;

namespace WMS.Business.Services;

public class StockService : IStockService
{
    private readonly WmsDbContext _context;

    public StockService(WmsDbContext context)
    {
        _context = context;
    }

    public async Task UpdateAverageCostAsync(int stockId)
    {
        var stock = await _context.Stocks
            .Include(s => s.Movements)
            .FirstOrDefaultAsync(s => s.Id == stockId);

        if (stock == null)
            return;

        // Calculer le CUMP (Coût Unitaire Moyen Pondéré)
        var inboundMovements = stock.Movements
            .Where(m => m.Type == Data.Entities.MovementType.Inbound)
            .OrderBy(m => m.CreatedAt)
            .ToList();

        if (!inboundMovements.Any())
            return;

        decimal totalCost = 0;
        decimal totalQuantity = 0;

        foreach (var movement in inboundMovements)
        {
            totalCost += movement.Quantity * movement.UnitCost;
            totalQuantity += movement.Quantity;
        }

        if (totalQuantity > 0)
        {
            stock.AverageCost = totalCost / totalQuantity;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<decimal> GetAverageCostAsync(int productId, int warehouseId)
    {
        var stock = await _context.Stocks
            .FirstOrDefaultAsync(s => s.ProductId == productId && s.WarehouseId == warehouseId);

        return stock?.AverageCost ?? 0;
    }
}

