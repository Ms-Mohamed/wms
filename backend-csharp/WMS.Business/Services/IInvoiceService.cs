using WMS.Business.DTOs;

namespace WMS.Business.Services;

public interface IInvoiceService
{
    Task<InvoiceDto?> GetInvoiceByOrderIdAsync(int orderId);
    Task<InvoiceDto?> GetInvoiceByIdAsync(int invoiceId);
}

