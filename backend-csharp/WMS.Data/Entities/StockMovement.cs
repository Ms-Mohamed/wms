namespace WMS.Data.Entities;

public enum MovementType
{
    Inbound,
    Outbound,
    Transfer,
    Adjustment
}

public class StockMovement
{
    public int Id { get; set; }
    public int StockId { get; set; }
    public MovementType Type { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Stock Stock { get; set; } = null!;
}

