using WMS.Business.DTOs;

namespace WMS.Business.Services;

public interface IOrderService
{
    Task<OrderDto> CreateOrderAsync(CreateOrderDto createOrderDto);
    Task<OrderDto> ShipOrderAsync(int orderId, ShipOrderDto shipOrderDto);
    Task<OrderDto?> GetOrderByIdAsync(int orderId);
    Task<List<OrderDto>> GetAllOrdersAsync();
}

