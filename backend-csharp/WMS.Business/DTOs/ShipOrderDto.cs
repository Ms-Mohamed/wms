namespace WMS.Business.DTOs;

public class ShipOrderDto
{
    public List<ShipOrderItemDto> Items { get; set; } = new();
}

public class ShipOrderItemDto
{
    public int OrderItemId { get; set; }
    public int LocationId { get; set; }
    public decimal Quantity { get; set; }
}
