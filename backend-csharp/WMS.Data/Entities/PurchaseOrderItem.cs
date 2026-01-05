using System.ComponentModel.DataAnnotations;

namespace WMS.Data.Entities;

public class PurchaseOrderItem
{
    public int Id { get; set; }
    
    public int PurchaseOrderId { get; set; }
    
    public int ProductId { get; set; }
    
    public decimal Quantity { get; set; }
    
    public decimal UnitCost { get; set; }
    
    public decimal TotalCost { get; set; }

    // Navigation properties
    public PurchaseOrder PurchaseOrder { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
