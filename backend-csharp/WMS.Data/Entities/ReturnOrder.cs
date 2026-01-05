using System.ComponentModel.DataAnnotations;

namespace WMS.Data.Entities;

public enum ReturnStatus
{
    Requested,
    Approved,
    Received,
    Rejected,
    Refunded
}

public class ReturnOrder
{
    public int Id { get; set; }
    
    public string ReturnNumber { get; set; } = string.Empty;
    
    public int OrderId { get; set; }
    
    public DateTime RequestDate { get; set; } = DateTime.UtcNow;
    
    public DateTime? ReceivedDate { get; set; }
    
    public ReturnStatus Status { get; set; } = ReturnStatus.Requested;
    
    public string? Reason { get; set; }
    
    public string? AdminNotes { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public Order Order { get; set; } = null!;
    public ICollection<ReturnOrderItem> Items { get; set; } = new List<ReturnOrderItem>();
}
