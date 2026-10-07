using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using WMS.API.Resources;
using WMS.Business.DTOs;
using WMS.Business.Exceptions;
using WMS.Business.Services;

namespace WMS.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly ILogger<OrdersController> _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public OrdersController(IOrderService orderService, ILogger<OrdersController> logger, IStringLocalizer<SharedResources> localizer)
    {
        _orderService = orderService;
        _logger = logger;
        _localizer = localizer;
    }

    /// <summary>Send an "Idempotency-Key" header so a network retry never creates a second order.</summary>
    [HttpPost]
    public async Task<ActionResult<OrderDto>> CreateOrder([FromBody] CreateOrderDto createOrderDto,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken ct)
    {
        try
        {
            string? requestHash = null;
            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                var json = System.Text.Json.JsonSerializer.Serialize(createOrderDto);
                using var sha256 = System.Security.Cryptography.SHA256.Create();
                var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(json));
                requestHash = Convert.ToBase64String(hashBytes);
            }
            var order = await _orderService.CreateOrderAsync(createOrderDto, idempotencyKey, requestHash, ct);
            return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order);
        }
        catch (ArgumentException ex)
        {
            if (ex.Message.Contains("Idempotency key reused")) return UnprocessableEntity(new { error = ex.Message });
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la création de la commande");
            var errorMessage = _localizer["OrderCreationError"];
            return StatusCode(500, new { error = errorMessage.Value });
        }
    }

    [HttpPost("{id}/ship")]
    public async Task<ActionResult<OrderDto>> ShipOrder(int id, [FromBody] ShipOrderDto shipOrderDto, CancellationToken ct)
    {
        try
        {
            var order = await _orderService.ShipOrderAsync(id, shipOrderDto, ct);
            return Ok(order);
        }
        catch (InsufficientStockException ex)
        {
            _logger.LogWarning(ex, "Stock insuffisant pour l'expédition");
            return Conflict(new
            {
                error = "Stock insuffisant",
                productId = ex.ProductId,
                productCode = ex.ProductCode,
                requiredQuantity = ex.RequiredQuantity,
                availableQuantity = ex.AvailableQuantity
            });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de l'expédition de la commande");
            return StatusCode(500, new { error = "Une erreur est survenue lors de l'expédition" });
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<OrderDto>> GetOrder(int id, CancellationToken ct)
    {
        var order = await _orderService.GetOrderByIdAsync(id, ct);
        if (order == null)
        {
            return NotFound();
        }
        return Ok(order);
    }

    /// <summary>Paged: ?page=1&amp;pageSize=500 (default and max 500). Total in the X-Total-Count header.</summary>
    [HttpGet]
    public async Task<ActionResult<List<OrderDto>>> GetAllOrders([FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct)
    {
        try
        {
            var result = await _orderService.GetOrdersAsync(page, pageSize, ct);
            Response.Headers["X-Total-Count"] = result.TotalCount.ToString();
            return Ok(result.Items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la récupération des commandes");
            return StatusCode(500, new { error = "Une erreur est survenue lors de la récupération des commandes" });
        }
    }
}
