namespace WMS.Data.Entities;

public class Product
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public decimal CostPrice { get; set; }
    public string Unit { get; set; } = "PIECE";
    public bool RequiresLotTracking { get; set; }
    public bool RequiresSerialTracking { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public int? DefaultLocationId { get; set; }
    public Location? DefaultLocation { get; set; }

    // Navigation properties
    public ICollection<Stock> Stocks { get; set; } = new List<Stock>();
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    public ICollection<InvoiceItem> InvoiceItems { get; set; } = new List<InvoiceItem>();
}

