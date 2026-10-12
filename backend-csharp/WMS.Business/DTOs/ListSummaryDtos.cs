using WMS.Data.Entities;

namespace WMS.Business.DTOs;

public class PurchaseOrderListDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string Number => OrderNumber;
    public DateTime OrderDate { get; set; }
    public DateTime Date => OrderDate;
    public string Status { get; set; } = string.Empty;
    public string Counterparty { get; set; } = string.Empty;
    public int? SupplierId { get; set; }
    public SupplierSummaryDto? Supplier { get; set; }
    public int LineCount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal Total => TotalAmount;
}

public class SupplierSummaryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class ReturnOrderListDto
{
    public int Id { get; set; }
    public string ReturnNumber { get; set; } = string.Empty;
    public string Number => ReturnNumber;
    public DateTime RequestDate { get; set; }
    public DateTime Date => RequestDate;
    public string Status { get; set; } = string.Empty;
    public string Counterparty { get; set; } = string.Empty;
    public int OrderId { get; set; }
    public OrderSummaryDto? Order { get; set; }
    public string? Reason { get; set; }
    public int LineCount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal Total => TotalAmount;
}

public class OrderSummaryDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
}
