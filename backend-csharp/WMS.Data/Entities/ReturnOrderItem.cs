using System.ComponentModel.DataAnnotations;

namespace WMS.Data.Entities;

public enum ReturnCondition
{
    Good,
    Damaged,
    Defective,
    Opened
}

public class ReturnOrderItem
{
    public int Id { get; set; }
    
    public int ReturnOrderId { get; set; }
    
    public int ProductId { get; set; }
    
    public decimal Quantity { get; set; }
    
    public ReturnCondition Condition { get; set; } = ReturnCondition.Good;

    // Navigation properties
    public ReturnOrder ReturnOrder { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
