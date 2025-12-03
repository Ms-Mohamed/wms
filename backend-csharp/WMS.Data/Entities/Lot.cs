namespace WMS.Data.Entities;

public class Lot
{
    public int Id { get; set; }
    public int StockId { get; set; }
    public string LotNumber { get; set; } = string.Empty;
    public string? SerialNumber { get; set; }
    public decimal Quantity { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public DateTime? ProductionDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Stock Stock { get; set; } = null!;
}

