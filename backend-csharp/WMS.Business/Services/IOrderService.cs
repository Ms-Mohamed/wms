using WMS.Business.DTOs;

namespace WMS.Business.Services;

public interface IOrderService
{
    /// <param name="idempotencyKey">Optional. A retry with the same key returns the original order.</param>
    Task<OrderDto> CreateOrderAsync(CreateOrderDto createOrderDto, string? idempotencyKey = null, CancellationToken ct = default);

    /// <summary>Ships an order: all-or-nothing, concurrency-safe, one invoice. See OrderService.</summary>
    Task<OrderDto> ShipOrderAsync(int orderId, ShipOrderDto shipOrderDto, CancellationToken ct = default);

    Task<OrderDto?> GetOrderByIdAsync(int orderId, CancellationToken ct = default);

    Task<PagedResult<OrderDto>> GetOrdersAsync(int? page, int? pageSize, CancellationToken ct = default);
}
