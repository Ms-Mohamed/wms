using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using WMS.Business.Exceptions;
using WMS.Data;
using WMS.Data.Entities;

namespace WMS.Business.Services;

public class StockLedger : IStockLedger
{
    // SQLSTATEs raised by the database functions (see stock_integrity.sql)
    private const string InsufficientStock = "WMS01";
    private const string InvalidQuantity = "WMS02";
    private const string StockNotFound = "WMS03";
    private const string InvalidTransfer = "WMS04";

    private readonly WmsDbContext _db;

    public StockLedger(WmsDbContext db) => _db = db;

    public Task<decimal> IssueAsync(int stockId, decimal quantity, MovementType type, string? reference, string? notes, CancellationToken ct = default)
        => RunAsync(() => ScalarAsync<decimal>(
            $"SELECT wms_stock_issue({stockId}, {quantity}, {(int)type}, {reference}, {notes}) AS \"Value\"", ct));

    public Task<decimal> ReceiveAsync(int stockId, decimal quantity, decimal? unitCost, MovementType type, string? reference, string? notes, CancellationToken ct = default)
        => RunAsync(() => ScalarAsync<decimal>(
            $"SELECT wms_stock_receive({stockId}, {quantity}, {unitCost}, {(int)type}, {reference}, {notes}) AS \"Value\"", ct));

    public Task<decimal> AdjustAsync(int stockId, decimal delta, string? reason, CancellationToken ct = default)
        => RunAsync(() => ScalarAsync<decimal>(
            $"SELECT wms_stock_adjust({stockId}, {delta}, {reason}) AS \"Value\"", ct));

    public Task TransferAsync(int fromStockId, int toStockId, decimal quantity, string? reference, CancellationToken ct = default)
        => RunAsync(async () =>
        {
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT wms_stock_transfer({fromStockId}, {toStockId}, {quantity}, {reference})", ct);
            return 0;
        });

    public Task<int> GetOrCreateStockIdAsync(int productId, int warehouseId, int? locationId, decimal reorderPoint = 0, CancellationToken ct = default)
        => RunAsync(() => ScalarAsync<int>(
            $"SELECT wms_stock_get_or_create({productId}, {warehouseId}, {locationId}, {reorderPoint}) AS \"Value\"", ct));

    public Task<string> NextNumberAsync(string kind, CancellationToken ct = default)
        => RunAsync(() => ScalarAsync<string>($"SELECT wms_next_number({kind}) AS \"Value\"", ct));

    private async Task<T> ScalarAsync<T>(FormattableString sql, CancellationToken ct)
    {
        var rows = await _db.Database.SqlQuery<T>(sql).ToListAsync(ct);
        return rows[0];
    }

    /// <summary>Translate database business errors into domain exceptions.</summary>
    private static async Task<T> RunAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (PostgresException ex) when (ex.SqlState == InsufficientStock)
        {
            int productId = 0; decimal required = 0, available = 0;
            if (!string.IsNullOrEmpty(ex.Detail))
            {
                using var doc = JsonDocument.Parse(ex.Detail);
                productId = doc.RootElement.GetProperty("productId").GetInt32();
                required = doc.RootElement.GetProperty("required").GetDecimal();
                available = doc.RootElement.GetProperty("available").GetDecimal();
            }
            throw new InsufficientStockException(productId, productId.ToString(), required, available);
        }
        catch (PostgresException ex) when (ex.SqlState == InvalidQuantity)
        {
            throw new ArgumentException(ex.MessageText, ex);
        }
        catch (PostgresException ex) when (ex.SqlState == StockNotFound)
        {
            throw new KeyNotFoundException(ex.MessageText, ex);
        }
        catch (PostgresException ex) when (ex.SqlState == InvalidTransfer)
        {
            throw new InvalidOperationException(ex.MessageText, ex);
        }
    }
}
