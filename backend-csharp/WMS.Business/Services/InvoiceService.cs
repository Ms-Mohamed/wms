using Microsoft.EntityFrameworkCore;
using WMS.Business.DTOs;
using WMS.Data;

namespace WMS.Business.Services;

public class InvoiceService : IInvoiceService
{
    private readonly WmsDbContext _context;

    public InvoiceService(WmsDbContext context)
    {
        _context = context;
    }

    public async Task<InvoiceDto?> GetInvoiceByOrderIdAsync(int orderId)
    {
        var invoice = await _context.Invoices
            .Include(i => i.Order)
            .Include(i => i.Items)
                .ThenInclude(item => item.Product)
            .FirstOrDefaultAsync(i => i.OrderId == orderId);

        if (invoice == null)
            return null;

        return MapToDto(invoice);
    }

    public async Task<InvoiceDto?> GetInvoiceByIdAsync(int invoiceId)
    {
        var invoice = await _context.Invoices
            .Include(i => i.Order)
            .Include(i => i.Items)
                .ThenInclude(item => item.Product)
            .FirstOrDefaultAsync(i => i.Id == invoiceId);

        if (invoice == null)
            return null;

        return MapToDto(invoice);
    }

    private static InvoiceDto MapToDto(Data.Entities.Invoice invoice)
    {
        return new InvoiceDto
        {
            Id = invoice.Id,
            OrderId = invoice.OrderId,
            InvoiceNumber = invoice.InvoiceNumber,
            OrderNumber = invoice.Order.OrderNumber,
            InvoiceDate = invoice.InvoiceDate,
            DueDate = invoice.DueDate,
            Status = invoice.Status.ToString(),
            CustomerName = invoice.Order.CustomerName,
            CustomerEmail = invoice.Order.CustomerEmail,
            CustomerAddress = invoice.Order.CustomerAddress,
            SubTotal = invoice.SubTotal,
            TaxRate = invoice.TaxRate,
            TaxAmount = invoice.TaxAmount,
            TotalAmount = invoice.TotalAmount,
            Notes = invoice.Notes,
            Items = invoice.Items.Select(item => new InvoiceItemDto
            {
                Id = item.Id,
                ProductId = item.ProductId,
                ProductCode = item.ProductCode,
                ProductName = item.ProductName,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                Discount = item.Discount,
                TaxRate = item.TaxRate,
                LineTotal = item.LineTotal
            }).ToList()
        };
    }
}

