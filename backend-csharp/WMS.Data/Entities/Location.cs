namespace WMS.Data.Entities;

public class Location
{
    public int Id { get; set; }
    public int WarehouseId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Zone { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Warehouse Warehouse { get; set; } = null!;
    public ICollection<Stock> Stocks { get; set; } = new List<Stock>();
}

