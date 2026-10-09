using WMS.Data.Entities;

namespace WMS.Business.Services;

/// <summary>
/// The ONLY way to change a stock quantity. Every operation is a single atomic statement in
/// PostgreSQL (see WMS.Data/Sql/stock_integrity.sql): it checks, updates the quantity and writes
/// the ledger row together, so concurrent callers can never oversell or drift the ledger.
/// Call inside a transaction when several lines must succeed or fail together.
/// </summary>
public interface IStockLedger
{
    /// <summary>Remove stock. Throws <see cref="Exceptions.InsufficientStockException"/> if unavailable.</summary>
    Task<decimal> IssueAsync(int stockId, decimal quantity, MovementType type, string? reference, string? notes, CancellationToken ct = default);

    /// <summary>Add stock; updates the weighted average cost (CUMP). unitCost null = at current average.</summary>
    Task<decimal> ReceiveAsync(int stockId, decimal quantity, decimal? unitCost, MovementType type, string? reference, string? notes, CancellationToken ct = default);

    /// <summary>Signed manual correction.</summary>
    Task<decimal> AdjustAsync(int stockId, decimal delta, string? reason, CancellationToken ct = default);

    Task TransferAsync(int fromStockId, int toStockId, decimal quantity, string? reference, CancellationToken ct = default);

    /// <summary>Race-free find-or-create of the stock row for (product, warehouse, location).</summary>
    Task<int> GetOrCreateStockIdAsync(int productId, int warehouseId, int? locationId, decimal reorderPoint = 0, CancellationToken ct = default);

    /// <summary>Reserve every open line of the order, all or nothing. Throws InsufficientStockException. Returns the quantity newly reserved.</summary>
    Task<decimal> ReserveOrderAsync(int orderId, CancellationToken ct = default);

    /// <summary>Ship part or all of one order line from one stock row. Returns the order status afterwards.</summary>
    Task<OrderStatus> ShipOrderLineAsync(int orderItemId, int stockId, decimal quantity, string? reference, CancellationToken ct = default);

    /// <summary>Cancel an order that has not shipped anything and release its reservations.</summary>
    Task CancelOrderAsync(int orderId, CancellationToken ct = default);

    /// <summary>Next document number from a database sequence. kind: order | invoice | po | rma.</summary>
    Task<string> NextNumberAsync(string kind, CancellationToken ct = default);
}
