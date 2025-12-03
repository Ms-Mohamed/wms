namespace WMS.Business.Services;

public interface IStockService
{
    Task UpdateAverageCostAsync(int stockId);
    Task<decimal> GetAverageCostAsync(int productId, int warehouseId);
}

