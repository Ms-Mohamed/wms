namespace WMS.Data.Entities;

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public int WarehouseId { get; set; }
    public decimal Quantity { get; set; }
    /// <summary>Units already shipped. 0 &lt;= ShippedQuantity &lt;= Quantity (CHECK in the database).</summary>
    public decimal ShippedQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal UnitPriceAtSale { get; set; } // Prix unitaire au moment de la vente
    public decimal Discount { get; set; }
    public decimal LineTotal { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Order Order { get; set; } = null!;
    public Product Product { get; set; } = null!;
    public Warehouse Warehouse { get; set; } = null!;
}

