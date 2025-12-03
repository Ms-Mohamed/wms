namespace WMS.Data.Entities;

public class Stock
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int WarehouseId { get; set; }
    public int? LocationId { get; set; }
    public decimal Quantity { get; set; }
    public decimal ReservedQuantity { get; set; }
    public decimal AvailableQuantity => Quantity - ReservedQuantity;
    public decimal AverageCost { get; set; } // CUMP
    public decimal ReorderPoint { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Product Product { get; set; } = null!;
    public Warehouse Warehouse { get; set; } = null!;
    public Location? Location { get; set; }
    public ICollection<StockMovement> Movements { get; set; } = new List<StockMovement>();
    public ICollection<Lot> Lots { get; set; } = new List<Lot>();
}

