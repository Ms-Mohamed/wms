using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using WMS.API.Resources;
using WMS.Business.DTOs;
using WMS.Business.Exceptions;
using WMS.Business.Services;

namespace WMS.API.Controllers;

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

    [HttpPost]
    public async Task<ActionResult<OrderDto>> CreateOrder([FromBody] CreateOrderDto createOrderDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var order = await _orderService.CreateOrderAsync(createOrderDto);
            return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order);
        }
        catch (InsufficientStockException ex)
        {
            _logger.LogWarning(ex, "Stock insuffisant pour la commande");
            var errorMessage = _localizer["InsufficientStock", ex.ProductCode, ex.RequiredQuantity, ex.AvailableQuantity];
            return BadRequest(new { error = errorMessage.Value, productId = ex.ProductId, productCode = ex.ProductCode, requiredQuantity = ex.RequiredQuantity, availableQuantity = ex.AvailableQuantity });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la création de la commande");
            var errorMessage = _localizer["OrderCreationError"];
            return StatusCode(500, new { error = errorMessage.Value });
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<OrderDto>> GetOrder(int id)
    {
        var order = await _orderService.GetOrderByIdAsync(id);
        if (order == null)
        {
            return NotFound();
        }
        return Ok(order);
    }

    [HttpGet]
    public async Task<ActionResult<List<OrderDto>>> GetAllOrders()
    {
        try
        {
            var orders = await _orderService.GetAllOrdersAsync();
            return Ok(orders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la récupération des commandes");
            return StatusCode(500, new { error = "Une erreur est survenue lors de la récupération des commandes", details = ex.Message });
        }
    }
}

