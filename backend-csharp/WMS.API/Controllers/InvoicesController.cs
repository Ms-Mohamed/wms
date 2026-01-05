using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using WMS.API.Resources;
using Microsoft.AspNetCore.Authorization;
using WMS.Business.DTOs;
using WMS.Business.Services;

namespace WMS.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class InvoicesController : ControllerBase
{
    private readonly IInvoiceService _invoiceService;
    private readonly ILogger<InvoicesController> _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public InvoicesController(IInvoiceService invoiceService, ILogger<InvoicesController> logger, IStringLocalizer<SharedResources> localizer)
    {
        _invoiceService = invoiceService;
        _logger = logger;
        _localizer = localizer;
    }

    [HttpGet("{orderId}")]
    public async Task<ActionResult<InvoiceDto>> GetInvoiceByOrderId(int orderId)
    {
        try
        {
            var invoice = await _invoiceService.GetInvoiceByOrderIdAsync(orderId);
            if (invoice == null)
            {
                var errorMessage = _localizer["InvoiceNotFound", orderId];
                return NotFound(new { error = errorMessage.Value });
            }
            return Ok(invoice);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la récupération de la facture");
            var errorMessage = _localizer["InvoiceRetrievalError"];
            return StatusCode(500, new { error = errorMessage.Value });
        }
    }

    [HttpGet("id/{id}")]
    public async Task<ActionResult<InvoiceDto>> GetInvoiceById(int id)
    {
        try
        {
            var invoice = await _invoiceService.GetInvoiceByIdAsync(id);
            if (invoice == null)
            {
                return NotFound();
            }
            return Ok(invoice);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la récupération de la facture");
            return StatusCode(500, new { error = "Une erreur est survenue lors de la récupération de la facture" });
        }
    }
}

