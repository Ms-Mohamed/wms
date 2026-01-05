using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WMS.Data;
using WMS.Data.Entities;

namespace WMS.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class WarehousesController : ControllerBase
{
    private readonly WmsDbContext _context;
    private readonly ILogger<WarehousesController> _logger;

    public WarehousesController(WmsDbContext context, ILogger<WarehousesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult> GetAllWarehouses()
    {
        var warehouses = await _context.Warehouses
            .Where(w => w.IsActive)
            .OrderBy(w => w.Name)
            .Select(w => new
            {
                w.Id,
                w.Code,
                w.Name,
                w.Address,
                w.City,
                w.Country
            })
            .ToListAsync();

        return Ok(warehouses);
    }
}

